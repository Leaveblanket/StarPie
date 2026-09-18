using System;
using StarPie.Host.Icons;
using StarPie.Sdk.Services.Icons;
using StarPie.Sdk.Wpf.Services.Icons;
using StarPie.Ui.Services.Icons;
using StarPie.Host.Wheel;

namespace StarPie.Tests;

/// <summary>
/// 扇区内容共享内核的纯逻辑覆盖：图标五级回退链（各来源与空值/坏值回落）、
/// 按扇区数（4/6/8/12）的排版缩放与字号区间、内置向量字面量集中与系统参数映射。
/// 内核零 WPF、入参不含配置对象，故全部用例 headless 直构。
/// </summary>
public sealed class WheelSectorContentKernelTests
{
    /// <summary>通用排版：8 扇区、显示文字、配置默认图标尺寸与字号。</summary>
    private static WheelSectorLayoutSpec Layout(
        int sectorCount = 8,
        string layoutMode = "IconAndText",
        bool showText = true,
        double iconSize = 20.0,
        double fontSize = 10.5)
        => new(sectorCount, layoutMode, showText, iconSize, fontSize);

    private static WheelSectorContent Build(
        WheelSectorInput sector,
        WheelSectorLayoutSpec? layout = null,
        double scale = 1.0,
        Func<string, bool>? isParsableSvg = null)
        => WheelSectorContentKernel.Build(sector, layout ?? Layout(), scale, isParsableSvg);

    private static WheelSectorInput Sector(
        string? name = "动作",
        string? type = "Hotkey",
        string? parameter = "",
        string? iconKey = "",
        string? customIconSvg = "",
        CustomIconItem? customIcon = null)
        => new(name, type, parameter, iconKey, customIconSvg, customIcon);

    // --- 按扇区数的排版缩放表 ------------------------------------------

    [Theory]
    [InlineData(4, 96.0, 72.0, 24.0, 11.5, 90.0)]   // 图标 20 × 1.20；字号抬到下限 11.5
    [InlineData(6, 84.0, 64.0, 20.0, 10.5, 78.0)]   // 通用列
    [InlineData(8, 84.0, 64.0, 20.0, 10.5, 78.0)]   // 通用列
    [InlineData(12, 58.0, 48.0, 16.4, 9.5, 50.0)]   // 图标 20 × 0.82；字号压到上限 9.5
    public void Build_ScalesLayoutBySectorCount(
        int sectorCount, double containerWidth, double containerHeight,
        double iconSize, double fontSize, double textMaxWidth)
    {
        var content = Build(Sector(type: "Hotkey"), Layout(sectorCount));

        Assert.Equal(containerWidth, content.ContainerWidth, 3);
        Assert.Equal(containerHeight, content.ContainerHeight, 3);
        Assert.Equal(iconSize, content.IconSize, 3);
        Assert.Equal(fontSize, content.FontSize, 3);
        Assert.Equal(textMaxWidth, content.TextMaxWidth, 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-3)]
    public void Build_UnknownSectorCount_FallsBackToGenericRow(int sectorCount)
    {
        var content = Build(Sector(), Layout(sectorCount));

        Assert.Equal(84.0, content.ContainerWidth, 3);
        Assert.Equal(20.0, content.IconSize, 3);
        Assert.Equal(10.5, content.FontSize, 3);
        Assert.Equal(78.0, content.TextMaxWidth, 3);
    }

    [Fact]
    public void Build_TextOnlyLayout_LiftsFont_AndIconOnlyLayout_ShrinksNothingButBoostsIcon()
    {
        var textOnly = Build(Sector(type: "Hotkey"), Layout(layoutMode: "TextOnly"));
        var iconOnly = Build(Sector(type: "Hotkey"), Layout(layoutMode: "IconOnly"));

        Assert.Equal(11.5, textOnly.FontSize, 3);               // 10.5 + 1.0
        Assert.Equal(WheelIconKind.None, textOnly.Icon.Kind);   // 纯文字不画图标
        Assert.True(textOnly.ShowText);                         // 文字仍在

        Assert.Equal(27.0, iconOnly.IconSize, 3);               // 20 × 1.35
        Assert.False(iconOnly.ShowText);                        // 纯图标不画文字
        Assert.Equal(0.0, iconOnly.IconBottomMargin, 3);        // 无文字即无间距
    }

    [Fact]
    public void Build_BlankLayoutMode_FallsBackToIconAndText()
    {
        var content = Build(Sector(type: "Hotkey"), new WheelSectorLayoutSpec(8, "", true, 0, 0));

        Assert.NotEqual(WheelIconKind.None, content.Icon.Kind);
        Assert.True(content.ShowText);
        Assert.Equal(20.0, content.IconSize, 3);   // 图标尺寸 0 → 默认 20
        Assert.Equal(10.5, content.FontSize, 3);   // 字号 0 → 默认 10.5
    }

    [Fact]
    public void Build_ScaleMultipliesContentMetrics()
    {
        var content = Build(Sector(type: "Hotkey"), Layout(), scale: 0.5);

        Assert.Equal(42.0, content.ContainerWidth, 3);
        Assert.Equal(10.0, content.IconSize, 3);
        Assert.Equal(5.25, content.FontSize, 3);
        Assert.Equal(39.0, content.TextMaxWidth, 3);
        Assert.Equal(1.0, content.IconBottomMargin, 3);
    }

    // --- 图标回退链：各来源 --------------------------------------------

    [Fact]
    public void ResolveIcon_ActionCustomSvgWins()
    {
        var content = Build(Sector(type: "Launch", parameter: @"C:\chrome.exe", iconKey: "Copy", customIconSvg: "M0,0L9,9"));

        Assert.Equal(WheelIconKind.SvgPath, content.Icon.Kind);
        Assert.Equal("M0,0L9,9", content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_CustomIconKey_WithSvgData_UsesStoreSvg()
    {
        var item = new CustomIconItem { Key = "custom:star", SvgData = "M1,1L2,2", FilePath = @"C:\icons\star.svg" };

        var content = Build(Sector(iconKey: "custom:star", customIcon: item));

        Assert.Equal(WheelIconKind.SvgPath, content.Icon.Kind);
        Assert.Equal("M1,1L2,2", content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_CustomIconKey_WithRasterFile_UsesStoreFile()
    {
        var item = new CustomIconItem { Key = "custom:photo", SvgData = "", FilePath = @"C:\icons\photo.png" };

        var content = Build(Sector(iconKey: "custom:photo", customIcon: item));

        Assert.Equal(WheelIconKind.CustomImageFile, content.Icon.Kind);
        Assert.Equal(@"C:\icons\photo.png", content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_CustomIconKeyWithPrefix_CaseInsensitive()
    {
        var item = new CustomIconItem { Key = "CUSTOM:star", SvgData = "M1,1L2,2" };

        var content = Build(Sector(iconKey: "CUSTOM:star", customIcon: item));

        Assert.Equal(WheelIconKind.SvgPath, content.Icon.Kind);
        Assert.Equal("M1,1L2,2", content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_BuiltInKey_UsesCatalogIgnoringCase()
    {
        var content = Build(Sector(iconKey: "showdesktop"));

        Assert.Equal(WheelIconKind.SvgPath, content.Icon.Kind);
        Assert.Equal(IconCatalog.GetSvgPathByKey("ShowDesktop"), content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_LaunchParameter_UsesProgramIconWithExtraPadding()
    {
        var content = Build(Sector(type: "Launch", parameter: @"C:\chrome.exe"));

        Assert.Equal(WheelIconKind.ProgramIcon, content.Icon.Kind);
        Assert.Equal(@"C:\chrome.exe", content.Icon.Data);
        Assert.Equal(24.0, content.IconSize, 3);   // 20 + 4（程序图标留白）
    }

    [Theory]
    [InlineData("lock", "Lock")]
    [InlineData("VolumeUp", "VolumeUp")]
    [InlineData("  volumedown  ", "VolumeDown")]
    [InlineData("VolumeMute", "VolumeMute")]
    [InlineData("ShowDesktop", "ShowDesktop")]
    [InlineData("screenshot", "Screenshot")]
    public void ResolveIcon_SystemParameter_MapsToCatalogKey(string parameter, string expectedKey)
    {
        var content = Build(Sector(type: "System", parameter: parameter));

        Assert.Equal(WheelIconKind.SvgPath, content.Icon.Kind);
        Assert.Equal(IconCatalog.GetSvgPathByKey(expectedKey), content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_SystemParameterUnmapped_FindsNoIcon()
    {
        // 参数未经映射时不得原样当图标键查目录（预览侧旧实现的漂移）。
        var content = Build(Sector(type: "System", parameter: "CloseWindow"));

        Assert.Equal(WheelIconKind.None, content.Icon.Kind);
        Assert.Equal(0.0, content.IconSize, 3);
    }

    [Fact]
    public void ResolveIcon_FolderAction_UsesFolderVector()
    {
        var content = Build(Sector(type: "Folder", parameter: @"C:\temp"));

        Assert.Equal(WheelIconKind.SvgPath, content.Icon.Kind);
        Assert.Equal(IconCatalog.GetSvgPathByKey("Folder"), content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_Hotkey_UsesSharedBuiltInLiteral()
    {
        var content = Build(Sector(type: "Hotkey", parameter: "Ctrl+C"));

        Assert.Equal(WheelIconKind.SvgPath, content.Icon.Kind);
        Assert.Equal(WheelBuiltInIcons.Hotkey, content.Icon.Data);
    }

    // --- 图标回退链：空值与坏值回落 ------------------------------------

    [Fact]
    public void ResolveIcon_SlotWithoutAction_CarriesHotkeyTypeFromCaller()
    {
        // 无动作槽位的 "Hotkey" 由消费方给出（WheelSectorViewModel 的视图默认值），
        // 内核据此给内置键盘图标。
        var content = Build(new WheelSectorInput("", "Hotkey", "", "", ""));

        Assert.Equal(WheelIconKind.SvgPath, content.Icon.Kind);
        Assert.Equal(WheelBuiltInIcons.Hotkey, content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_BlankType_FindsNoIcon()
    {
        // 空类型不给内置向量：回落口径不在内核里重造，由消费方决定传什么类型。
        var content = Build(new WheelSectorInput("", null, null, null, null));

        Assert.Equal(WheelIconKind.None, content.Icon.Kind);
    }

    [Fact]
    public void ResolveIcon_UnknownTypeAndParameter_FindsNoIcon()
    {
        var content = Build(Sector(type: "Nonsense", parameter: "whatever"));

        Assert.Equal(WheelIconKind.None, content.Icon.Kind);
        Assert.Equal("", content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_UnparsableActionSvg_FallsThroughToIconKey()
    {
        var content = Build(
            Sector(iconKey: "Copy", customIconSvg: "not-a-path"),
            isParsableSvg: _ => false);

        Assert.Equal(WheelIconKind.SvgPath, content.Icon.Kind);
        Assert.Equal(IconCatalog.GetSvgPathByKey("Copy"), content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_UnparsableActionSvg_AndNoKey_FallsThroughToBuiltInVector()
    {
        var content = Build(Sector(type: "Folder", customIconSvg: "not-a-path"), isParsableSvg: _ => false);

        Assert.Equal(IconCatalog.GetSvgPathByKey("Folder"), content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_UnparsableStoredSvg_FallsThroughToBuiltInVector()
    {
        var item = new CustomIconItem { Key = "custom:star", SvgData = "not-a-path" };

        var content = Build(
            Sector(type: "System", parameter: "lock", iconKey: "custom:star", customIcon: item),
            isParsableSvg: _ => false);

        Assert.Equal(IconCatalog.GetSvgPathByKey("Lock"), content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_CustomKeyMissingFromStore_DoesNotQueryCatalog()
    {
        // 前缀键未命中目录时不得把 "custom:xxx" 当内置图标键查（旧运行态即如此）。
        var content = Build(Sector(type: "System", parameter: "lock", iconKey: "custom:absent"));

        Assert.Equal(IconCatalog.GetSvgPathByKey("Lock"), content.Icon.Data);
    }

    [Fact]
    public void ResolveIcon_UnknownIconKey_FallsThroughToActionTypeVector()
    {
        var content = Build(Sector(type: "Hotkey", iconKey: "NoSuchKey"));

        Assert.Equal(WheelBuiltInIcons.Hotkey, content.Icon.Data);
    }

    // --- 文字口径 ------------------------------------------------------

    [Fact]
    public void ShowText_RequiresBothLayoutFlagAndNonEmptyText()
    {
        Assert.False(Build(Sector(name: ""), Layout()).ShowText);
        Assert.False(Build(Sector(name: "动作"), Layout(showText: false)).ShowText);
        Assert.True(Build(Sector(name: "动作"), Layout()).ShowText);
    }

    [Fact]
    public void ShowText_ResolvedTextIsCarriedThrough()
    {
        var content = Build(Sector(name: "未设置"), Layout());

        Assert.True(content.ShowText);
        Assert.Equal("未设置", content.Text);
        Assert.Equal(2.0, content.IconBottomMargin, 3);
        Assert.Equal(1.0, content.TextTopMargin, 3);
    }

    [Fact]
    public void ShowText_HiddenLayout_KeepsIconGapAtZero()
    {
        var content = Build(Sector(name: "动作"), Layout(showText: false));

        Assert.Equal(0.0, content.IconBottomMargin, 3);
    }

    // --- 内置向量与系统参数映射（纯函数面） ----------------------------

    [Theory]
    [InlineData("Lock", "lock")]
    [InlineData("VolumeUp", "VOLUMEUP")]
    [InlineData("ShowDesktop", "  ShowDesktop ")]
    [InlineData("Screenshot", "screenshot")]
    public void SystemParameterIconKey_NormalizesCaseAndWhitespace(string expectedKey, string parameter)
        => Assert.Equal(expectedKey, WheelBuiltInIcons.SystemParameterIconKey(parameter));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CloseWindow")]
    public void SystemParameterIconKey_UnmappedOrBlank_ReturnsNull(string? parameter)
        => Assert.Null(WheelBuiltInIcons.SystemParameterIconKey(parameter));

    [Fact]
    public void IsCustomIconKey_RequiresPrefixAndNonEmpty()
    {
        Assert.True(WheelSectorContentKernel.IsCustomIconKey("custom:star"));
        Assert.True(WheelSectorContentKernel.IsCustomIconKey("Custom:star"));
        Assert.False(WheelSectorContentKernel.IsCustomIconKey("Copy"));
        Assert.False(WheelSectorContentKernel.IsCustomIconKey(""));
        Assert.False(WheelSectorContentKernel.IsCustomIconKey(null));
    }

    [Fact]
    public void BuiltInVector_HotkeyLiteral_IsNonEmptyPathData()
    {
        Assert.StartsWith("M", WheelBuiltInIcons.Hotkey, StringComparison.Ordinal);
    }
}
