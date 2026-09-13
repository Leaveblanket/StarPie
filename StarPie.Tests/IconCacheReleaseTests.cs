using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StarPie.Icons;
using StarPie.Services.Icons;

namespace StarPie.Tests;

/// <summary>
/// 图标缓存出账测试（#153）：内核侧列表缓存清空幂等、清后重建（条目一致、缓存对象更新）；
/// 提取的位图无静态 root（WeakReference 判定可回收）。
/// </summary>
public sealed class IconCacheReleaseTests
{
    /// <summary>IShortcutTargetResolver 测试替身：不解析任何快捷方式（走路径兜底分支）。</summary>
    private sealed class NoShortcutResolver : IShortcutTargetResolver
    {
        public bool ResolveShortcutTarget(string lnkPath, out string targetPath, out string iconPath, out int iconIndex)
        {
            targetPath = "";
            iconPath = "";
            iconIndex = 0;
            return false;
        }
    }

    private static string CreateIconSandbox(out CustomIconStore store)
    {
        string root = Directory.CreateTempSubdirectory("starpie-icon-cache-tests").FullName;
        store = new CustomIconStore(() => root);
        Directory.CreateDirectory(Path.Combine(root, "CustomIcons"));
        File.WriteAllText(
            Path.Combine(root, "CustomIcons", "probe.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0,0 L1,1\"/></svg>");
        return root;
    }

    [Fact]
    public void 清空缓存_幂等_清后重建条目一致()
    {
        CreateIconSandbox(out CustomIconStore store);

        IReadOnlyList<CustomIconItem> first = store.GetCustomIcons();
        Assert.Single(first);

        // 两次清空不抛、状态一致；重建条目内容一致但为重新扫描的新缓存对象
        store.ClearCache();
        store.ClearCache();
        IReadOnlyList<CustomIconItem> rebuilt = store.GetCustomIcons();

        Assert.Single(rebuilt);
        Assert.Equal(first[0].Key, rebuilt[0].Key);
        Assert.Equal(first[0].FilePath, rebuilt[0].FilePath);
        Assert.Equal(first[0].SvgData, rebuilt[0].SvgData);
    }

    [Fact]
    public void 释放后新导入可见_缓存按需重建()
    {
        CreateIconSandbox(out CustomIconStore store);
        Assert.Single(store.GetCustomIcons());

        store.ClearCache();
        string added = Path.Combine(store.GetCustomIconsDirectory(), "added.svg");
        File.WriteAllText(added, "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M2,2 L3,3\"/></svg>");
        IReadOnlyList<CustomIconItem> rebuilt = store.GetCustomIcons();

        Assert.Equal(2, rebuilt.Count);
    }

    [Fact]
    public void 提取位图无静态root_引用放弃后可回收()
    {
        CreateIconSandbox(out CustomIconStore store);
        var service = new IconAssetService(store, new NoShortcutResolver());

        WeakReference reference = StaTestHarness.Run(() =>
        {
            // 不存在的路径走"按属性取系统关联图标"兜底分支,产出冻结位图;服务不持有它
            BitmapSource? bitmap = service.GetIcon(@"Z:\__starpie_no_such_file__.exe");
            Assert.NotNull(bitmap);

            // 排空 Dispatcher 上位图创建遗留的清理载荷(同 W2 预热产物排空,不排空则滞留)
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                () => { }, System.Windows.Threading.DispatcherPriority.Render);

            return new WeakReference(bitmap);
        });

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(reference.IsAlive, "提取的位图不应被服务静态 root 滞留");
    }
}
