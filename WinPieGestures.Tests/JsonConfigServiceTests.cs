using System;
using System.Collections.Generic;
using System.IO;
using WinPieGestures;

namespace WinPieGestures.Tests;

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
}
