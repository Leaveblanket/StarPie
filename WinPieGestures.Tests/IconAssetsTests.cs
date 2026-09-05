using System;
using System.Collections.Generic;

namespace WinPieGestures.Tests;

/// <summary>
/// 共享「图标资产」出口（S1）纯函数覆盖（T3a/#65，R6/ADR-0015）：矢量图标目录完整性、
/// SVG 键目录/取值（大小写不敏感）与 SVG 路径数据解析。
/// 自定义图标存储与文件图标提取属 IO/Win32 集成面，按仓库惯例不在此单测。
/// </summary>
public sealed class IconAssetsTests
{
    [Fact]
    public void VectorIconList_EntriesAreWellFormed_AndKeysUniqueIgnoreCase()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in IconAssets.VectorIconList)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Key), "矢量图标键不能为空");
            Assert.False(string.IsNullOrWhiteSpace(item.DisplayName), "矢量图标显示名不能为空");
            Assert.False(string.IsNullOrWhiteSpace(item.Category), "矢量图标分类不能为空");
            Assert.False(string.IsNullOrWhiteSpace(item.SvgData), "矢量图标 SVG 数据不能为空");
            Assert.True(seen.Add(item.Key), $"SVG 键目录存在重复键: {item.Key}");
        }

        Assert.NotEmpty(IconAssets.VectorIconList);
    }

    [Fact]
    public void GetSvgPathByKey_ReturnsCatalogSvgData_ForEveryEntry()
    {
        foreach (var item in IconAssets.VectorIconList)
        {
            Assert.Equal(item.SvgData, IconAssets.GetSvgPathByKey(item.Key));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not-A-Real-Key")]
    public void GetSvgPathByKey_UnknownOrEmpty_ReturnsNull(string? key)
    {
        Assert.Null(IconAssets.GetSvgPathByKey(key));
    }

    [Fact]
    public void GetSvgPathByKey_IsCaseInsensitive()
    {
        string? lower = IconAssets.GetSvgPathByKey("copy");

        Assert.NotNull(lower);
        Assert.Equal(IconAssets.GetSvgPathByKey("Copy"), lower);
    }

    [Theory]
    [InlineData("M10,10 L20,20 Z", "M10,10 L20,20 Z")]
    [InlineData("  M5,5 L6,6 Z  ", "M5,5 L6,6 Z")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M1,1 L2,2 Z\"/></svg>", "M1,1 L2,2 Z")]
    [InlineData("<svg><path d='M3,3 L4,4 Z'/></svg>", "M3,3 L4,4 Z")]
    public void ExtractSvgPathData_ExtractsPathData(string content, string expected)
    {
        Assert.Equal(expected, IconAssets.ExtractSvgPathData(content));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\"/></svg>")]
    public void ExtractSvgPathData_NoPathAttribute_ReturnsEmpty(string? content)
    {
        Assert.Equal("", IconAssets.ExtractSvgPathData(content));
    }

    [Fact]
    public void CustomIconItem_IsSvg_FollowsSvgData()
    {
        Assert.False(new IconAssets.CustomIconItem { SvgData = "" }.IsSvg);
        Assert.True(new IconAssets.CustomIconItem { SvgData = "M0,0 L1,1 Z" }.IsSvg);
    }

}
