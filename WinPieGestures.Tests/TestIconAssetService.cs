using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StarPie.Services.Icons;

namespace StarPie.Tests;

/// <summary>
/// <see cref="IIconAssetService"/> 测试替身：注入的条目列表为空存储，导入/删除/位图
/// 提取均为空操作——供 VM/渲染器/模块注册器微型容器测试注入，不触真实文件 IO 与 Win32。
/// </summary>
public sealed class TestIconAssetService : IIconAssetService
{
    private readonly List<CustomIconItem> _items;

    public TestIconAssetService(params CustomIconItem[] items)
    {
        _items = new List<CustomIconItem>(items);
    }

    public string GetCustomIconsDirectory()
        => Path.Combine(Path.GetTempPath(), "StarPie-Tests-CustomIcons");

    public IReadOnlyList<CustomIconItem> GetCustomIcons() => _items;

    public CustomIconItem? ImportCustomIcon(string sourceFilePath, string? customName = null) => null;

    public CustomIconItem? ImportCustomSvgData(string svgPathData, string iconName) => null;

    public bool DeleteCustomIcon(string key) => false;

    public ImageSource? GetCustomImageSource(string iconKeyOrPath) => null;

    public BitmapSource? GetIcon(string path) => null;
}
