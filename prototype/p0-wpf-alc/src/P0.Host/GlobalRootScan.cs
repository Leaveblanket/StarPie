using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;

namespace P0.Host;

/// <summary>
/// 全局根扫描（对应 P3 <c>PluginUiLeakVerifier</c> 的"全局根扫描"一半）。
/// 只回答一个问题：WPF 的全局根里还有没有属于插件程序集的对象，以及是哪条路径。
/// 注意：插件内部静态缓存这类根扫不到——那正是 WeakReference 判定的职责。
/// </summary>
internal static class GlobalRootScan
{
    private const int MaxDepth = 4;
    private const int MaxFindings = 40;

    public static List<string> Scan(Assembly pluginAssembly, string phase)
    {
        var found = new List<string>();
        try
        {
            var app = Application.Current;
            if (app is null)
            {
                return found;
            }

            foreach (Window window in app.Windows)
            {
                Check($"Application.Windows[{window.GetType().Name}]", window);
            }

            Check("Application.MainWindow", app.MainWindow);
            WalkDictionary("Application.Current.Resources", app.Resources, 0);
        }
        catch (Exception ex)
        {
            found.Add($"[scan-error] {ex.GetType().Name}: {ex.Message}");
        }

        if (found.Count > 0)
        {
            found.Insert(0, $"[{phase}] 命中 {found.Count} 条全局根残留");
        }

        return found;

        void Check(string path, object? candidate)
        {
            if (candidate is null || found.Count >= MaxFindings)
            {
                return;
            }

            if (candidate.GetType().Assembly == pluginAssembly)
            {
                found.Add($"{path} → {candidate.GetType().FullName}");
            }
        }

        void WalkDictionary(string path, ResourceDictionary dictionary, int depth)
        {
            if (depth > MaxDepth || found.Count >= MaxFindings)
            {
                return;
            }

            Check(path, dictionary);

            for (var i = 0; i < dictionary.MergedDictionaries.Count; i++)
            {
                ResourceDictionary merged;
                try
                {
                    merged = dictionary.MergedDictionaries[i];
                }
                catch (Exception ex)
                {
                    found.Add($"{path}.MergedDictionaries[{i}] → 读取失败 {ex.GetType().Name}");
                    continue;
                }

                WalkDictionary($"{path}.MergedDictionaries[{i}]", merged, depth + 1);
            }

            List<object> keys;
            try
            {
                keys = new List<object>();
                foreach (var key in dictionary.Keys)
                {
                    if (key is not null)
                    {
                        keys.Add(key);
                    }
                }
            }
            catch (Exception ex)
            {
                found.Add($"{path}.Keys → 枚举失败 {ex.GetType().Name}: {ex.Message}");
                return;
            }

            foreach (var key in keys)
            {
                if (found.Count >= MaxFindings)
                {
                    return;
                }

                Check($"{path}['{key}']", key);
                try
                {
                    Check($"{path}['{key}']", dictionary[key]);
                }
                catch
                {
                    // 延迟内容（DeferrableContent）取不到值时跳过：不算残留。
                }
            }
        }
    }
}
