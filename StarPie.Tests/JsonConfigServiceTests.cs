using System;
using System.Collections.Generic;
using System.IO;
using StarPie.Ui;

namespace StarPie.Tests;

/// <summary>
/// 配置持久化的读写边界：加载是「缺文件建默认、损坏文件不覆写、容忍手改的 JSON（注释 / 尾逗号 /
/// 大小写）、旧键迁到正典键且正典键优先」，保存只写正典键，<c>GetProfileForProcess</c> 不区分大小写
/// 并回落 Global，导入换配置时连带换运行态语言，导出产物可被导入读回、写失败按 false 上报。
/// </summary>
public sealed class JsonConfigServiceTests : IDisposable
{
    private static readonly LocalizationService Localization = new();

    private readonly string _tempDir;
    private readonly string _configPath;

    public JsonConfigServiceTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("starpie-config-tests").FullName;
        _configPath = Path.Combine(_tempDir, "config.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Load_WithExistingFile_ReadsValuesIntoCurrent()
    {
        File.WriteAllText(_configPath, """
            {
              "Language": "en",
              "DragThreshold": 42.0,
              "Profiles": [
                {
                  "ProcessName": "explorer.exe",
                  "SectorCount": 4,
                  "Actions": [
                    { "Type": "Hotkey", "Name": "复制", "Parameter": "Ctrl+C" }
                  ]
                }
              ]
            }
            """);
        var service = new JsonConfigService(_configPath, Localization);

        service.Load();

        Assert.Equal(42.0, service.Current.DragThreshold);
        Assert.Equal("en", service.Current.Language);
        var profile = Assert.Single(service.Current.Profiles);
        Assert.Equal("explorer.exe", profile.ProcessName);
        Assert.Equal(4, profile.SectorCount);
        var action = Assert.Single(profile.Actions);
        Assert.Equal("Hotkey", action.Type);
        Assert.Equal("Ctrl+C", action.Parameter);
    }

    [Fact]
    public void Load_WithMissingFile_CreatesDefaultConfigOnDisk()
    {
        var service = new JsonConfigService(_configPath, Localization);

        service.Load();

        Assert.True(File.Exists(_configPath));
        Assert.Equal(25.0, service.Current.DragThreshold);
        Assert.Equal("Global", service.Current.Profiles[0].ProcessName);
        Assert.Equal(8, service.Current.Profiles[0].SectorCount);
        Assert.Equal(3, service.Current.Profiles.Count);
    }

    [Fact]
    public void Load_LegacyConfigWithoutTriggerButtonKey_FallsBackToRightButton()
    {
        File.WriteAllText(_configPath, """{ "Language": "en", "DragThreshold": 42.0 }""");
        var service = new JsonConfigService(_configPath, Localization);

        service.Load();

        Assert.Equal("RightButton", service.Current.TriggerButton);
    }

    [Fact]
    public void TriggerButton_RoundTripsThroughDisk()
    {
        var writer = new JsonConfigService(_configPath, Localization);
        writer.Load();
        writer.Current.TriggerButton = "XButton1";

        writer.Save();

        var reader = new JsonConfigService(_configPath, Localization);
        reader.Load();
        Assert.Equal("XButton1", reader.Current.TriggerButton);
    }

    [Fact]
    public void Load_WithCorruptJson_FallsBackToDefaultsWithoutOverwritingFile()
    {
        const string corrupt = "{ this is not json";
        File.WriteAllText(_configPath, corrupt);
        var service = new JsonConfigService(_configPath, Localization);

        service.Load();

        Assert.Equal(25.0, service.Current.DragThreshold);
        Assert.Equal("Global", service.Current.Profiles[0].ProcessName);
        // 损坏文件保持原样，不被迫写覆盖
        Assert.Equal(corrupt, File.ReadAllText(_configPath));
    }

    [Fact]
    public void Save_PersistsChanges_ForNextLoad()
    {
        var writer = new JsonConfigService(_configPath, Localization);
        writer.Load();
        writer.Current.DragThreshold = 77.5;
        writer.Current.Profiles.Add(new WheelProfile
        {
            ProcessName = "explorer.exe",
            SectorCount = 4,
            Actions = new List<ActionItem>
            {
                new ActionItem { Type = "System", Name = "显示桌面", Parameter = "ShowDesktop" }
            }
        });

        writer.Save();

        var reader = new JsonConfigService(_configPath, Localization);
        reader.Load();
        Assert.Equal(77.5, reader.Current.DragThreshold);
        var profile = reader.Current.Profiles.Find(p => p.ProcessName == "explorer.exe");
        Assert.NotNull(profile);
        Assert.Equal("ShowDesktop", profile!.Actions[0].Parameter);
    }

    [Fact]
    public void Save_PersistsAdminAutoStartFlag_ForNextLoad()
    {
        var writer = new JsonConfigService(_configPath, Localization);
        writer.Load();
        writer.Current.AutoStartAsAdmin = true;

        writer.Save();

        var reader = new JsonConfigService(_configPath, Localization);
        reader.Load();
        Assert.True(reader.Current.AutoStartAsAdmin);
    }

    [Fact]
    public void Load_LegacyConfigWithoutAdminAutoStartFlag_TreatsAsOffAndKeepsOtherKeys()
    {
        // 旧配置没有提权自启键：按未开启处理，既有键照常读出（ADR-0041 的向后兼容硬约束）。
        File.WriteAllText(_configPath, """
            {
              "Language": "en",
              "DragThreshold": 42.0,
              "Profiles": [ { "ProcessName": "Global", "SectorCount": 8, "Actions": [] } ]
            }
            """);
        var service = new JsonConfigService(_configPath, Localization);

        service.Load();

        Assert.False(service.Current.AutoStartAsAdmin);
        Assert.Equal("en", service.Current.Language);
        Assert.Equal(42.0, service.Current.DragThreshold);
        Assert.Equal(8, Assert.Single(service.Current.Profiles).SectorCount);
    }

    [Fact]
    public void Load_ToleratesCommentsTrailingCommasAndCasing()
    {
        File.WriteAllText(_configPath, """
            {
              // 手改配置
              "dragthreshold": 33,
              "Profiles": [ { "processname": "code.exe", "sectorcount": 8, "actions": [] }, ],
            }
            """);
        var service = new JsonConfigService(_configPath, Localization);

        service.Load();

        Assert.Equal(33.0, service.Current.DragThreshold);
        Assert.Equal("code.exe", service.Current.Profiles[0].ProcessName);
        Assert.Equal(8, service.Current.Profiles[0].SectorCount);
    }

    [Fact]
    public void Load_LegacyThemeAndUiStyleKeys_MigrateToCanonicalKeysAndSaveWritesOnlyNewKeys()
    {
        File.WriteAllText(_configPath, """
            {
              "Theme": "MatchaForest",
              "UiStyle": "CleanSectors"
            }
            """);
        var service = new JsonConfigService(_configPath, Localization);

        service.Load();

        Assert.Equal("MatchaForest", service.Current.WheelPalette);
        Assert.Equal("CleanSectors", service.Current.WheelStyle);

        service.Save();

        string saved = File.ReadAllText(_configPath);
        Assert.Contains("\"WheelPalette\": \"MatchaForest\"", saved);
        Assert.Contains("\"WheelStyle\": \"CleanSectors\"", saved);
        Assert.DoesNotContain("\"Theme\"", saved);
        Assert.DoesNotContain("\"UiStyle\"", saved);
    }

    [Fact]
    public void Load_CanonicalKeysTakePrecedence_OverLegacyKeys()
    {
        File.WriteAllText(_configPath, """
            {
              "WheelPalette": "Dark",
              "Theme": "MatchaForest",
              "WheelStyle": "Glassmorphism",
              "UiStyle": "CleanSectors"
            }
            """);
        var service = new JsonConfigService(_configPath, Localization);

        service.Load();

        Assert.Equal("Dark", service.Current.WheelPalette);
        Assert.Equal("Glassmorphism", service.Current.WheelStyle);
    }

    [Fact]
    public void Load_LegacyKeysAreMatchedCaseInsensitively()
    {
        File.WriteAllText(_configPath, """
            {
              "theme": "GlacialIce",
              "uistyle": "CatPaw"
            }
            """);
        var service = new JsonConfigService(_configPath, Localization);

        service.Load();

        Assert.Equal("GlacialIce", service.Current.WheelPalette);
        Assert.Equal("CatPaw", service.Current.WheelStyle);
    }

    [Fact]
    public void GetProfileForProcess_MatchesCaseInsensitively_AndFallsBackToGlobal()
    {
        var service = new JsonConfigService(_configPath, Localization);
        service.Load();

        Assert.Equal("chrome.exe", service.GetProfileForProcess("CHROME.EXE").ProcessName);
        Assert.Equal("Global", service.GetProfileForProcess("unknown.exe").ProcessName);
        Assert.Equal("Global", service.GetProfileForProcess("").ProcessName);
    }

    [Fact]
    public void GetGlobalProfile_ReinsertsGlobalWhenMissing()
    {
        var service = new JsonConfigService(_configPath, Localization);
        service.Load();
        service.Current.Profiles.Clear();

        var global = service.GetGlobalProfile();

        Assert.Equal("Global", global.ProcessName);
        Assert.Same(global, service.Current.Profiles[0]);
    }

    [Fact]
    public void Current_NeverNull_BeforeAnyLoad()
    {
        var service = new JsonConfigService(_configPath, Localization);

        Assert.Equal(25.0, service.Current.DragThreshold);
    }

    /// <summary>
    /// 加载是替换运行态配置的入口之一，语言状态必须跟着换——用独立实例以免污染本类共享的语言服务。
    /// </summary>
    [Fact]
    public void Load_AppliesConfiguredLanguageToRuntime()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("en");
        File.WriteAllText(_configPath, """{ "Language": "ja", "Profiles": [] }""");
        var service = new JsonConfigService(_configPath, localization);

        service.Load();

        Assert.Equal("ja", localization.CurrentLanguage);
    }

    /// <summary>
    /// 导入是替换运行态配置的入口之一，语言状态必须跟着换——否则磁盘上的 Language 与运行态语言
    /// 各说各话，直到下次启动才收敛。用独立实例以免污染本类共享的语言服务。
    /// </summary>
    [Fact]
    public void Import_LanguageDiffersFromRuntime_AppliesImportedLanguage()
    {
        var localization = new LocalizationService();
        localization.SetLanguage("zh-CN");
        var service = new JsonConfigService(_configPath, localization);
        string importedPath = Path.Combine(_tempDir, "imported.json");
        File.WriteAllText(importedPath, """{ "Language": "ja", "Profiles": [] }""");

        bool imported = service.Import(importedPath);

        Assert.True(imported);
        Assert.Equal("ja", service.Current.Language);
        Assert.Equal("ja", localization.CurrentLanguage);
    }

    [Fact]
    public void Import_SourceMissing_KeepsCurrentConfigAndLanguage()
    {
        var localization = new LocalizationService();
        var service = new JsonConfigService(_configPath, localization);
        service.Load();
        string languageAfterLoad = localization.CurrentLanguage;

        bool imported = service.Import(Path.Combine(_tempDir, "absent.json"));

        Assert.False(imported);
        Assert.Equal(languageAfterLoad, localization.CurrentLanguage);
    }

    /// <summary>导出产物能被导入读回（导出与导入是同一份 JSON 格式的两端）。</summary>
    [Fact]
    public void Export_ThenImport_RoundTripsConfig()
    {
        // 接缝两侧各自都有用例，缺口只在接缝上：这条路径断了，"导出备份 → 换机导入"就整条走不通。
        var service = new JsonConfigService(_configPath, Localization);
        service.Load();
        service.Current.DragThreshold = 66.0;
        service.GetGlobalProfile().SectorCount = 12;
        string exportedPath = Path.Combine(_tempDir, "exported.json");

        bool exported = service.Export(exportedPath);

        var target = new JsonConfigService(Path.Combine(_tempDir, "target.json"), Localization);
        bool imported = target.Import(exportedPath);

        Assert.True(exported);
        Assert.True(imported);
        Assert.Equal(66.0, target.Current.DragThreshold);
        Assert.Equal(12, target.Current.Profiles.Find(profile => profile.ProcessName == "Global")!.SectorCount);
    }

    [Fact]
    public void Export_UnwritableTarget_ReturnsFalse()
    {
        var service = new JsonConfigService(_configPath, Localization);
        service.Load();

        // 目标目录不存在：导出与 Save 不同，不代建目录，失败按 false 上报。
        bool exported = service.Export(Path.Combine(_tempDir, "absent-dir", "config.json"));

        Assert.False(exported);
    }
}
