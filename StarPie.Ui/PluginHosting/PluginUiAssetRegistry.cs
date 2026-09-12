using System;
using System.Collections.Generic;
using System.Linq;

namespace StarPie.PluginHosting
{
    /// <summary>
    /// 按插件 id 分组的 UI 资产登记表：可枚举、可按 id 整体摘除，是"卸载是否真的完成"的唯一账本。
    /// </summary>
    /// <remarks>
    /// 登记与摘除都在 UI 线程发生；读侧（诊断、泄漏验证）允许从任意线程快照，故内部加锁。
    /// 账本只记宿主签发的资产，插件绕过契约自建的对象不在账本内——那类残留由
    /// <see cref="PluginUiLeakVerifier"/> 的全局根扫描负责发现。
    /// </remarks>
    public sealed class PluginUiAssetRegistry
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, List<PluginUiAsset>> _byPlugin =
            new(StringComparer.Ordinal);

        /// <summary>登记一项资产；相同实例重复登记会各记一条（摘除动作由登记方保证幂等）。</summary>
        /// <param name="pluginId">插件 id（非空）。</param>
        /// <param name="kind">资产类别。</param>
        /// <param name="description">可读描述（非空）。</param>
        /// <param name="detach">摘除动作；返回是否摘除成功。</param>
        /// <returns>登记条目。</returns>
        public PluginUiAsset Track(
            string pluginId,
            PluginUiAssetKind kind,
            string description,
            Func<bool> detach)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            ArgumentNullException.ThrowIfNull(detach);

            var asset = new PluginUiAsset(pluginId, kind, description, detach);
            lock (_sync)
            {
                if (!_byPlugin.TryGetValue(pluginId, out List<PluginUiAsset>? list))
                {
                    list = new List<PluginUiAsset>();
                    _byPlugin[pluginId] = list;
                }

                list.Add(asset);
            }

            return asset;
        }

        /// <summary>摘除登记条目（摘除动作成功或插件自行 Dispose 后调用）。</summary>
        /// <param name="asset">要摘除的条目。</param>
        /// <returns>是否找到并移除。</returns>
        public bool Forget(PluginUiAsset asset)
        {
            ArgumentNullException.ThrowIfNull(asset);

            lock (_sync)
            {
                if (!_byPlugin.TryGetValue(asset.PluginId, out List<PluginUiAsset>? list))
                {
                    return false;
                }

                bool removed = list.Remove(asset);
                if (list.Count == 0)
                {
                    _byPlugin.Remove(asset.PluginId);
                }

                return removed;
            }
        }

        /// <summary>该插件当前登记的资产数。</summary>
        /// <param name="pluginId">插件 id。</param>
        public int CountFor(string pluginId)
        {
            lock (_sync)
            {
                return _byPlugin.TryGetValue(pluginId, out List<PluginUiAsset>? list) ? list.Count : 0;
            }
        }

        /// <summary>该插件的资产快照，按清理编排顺序排列。</summary>
        /// <param name="pluginId">插件 id。</param>
        public IReadOnlyList<PluginUiAsset> Snapshot(string pluginId)
        {
            lock (_sync)
            {
                return _byPlugin.TryGetValue(pluginId, out List<PluginUiAsset>? list)
                    ? list.OrderBy(item => item.Kind).ToArray()
                    : Array.Empty<PluginUiAsset>();
            }
        }

        /// <summary>全部插件的资产快照（泄漏验证与诊断用）。</summary>
        public IReadOnlyList<PluginUiAsset> SnapshotAll()
        {
            lock (_sync)
            {
                return _byPlugin.Values
                    .SelectMany(list => list)
                    .OrderBy(item => item.PluginId, StringComparer.Ordinal)
                    .ThenBy(item => item.Kind)
                    .ToArray();
            }
        }

        /// <summary>
        /// 按固定顺序执行该插件的全部摘除动作，成功的条目出账；失败条目留在账本里作为残留，
        /// 由调用方（释放编排）转换为诊断与隔离结论。
        /// </summary>
        /// <param name="pluginId">插件 id。</param>
        /// <returns>未能摘除的资产（按清理顺序）。</returns>
        public IReadOnlyList<PluginUiAsset> DetachAll(string pluginId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

            var residual = new List<PluginUiAsset>();
            foreach (PluginUiAsset asset in Snapshot(pluginId))
            {
                bool detached;
                try
                {
                    detached = asset.TryDetach();
                    if (!detached)
                    {
                        asset.DetachFailure ??= "摘除动作返回失败";
                    }
                }
                catch (Exception exception)
                {
                    detached = false;
                    asset.DetachFailure = $"{exception.GetType().Name}：{exception.Message}";
                }

                if (detached)
                {
                    Forget(asset);
                }
                else
                {
                    residual.Add(asset);
                }
            }

            return residual;
        }
    }
}
