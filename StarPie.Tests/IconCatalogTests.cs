using System;
using System.Collections.Generic;
using StarPie.Services.Icons;

namespace StarPie.Tests;

/// <summary>
/// 共享「图标资产」静态纯目录（<see cref="IconCatalog"/>）的纯函数覆盖：矢量图标目录
/// 完整性、SVG 键目录/取值（大小写不敏感）与 SVG 路径数据解析。
/// 自定义图标存储与文件图标提取属 IO/Win32 集成面（<see cref="IIconAssetService"/>），
/// 按仓库惯例不在此单测（ADR-0019/#87 双形拆分后不变）。
/// </summary>
public sealed class IconCatalogTests
{
    [Fact]
    public void VectorIconList_EntriesAreWellFormed_AndKeysUniqueIgnoreCase()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in IconCatalog.VectorIconList)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Key), "矢量图标键不能为空");
            Assert.False(string.IsNullOrWhiteSpace(item.DisplayName), "矢量图标显示名不能为空");
            Assert.False(string.IsNullOrWhiteSpace(item.Category), "矢量图标分类不能为空");
            Assert.False(string.IsNullOrWhiteSpace(item.SvgData), "矢量图标 SVG 数据不能为空");
            Assert.True(seen.Add(item.Key), $"SVG 键目录存在重复键: {item.Key}");
        }

        Assert.NotEmpty(IconCatalog.VectorIconList);
    }

    [Fact]
    public void GetSvgPathByKey_ReturnsCatalogSvgData_ForEveryEntry()
    {
        foreach (var item in IconCatalog.VectorIconList)
        {
            Assert.Equal(item.SvgData, IconCatalog.GetSvgPathByKey(item.Key));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not-A-Real-Key")]
    public void GetSvgPathByKey_UnknownOrEmpty_ReturnsNull(string? key)
    {
        Assert.Null(IconCatalog.GetSvgPathByKey(key));
    }

    [Fact]
    public void GetSvgPathByKey_IsCaseInsensitive()
    {
        string? lower = IconCatalog.GetSvgPathByKey("copy");

        Assert.NotNull(lower);
        Assert.Equal(IconCatalog.GetSvgPathByKey("Copy"), lower);
    }

    [Theory]
    [InlineData("M10,10 L20,20 Z", "M10,10 L20,20 Z")]
    [InlineData("  M5,5 L6,6 Z  ", "M5,5 L6,6 Z")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M1,1 L2,2 Z\"/></svg>", "M1,1 L2,2 Z")]
    [InlineData("<svg><path d='M3,3 L4,4 Z'/></svg>", "M3,3 L4,4 Z")]
    public void ExtractSvgPathData_ExtractsPathData(string content, string expected)
    {
        Assert.Equal(expected, IconCatalog.ExtractSvgPathData(content));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\"/></svg>")]
    public void ExtractSvgPathData_NoPathAttribute_ReturnsEmpty(string? content)
    {
        Assert.Equal("", IconCatalog.ExtractSvgPathData(content));
    }

    [Fact]
    public void CustomIconItem_IsSvg_FollowsSvgData()
    {
        Assert.False(new CustomIconItem { SvgData = "" }.IsSvg);
        Assert.True(new CustomIconItem { SvgData = "M0,0 L1,1 Z" }.IsSvg);
    }
}
