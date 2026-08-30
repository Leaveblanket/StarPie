using WinPieGestures;

namespace WinPieGestures.Tests;

/// <summary>
/// 程序目录纯规则函数的覆盖 (T06, ADR-0004)：垃圾可执行过滤（卸载器/安装器/更新器/诊断/
/// 内嵌框架/Python 内部脚本/文档网页）、跨源去重、显示名升级与搜索过滤。全部无 IO。
/// </summary>
public sealed class ProgramCatalogTests
{
    private static ProgramEntry Entry(string name, string path, string? friendly = null)
        => new(name, path, friendly ?? path, IconSource: null);

    // --- 垃圾可执行过滤 ---------------------------------------------------------

    [Theory]
    [InlineData("MyApp", @"C:\Apps\MyApp\uninstall.exe")]
    [InlineData("MyApp Uninstaller", @"C:\Apps\MyApp\app.exe")]
    [InlineData("MyApp", @"C:\Apps\MyApp\unins000.exe")]
    [InlineData("某软件卸载程序", @"C:\Apps\MyApp\un.exe")]
    [InlineData("MyApp", @"C:\Apps\MyApp\setup.exe")]
    [InlineData("VC Redistributable", @"C:\Apps\vc_redist.x64.exe")]
    [InlineData("Installer", @"C:\Apps\msiexec.exe")]
    [InlineData("MyApp", @"C:\Apps\MyApp\MyAppUpdater.exe")]
    [InlineData("MyApp", @"C:\Apps\MyApp\crashpad_handler.exe")]
    [InlineData("意见反馈", @"C:\Apps\MyApp\feedback.exe")]
    [InlineData("诊断工具", @"C:\Apps\MyApp\diagnostic.exe")]
    [InlineData("FFmpeg", @"C:\Tools\ffmpeg.exe")]
    [InlineData("Conhost", @"C:\Windows\System32\conhost.exe")]
    [InlineData("pip", @"C:\Python312\Scripts\pip.exe")]
    [InlineData("readme", @"C:\Apps\MyApp\readme.exe")]
    [InlineData("帮助文档", @"C:\Apps\MyApp\help.exe")]
    public void IsJunkExecutable_JunkHelpers_AreFiltered(string displayName, string exePath)
    {
        Assert.True(ProgramCatalog.IsJunkExecutable(displayName, exePath));
    }

    [Theory]
    [InlineData(@"C:\Apps\Electron\resources\app.exe")]
    [InlineData(@"C:\Apps\Tool\node_modules\esbuild.exe")]
    [InlineData(@"C:\Users\x\AppData\Local\Temp\tmp123.exe")]
    [InlineData(@"C:\Users\x\AppData\Local\cache\wheel.exe")]
    public void IsJunkExecutable_EmbeddedFrameworkPaths_AreFiltered(string exePath)
    {
        Assert.True(ProgramCatalog.IsJunkExecutable("App", exePath));
    }

    [Theory]
    [InlineData("Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe")]
    [InlineData("Python", @"C:\Python312\python.exe")]
    [InlineData("Python", @"C:\Python312\Lib\venv\pythonw.exe")] // \lib\ 命中 Python 规则，pythonw.exe 获豁免
    [InlineData("记事本", @"C:\Windows\notepad.exe")]
    [InlineData("VS Code", @"C:\Users\x\AppData\Local\Programs\Microsoft VS Code\Code.exe")]
    public void IsJunkExecutable_RealPrograms_Pass(string displayName, string exePath)
    {
        Assert.False(ProgramCatalog.IsJunkExecutable(displayName, exePath));
    }

    // --- 显示名升级 ---------------------------------------------------------------

    [Fact]
    public void ShouldUpgradeDisplayName_RawExeNameFirst_RicherCandidateWins()
    {
        Assert.True(ProgramCatalog.ShouldUpgradeDisplayName("chrome", "Google Chrome", @"C:\Apps\chrome.exe"));
    }

    [Fact]
    public void ShouldUpgradeDisplayName_RichNameFirst_RawCandidateLoses()
    {
        Assert.False(ProgramCatalog.ShouldUpgradeDisplayName("Google Chrome", "chrome", @"C:\Apps\chrome.exe"));
    }

    [Fact]
    public void ShouldUpgradeDisplayName_BothRaw_NoUpgrade()
    {
        Assert.False(ProgramCatalog.ShouldUpgradeDisplayName("chrome", "CHROME", @"C:\Apps\chrome.exe"));
    }

    [Fact]
    public void ShouldUpgradeDisplayName_CurrentNotRawName_NoUpgrade()
    {
        Assert.False(ProgramCatalog.ShouldUpgradeDisplayName("某干净的显示名", "Google Chrome", @"C:\Apps\chrome.exe"));
    }

    // --- 跨源去重 -----------------------------------------------------------------

    [Fact]
    public void MergeSources_SamePathDifferentCase_MergedIntoOne()
    {
        var merged = ProgramCatalog.MergeSources(new[]
        {
            Entry("chrome", @"C:\Apps\chrome.exe"),
            Entry("Google Chrome", @"C:\APPS\CHROME.EXE")
        });

        var entry = Assert.Single(merged);
        Assert.Equal("Google Chrome", entry.Name);
    }

    [Fact]
    public void MergeSources_RawThenRawThenRich_UpgradesAtRichCandidate()
    {
        var merged = ProgramCatalog.MergeSources(new[]
        {
            Entry("code", @"C:\Apps\code.exe"),
            Entry("CODE", @"C:\Apps\code.exe"),
            Entry("Visual Studio Code", @"C:\Apps\code.exe")
        });

        var entry = Assert.Single(merged);
        Assert.Equal("Visual Studio Code", entry.Name);
    }

    [Fact]
    public void MergeSources_RichThenRaw_KeepsRichName()
    {
        var merged = ProgramCatalog.MergeSources(new[]
        {
            Entry("Google Chrome", @"C:\Apps\chrome.exe"),
            Entry("chrome", @"C:\Apps\chrome.exe")
        });

        var entry = Assert.Single(merged);
        Assert.Equal("Google Chrome", entry.Name);
    }

    [Fact]
    public void MergeSources_DifferentPaths_KeptInFirstSeenOrder()
    {
        var merged = ProgramCatalog.MergeSources(new[]
        {
            Entry("A", @"C:\Apps\a.exe"),
            Entry("B", @"C:\Apps\b.exe"),
            Entry("a", @"C:\Apps\a.exe") // 同路径再来一个裸 exe 名：不升级也不覆盖
        });

        Assert.Equal(2, merged.Count);
        Assert.Equal(@"C:\Apps\a.exe", merged[0].Path);
        Assert.Equal("A", merged[0].Name);
        Assert.Equal("B", merged[1].Name);
    }

    // --- 搜索过滤 -----------------------------------------------------------------

    [Fact]
    public void FilterPrograms_EmptyFilter_ReturnsAllInOrder()
    {
        var programs = new ProgramEntry[] { Entry("A", @"C:\a.exe"), Entry("B", @"C:\b.exe") };

        Assert.Equal(2, ProgramCatalog.FilterPrograms(programs, null).Count);
        Assert.Equal(2, ProgramCatalog.FilterPrograms(programs, "").Count);
    }

    [Fact]
    public void FilterPrograms_MatchesNamePathOrExeFile_CaseInsensitive()
    {
        var programs = new[]
        {
            Entry("VS Code", @"C:\Programs\Microsoft VS Code\Code.exe"),
            Entry("Google Chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe"),
            Entry("记事本", @"C:\Windows\notepad.exe")
        };

        // 显示名（忽略大小写 + 首尾空白）
        Assert.Single(ProgramCatalog.FilterPrograms(programs, " vs code "));
        // exe 文件名
        Assert.Single(ProgramCatalog.FilterPrograms(programs, "CHROME.EXE"));
        // 友好路径片段
        Assert.Single(ProgramCatalog.FilterPrograms(programs, @"program files\google"));
        // 无命中
        Assert.Empty(ProgramCatalog.FilterPrograms(programs, "firefox"));
    }
}
