using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace P0.Host;

/// <summary>
/// 根因定位器：ALC 未回收时，扫描 WPF / System.Xaml / BCL / 本宿主的**静态字段**，
/// 找出「哪个类型的哪个静态字段仍然拿着插件程序集、插件类型或插件实例」。
/// 只做有限层展开，用来回答「谁把插件程序集钉住了」。
/// </summary>
internal static class RootFinder
{
    private const int MaxHits = 60;
    private const int MaxEntriesPerCollection = 4000;
    private const int MaxDepth = 2;

    public static List<string> Find(string pluginAssemblyName)
    {
        var hits = new List<string>();
        var watch = Stopwatch.StartNew();
        var types = 0;
        var fields = 0;

        foreach (var assembly in ScanAssemblies())
        {
            Type?[] all;
            try
            {
                all = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                all = ex.Types;
            }
            catch
            {
                continue;
            }

            foreach (var type in all)
            {
                if (type is null || type.IsGenericTypeDefinition || type.Namespace is null || !IsInterestingNamespace(type.Namespace))
                {
                    continue;
                }

                types++;
                FieldInfo[] fieldInfos;
                try
                {
                    fieldInfos = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
                catch
                {
                    continue;
                }

                foreach (var field in fieldInfos)
                {
                    fields++;
                    if (!IsInterestingFieldType(field.FieldType))
                    {
                        continue;
                    }

                    object? value;
                    try
                    {
                        value = field.GetValue(null);
                    }
                    catch
                    {
                        continue;
                    }

                    if (value is null)
                    {
                        continue;
                    }

                    Inspect($"{type.FullName}.{field.Name}", value, pluginAssemblyName, hits, 0);
                    if (hits.Count >= MaxHits)
                    {
                        hits.Add($"[scan] 达到上限，提前结束（已扫 {types} 类型 / {fields} 字段 / {watch.ElapsedMilliseconds}ms）");
                        return hits;
                    }
                }
            }
        }

        hits.Add($"[scan] 覆盖：类型={types} 静态字段={fields} 用时={watch.ElapsedMilliseconds}ms 命中={hits.Count}");
        return hits;
    }

    /// <summary>
    /// 强制清除扫描范围内、由插件拥有的全局缓存条目（评估「能不能靠清缓存换来真卸载」）。
    /// 只动 key/value 命中插件的条目；这是**不受支持的私有反射操作**，仅用于打样取证。
    /// </summary>
    public static List<string> Purge(string pluginAssemblyName)
    {
        var purged = new List<string>();
        foreach (var assembly in ScanAssemblies())
        {
            Type?[] all;
            try
            {
                all = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                all = ex.Types;
            }
            catch
            {
                continue;
            }

            foreach (var type in all)
            {
                if (type is null || type.IsGenericTypeDefinition || type.Namespace is null || !IsInterestingNamespace(type.Namespace))
                {
                    continue;
                }

                FieldInfo[] fieldInfos;
                try
                {
                    fieldInfos = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                }
                catch
                {
                    continue;
                }

                foreach (var field in fieldInfos)
                {
                    if (!IsInterestingFieldType(field.FieldType))
                    {
                        continue;
                    }

                    object? value;
                    try
                    {
                        value = field.GetValue(null);
                    }
                    catch
                    {
                        continue;
                    }

                    var path = $"{type.FullName}.{field.Name}";
                    try
                    {
                        if (value is IDictionary dictionary)
                        {
                            var keys = new List<object>();
                            foreach (DictionaryEntry entry in dictionary)
                            {
                                if (IsPluginObject(entry.Key!, pluginAssemblyName, out _)
                                    || IsPluginObject(entry.Value!, pluginAssemblyName, out _))
                                {
                                    keys.Add(entry.Key!);
                                }
                            }

                            foreach (var key in keys)
                            {
                                dictionary.Remove(key);
                            }

                            if (keys.Count > 0)
                            {
                                purged.Add($"{path}：移除 {keys.Count} 条");
                            }
                        }
                        else if (value is IList list)
                        {
                            var removed = 0;
                            for (var i = list.Count - 1; i >= 0; i--)
                            {
                                if (IsPluginObject(list[i]!, pluginAssemblyName, out _))
                                {
                                    list.RemoveAt(i);
                                    removed++;
                                }
                            }

                            if (removed > 0)
                            {
                                purged.Add($"{path}：移除 {removed} 项");
                            }
                        }
                    }
                    catch
                    {
                        // 缓存不可改：跳过。
                    }
                }
            }
        }

        return purged;
    }

    private static IEnumerable<Assembly> ScanAssemblies()    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in new[]
                 {
                     typeof(Application), typeof(System.Windows.Media.Visual), typeof(System.Windows.Threading.Dispatcher),
                     typeof(System.Xaml.XamlReader), typeof(System.ComponentModel.TypeDescriptor), typeof(System.IO.Packaging.Package),
                     typeof(RootFinder)
                 })
        {
            var assembly = type.Assembly;
            if (seen.Add(assembly.GetName().Name ?? string.Empty))
            {
                yield return assembly;
            }
        }
    }

    private static bool IsInterestingNamespace(string ns) =>
        ns.StartsWith("System.Windows", StringComparison.Ordinal)
        || ns.StartsWith("MS.Internal", StringComparison.Ordinal)
        || ns.StartsWith("System.Xaml", StringComparison.Ordinal)
        || ns.StartsWith("System.IO.Packaging", StringComparison.Ordinal)
        || ns.StartsWith("System.ComponentModel", StringComparison.Ordinal)
        || ns.StartsWith("P0.Host", StringComparison.Ordinal);

    private static bool IsInterestingFieldType(Type fieldType)
    {
        if (fieldType == typeof(object) || typeof(IEnumerable).IsAssignableFrom(fieldType))
        {
            return true;
        }

        if (typeof(Assembly).IsAssignableFrom(fieldType) || typeof(Type).IsAssignableFrom(fieldType)
            || typeof(Delegate).IsAssignableFrom(fieldType))
        {
            return true;
        }

        // 泛型容器（Dictionary/HashSet/List 等）
        return fieldType.IsGenericType;
    }

    private static void Inspect(string path, object? value, string pluginAssemblyName, List<string> hits, int depth)
    {
        if (value is null || depth > MaxDepth || hits.Count >= MaxHits)
        {
            return;
        }

        if (IsPluginObject(value, pluginAssemblyName, out var how))
        {
            hits.Add($"{path} → {how}");
            return;
        }

        try
        {
            if (value is IDictionary dictionary)
            {
                var count = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (count++ > MaxEntriesPerCollection)
                    {
                        break;
                    }

                    Inspect($"{path}[key]", entry.Key, pluginAssemblyName, hits, depth + 1);
                    Inspect($"{path}[value]", entry.Value, pluginAssemblyName, hits, depth + 1);
                }

                return;
            }

            if (value is not IEnumerable enumerable || value is string)
            {
                return;
            }

            var itemCount = 0;
            foreach (var item in enumerable)
            {
                if (itemCount++ > MaxEntriesPerCollection)
                {
                    break;
                }

                Inspect($"{path}[item]", item, pluginAssemblyName, hits, depth + 1);
            }
        }
        catch
        {
            // 集合不可枚举：跳过。
        }
    }

    private static bool IsPluginObject(object candidate, string pluginAssemblyName, out string how)
    {
        how = string.Empty;
        switch (candidate)
        {
            case Assembly assembly when assembly.GetName().Name == pluginAssemblyName:
                how = $"Assembly({pluginAssemblyName})";
                return true;
            case Type type when type.Assembly.GetName().Name == pluginAssemblyName:
                how = $"Type({type.FullName})";
                return true;
            case Delegate del:
                if (del.Target is { } target && IsPluginObject(target, pluginAssemblyName, out var targetHow))
                {
                    how = "Delegate→" + targetHow;
                    return true;
                }

                if (del.Method.DeclaringType is { } declaring && declaring.Assembly.GetName().Name == pluginAssemblyName)
                {
                    how = $"Delegate→{declaring.FullName}";
                    return true;
                }

                break;
        }

        var candidateType = candidate.GetType();
        if (candidateType.Assembly.GetName().Name == pluginAssemblyName)
        {
            how = $"实例({candidateType.FullName})";
            return true;
        }

        // 框架对象内嵌插件 Type（DataTemplate.DataType / Style.TargetType / XamlType.UnderlyingType …）
        foreach (var propertyName in new[] { "DataType", "TargetType", "UnderlyingType" })
        {
            var property = candidateType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || property.PropertyType != typeof(Type))
            {
                continue;
            }

            try
            {
                if (property.GetValue(candidate) is Type held && held.Assembly.GetName().Name == pluginAssemblyName)
                {
                    how = $"{candidateType.Name}.{propertyName}→Type({held.FullName})";
                    return true;
                }
            }
            catch
            {
                // 忽略。
            }
        }

        return false;
    }
}
