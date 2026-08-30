using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace WinPieGestures
{
    public partial class ProgramPickerWindow : Window
    {
        public class ProgramItem
        {
            public string Name { get; set; } = "";
            public string Path { get; set; } = "";
            public string FriendlyPath { get; set; } = "";
            public BitmapSource? IconSource { get; set; }
        }

        private readonly List<ProgramItem> _allPrograms = new List<ProgramItem>();
        private readonly ObservableCollection<ProgramItem> _displayedPrograms = new ObservableCollection<ProgramItem>();

        public string SelectedPath { get; private set; } = "";
        public string SelectedName { get; private set; } = "";

        public ProgramPickerWindow(IThemeService themeService)
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentEffectiveTheme);
            ProgramsListView.ItemsSource = _displayedPrograms;
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            this.Title = $"{I18n.T("ProgramPickerTitle")} - StarPie";
            if (HeaderTitleText != null) HeaderTitleText.Text = I18n.T("ProgramPickerHeader");
            if (SearchPlaceholderText != null) SearchPlaceholderText.Text = I18n.T("ProgramPickerPlaceholder");
            if (StatusTextBlock != null) StatusTextBlock.Text = I18n.T("ProgramPickerScanning");
            if (ManualBrowseButton != null) ManualBrowseButton.Content = I18n.T("BtnManualBrowse");
            if (OkButton != null) OkButton.Content = I18n.T("BtnConfirm");
            if (CancelButton != null) CancelButton.Content = I18n.T("BtnCancel");
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            StatusTextBlock.Visibility = Visibility.Visible;
            StatusTextBlock.Text = I18n.T("ProgramPickerScanning");

            try
            {
                var programs = await Task.Run(() => ScanInstalledPrograms());
                _allPrograms.Clear();
                _allPrograms.AddRange(programs);

                // Update UI on UI thread
                UpdateDisplayedList("");
                StatusTextBlock.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"{I18n.T("Error")}: {ex.Message}";
                StatusTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private List<ProgramItem> ScanInstalledPrograms()
        {
            var dict = new Dictionary<string, ProgramItem>(StringComparer.OrdinalIgnoreCase);

            // 1. Built-in Windows System Tools
            AddSystemApps(dict);

            // 2. Start Menu Shortcuts (Common & User)
            ScanStartMenuShortcuts(dict);

            // 3. Desktop Shortcuts (Common & User)
            ScanDesktopShortcuts(dict);

            // 4. User AppData Local Programs (VS Code, Discord, Spotify, Xmind, etc.)
            ScanUserAppDataPrograms(dict);

            // 5. Windows Apps (Windows 10/11 UWP / Store Tools)
            ScanWindowsApps(dict);

            // 6. Registry App Paths (64-bit & 32-bit HKLM, HKCU)
            ScanRegistryAppPaths(dict);

            // 7. Registry Uninstall Entries (64-bit & 32-bit HKLM, HKCU)
            ScanRegistryUninstall(dict);

            // 8. Program Files Directory Scanning (Top-level application suites)
            ScanProgramFilesTopLevel(dict);

            var list = dict.Values.ToList();
            // Clean natural sort by Display Name
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            return list;
        }

        private static bool IsJunkOrHelperExecutable(string displayName, string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return true;

            if (!exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                var fileInfo = new FileInfo(exePath);
                if (fileInfo.Length == 0) return true;
            }
            catch
            {
                return true;
            }

            string fileName = Path.GetFileName(exePath).ToLowerInvariant();
            string lowerName = displayName.ToLowerInvariant();
            string combined = $"{lowerName} {fileName}";
            string lowerPath = exePath.ToLowerInvariant();

            // 1. Uninstallers
            if (combined.Contains("uninstall") || combined.Contains("unins000") || combined.Contains("unins001") ||
                combined.Contains("uninst") || combined.Contains("卸载") || combined.Contains("remove") ||
                combined.Contains("deleter") || combined.Contains("cleanup"))
                return true;

            // 2. Installers & Setup Wizards & Redistributables
            if (combined.Contains("setup") || combined.Contains("installer") || combined.Contains("install_helper") ||
                combined.Contains("msiexec") || combined.Contains("vcredist") || combined.Contains("dxsetup") ||
                combined.Contains("dotnetfx") || combined.Contains("ndp4") || combined.Contains("vc_redist") ||
                combined.Contains("setup_helper") || combined.Contains("dpinst"))
                return true;

            // 3. Updaters, Crash Reporters & Feedbacks
            if (combined.Contains("update") || combined.Contains("updater") || combined.Contains("autoupdate") ||
                combined.Contains("patcher") || combined.Contains("crashpad") || combined.Contains("crash_report") ||
                combined.Contains("crashreporter") || combined.Contains("feedback") || combined.Contains("意见反馈") ||
                combined.Contains("bugreport"))
                return true;

            // 4. Diagnostics, Fixers & CLI Helpers
            if (combined.Contains("diagnostic") || combined.Contains("repair") || combined.Contains("修复") ||
                combined.Contains("fix") || combined.Contains("troubleshoot") || combined.Contains("elevate") ||
                combined.Contains("helper") || combined.Contains("launcher_helper") || combined.Contains("nwjc") ||
                combined.Contains("chromedriver") || combined.Contains("geckodriver") || combined.Contains("phantomjs") ||
                combined.Contains("conhost") || combined.Contains("ffmpeg") || combined.Contains("ffprobe") ||
                combined.Contains("winpty") || combined.Contains("openconsole") || combined.Contains("rcedit") ||
                combined.Contains("language_server") || combined.Contains("webm_encoder") || combined.Contains("compil32") ||
                combined.Contains("iscc") || combined.Contains("islzma") || combined.Contains("iediag"))
                return true;

            // 5. Internal embedded frameworks & node packages
            if (lowerPath.Contains("\\resources\\") || lowerPath.Contains("\\node_modules\\") ||
                lowerPath.Contains("\\extensions\\") || lowerPath.Contains("\\site-packages\\") ||
                lowerPath.Contains("\\packages\\") || lowerPath.Contains("\\internal\\") ||
                lowerPath.Contains("\\temp\\") || lowerPath.Contains("\\tmp\\") ||
                lowerPath.Contains("\\cache\\") || lowerPath.Contains("\\plugins\\") ||
                lowerPath.Contains("\\sdk\\") || lowerPath.Contains("\\tcl\\") || lowerPath.Contains("\\scripts\\"))
                return true;

            // 6. Python internal scripts
            if (lowerPath.Contains("python") && (lowerPath.Contains("\\scripts\\") || lowerPath.Contains("\\site-packages\\") || lowerPath.Contains("\\tcl\\") || lowerPath.Contains("\\lib\\")))
            {
                if (fileName != "python.exe" && fileName != "pythonw.exe")
                    return true;
            }

            // 7. Docs & web shortcuts
            if (combined.Contains("readme") || combined.Contains("license") || combined.Contains("changelog") ||
                combined.Contains("manual") || combined.Contains("使用说明") || combined.Contains("用户手册") ||
                combined.Contains("help") || combined.Contains("帮助") || combined.Contains("website") ||
                combined.Contains("官方网站") || combined.Contains("访问官网") || combined.Contains("homepage") ||
                combined.Contains("forum") || combined.Contains("bbs"))
                return true;

            return false;
        }

        private void AddProgramEntry(Dictionary<string, ProgramItem> dict, string displayName, string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return;

            string normalizedPath;
            try
            {
                normalizedPath = Path.GetFullPath(exePath);
            }
            catch
            {
                normalizedPath = exePath;
            }

            if (IsJunkOrHelperExecutable(displayName, normalizedPath))
                return;

            if (dict.TryGetValue(normalizedPath, out var existing))
            {
                // Upgrade display name if the new name is richer (not just a raw exe name)
                string rawExeName = Path.GetFileNameWithoutExtension(normalizedPath);
                if (string.Equals(existing.Name, rawExeName, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(displayName, rawExeName, StringComparison.OrdinalIgnoreCase))
                {
                    existing.Name = displayName;
                }
                return;
            }

            var icon = IconHelper.GetIcon(normalizedPath);
            dict[normalizedPath] = new ProgramItem
            {
                Name = displayName,
                Path = normalizedPath,
                FriendlyPath = normalizedPath,
                IconSource = icon
            };
        }

        private void AddSystemApps(Dictionary<string, ProgramItem> dict)
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);

            AddProgramEntry(dict, "文件资源管理器 (Explorer)", Path.Combine(winDir, "explorer.exe"));
            AddProgramEntry(dict, "记事本 (Notepad)", Path.Combine(sysDir, "notepad.exe"));
            AddProgramEntry(dict, "任务管理器 (Taskmgr)", Path.Combine(sysDir, "taskmgr.exe"));
            AddProgramEntry(dict, "计算器 (Calculator)", Path.Combine(sysDir, "calc.exe"));
            AddProgramEntry(dict, "截图工具 (SnippingTool)", Path.Combine(sysDir, "SnippingTool.exe"));
            AddProgramEntry(dict, "命令提示符 (CMD)", Path.Combine(sysDir, "cmd.exe"));
            AddProgramEntry(dict, "Windows PowerShell", Path.Combine(sysDir, @"WindowsPowerShell\v1.0\powershell.exe"));
            AddProgramEntry(dict, "画图 (MSPaint)", Path.Combine(sysDir, "mspaint.exe"));
            AddProgramEntry(dict, "注册表编辑器 (Regedit)", Path.Combine(winDir, "regedit.exe"));
            AddProgramEntry(dict, "控制面板 (Control Panel)", Path.Combine(sysDir, "control.exe"));
        }

        private void ScanStartMenuShortcuts(Dictionary<string, ProgramItem> dict)
        {
            var searchDirs = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs")
            };

            foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    var files = Directory.GetFiles(dir, "*.lnk", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);

                        // Resolve target path and reject dead shortcuts
                        if (IconHelper.ResolveShortcutTarget(file, out string targetPath, out _, out _))
                        {
                            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath) && targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                AddProgramEntry(dict, name, targetPath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to scan Start Menu in {dir}: {ex.Message}");
                }
            }
        }

        private void ScanDesktopShortcuts(Dictionary<string, ProgramItem> dict)
        {
            var searchDirs = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            foreach (var dir in searchDirs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(dir)) continue;

                try
                {
                    var files = Directory.GetFiles(dir, "*.lnk", SearchOption.TopDirectoryOnly);
                    foreach (string file in files)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);

                        if (IconHelper.ResolveShortcutTarget(file, out string targetPath, out _, out _))
                        {
                            if (!string.IsNullOrEmpty(targetPath) && File.Exists(targetPath) && targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                AddProgramEntry(dict, name, targetPath);
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private void ScanUserAppDataPrograms(Dictionary<string, ProgramItem> dict)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string localPrograms = Path.Combine(localAppData, "Programs");
                if (Directory.Exists(localPrograms))
                {
                    foreach (string appDir in Directory.GetDirectories(localPrograms))
                    {
                        string appName = Path.GetFileName(appDir);
                        try
                        {
                            // Search top level of the app directory
                            foreach (string exe in Directory.GetFiles(appDir, "*.exe", SearchOption.TopDirectoryOnly))
                            {
                                string displayName = string.Equals(Path.GetFileNameWithoutExtension(exe), appName, StringComparison.OrdinalIgnoreCase)
                                    ? appName
                                    : $"{appName} ({Path.GetFileNameWithoutExtension(exe)})";

                                AddProgramEntry(dict, displayName, exe);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private void ScanWindowsApps(Dictionary<string, ProgramItem> dict)
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string winApps = Path.Combine(localAppData, @"Microsoft\WindowsApps");
                if (Directory.Exists(winApps))
                {
                    foreach (string exe in Directory.GetFiles(winApps, "*.exe", SearchOption.TopDirectoryOnly))
                    {
                        string name = Path.GetFileNameWithoutExtension(exe);
                        AddProgramEntry(dict, name, exe);
                    }
                }
            }
            catch { }
        }

        private void ScanRegistryAppPaths(Dictionary<string, ProgramItem> dict)
        {
            var hives = new[]
            {
                (RegistryHive.LocalMachine, RegistryView.Registry64),
                (RegistryHive.LocalMachine, RegistryView.Registry32),
                (RegistryHive.CurrentUser, RegistryView.Default)
            };

            foreach (var (hive, view) in hives)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var appPaths = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                    if (appPaths == null) continue;

                    foreach (string subKeyName in appPaths.GetSubKeyNames())
                    {
                        try
                        {
                            using var key = appPaths.OpenSubKey(subKeyName);
                            string? defaultVal = key?.GetValue("")?.ToString();
                            if (string.IsNullOrEmpty(defaultVal)) continue;

                            string exePath = Environment.ExpandEnvironmentVariables(defaultVal.Trim().Trim('"'));
                            if (!File.Exists(exePath)) continue;

                            string name = Path.GetFileNameWithoutExtension(subKeyName);
                            AddProgramEntry(dict, name, exePath);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private void ScanRegistryUninstall(Dictionary<string, ProgramItem> dict)
        {
            var hives = new[]
            {
                (RegistryHive.LocalMachine, RegistryView.Registry64),
                (RegistryHive.LocalMachine, RegistryView.Registry32),
                (RegistryHive.CurrentUser, RegistryView.Default)
            };

            foreach (var (hive, view) in hives)
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                    if (uninstall == null) continue;

                    foreach (string subKeyName in uninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using var key = uninstall.OpenSubKey(subKeyName);
                            if (key == null) continue;

                            // Skip system components and updates
                            object? sysComponent = key.GetValue("SystemComponent");
                            if (sysComponent is int sc && sc == 1) continue;
                            if (key.GetValue("ParentKeyName") != null) continue;

                            string? displayName = key.GetValue("DisplayName")?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(displayName)) continue;

                            // Skip Windows security updates and redistributables
                            if (displayName.StartsWith("KB", StringComparison.OrdinalIgnoreCase) ||
                                displayName.StartsWith("Security Update", StringComparison.OrdinalIgnoreCase) ||
                                displayName.StartsWith("Microsoft Visual C++", StringComparison.OrdinalIgnoreCase) ||
                                displayName.StartsWith("Windows Software Development Kit", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string? displayIcon = key.GetValue("DisplayIcon")?.ToString();
                            string? installLocation = key.GetValue("InstallLocation")?.ToString();

                            string exePath = "";
                            if (!string.IsNullOrEmpty(displayIcon))
                            {
                                string raw = displayIcon.Split(',')[0].Trim().Trim('"');
                                string expanded = Environment.ExpandEnvironmentVariables(raw);
                                if (File.Exists(expanded) && expanded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    exePath = expanded;
                                }
                            }

                            if (string.IsNullOrEmpty(exePath) && !string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                            {
                                try
                                {
                                    var exes = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                                    var mainExe = exes.FirstOrDefault(e => !IsJunkOrHelperExecutable(displayName, e));
                                    if (mainExe != null) exePath = mainExe;
                                }
                                catch { }
                            }

                            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                            {
                                AddProgramEntry(dict, displayName, exePath);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private void ScanProgramFilesTopLevel(Dictionary<string, ProgramItem> dict)
        {
            var programFilesDirs = new List<string>();
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (Directory.Exists(pf)) programFilesDirs.Add(pf);
            if (Directory.Exists(pf86) && !string.Equals(pf, pf86, StringComparison.OrdinalIgnoreCase)) programFilesDirs.Add(pf86);

            foreach (var rootPf in programFilesDirs)
            {
                try
                {
                    foreach (var vendorDir in Directory.GetDirectories(rootPf))
                    {
                        string vendorName = Path.GetFileName(vendorDir);
                        if (vendorName.Equals("Common Files", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows Defender", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows Mail", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows Media Player", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows NT", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("Windows Photo Viewer", StringComparison.OrdinalIgnoreCase) ||
                            vendorName.Equals("WindowsPowerShell", StringComparison.OrdinalIgnoreCase))
                            continue;

                        // Check top level
                        try
                        {
                            foreach (var exe in Directory.GetFiles(vendorDir, "*.exe", SearchOption.TopDirectoryOnly))
                            {
                                AddProgramEntry(dict, $"{vendorName} ({Path.GetFileNameWithoutExtension(exe)})", exe);
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        private void UpdateDisplayedList(string filter)
        {
            _displayedPrograms.Clear();
            var query = _allPrograms.AsEnumerable();

            if (!string.IsNullOrEmpty(filter))
            {
                string lowerFilter = filter.Trim().ToLowerInvariant();
                query = query.Where(p =>
                    p.Name.ToLowerInvariant().Contains(lowerFilter) ||
                    p.FriendlyPath.ToLowerInvariant().Contains(lowerFilter) ||
                    Path.GetFileName(p.Path).ToLowerInvariant().Contains(lowerFilter));
            }

            foreach (var item in query)
            {
                _displayedPrograms.Add(item);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = SearchTextBox.Text;
            if (SearchPlaceholderText != null)
            {
                SearchPlaceholderText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateDisplayedList(text);
        }

        private void ProgramsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectAndClose();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            SelectAndClose();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ManualBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "可执行程序 (*.exe)|*.exe|快捷方式 (*.lnk)|*.lnk|所有文件 (*.*)|*.*",
                Title = I18n.T("BtnBrowseApp")
            };

            if (openFileDialog.ShowDialog(this) == true)
            {
                string chosenFile = openFileDialog.FileName;
                if (chosenFile.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                    IconHelper.ResolveShortcutTarget(chosenFile, out string targetPath, out _, out _) &&
                    !string.IsNullOrEmpty(targetPath) && File.Exists(targetPath))
                {
                    SelectedPath = targetPath;
                    SelectedName = Path.GetFileNameWithoutExtension(chosenFile);
                }
                else
                {
                    SelectedPath = chosenFile;
                    SelectedName = Path.GetFileNameWithoutExtension(chosenFile);
                }

                DialogResult = true;
                Close();
            }
        }

        private void SelectAndClose()
        {
            var selected = ProgramsListView.SelectedItem as ProgramItem;
            if (selected != null)
            {
                SelectedPath = selected.Path;
                SelectedName = selected.Name;
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("请选择一个程序，或者点击“手动浏览文件...”", "未选择", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
