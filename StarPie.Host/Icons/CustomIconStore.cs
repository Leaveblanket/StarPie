using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using StarPie.Kernel.Configuration;
using StarPie.Services.Icons;

namespace StarPie.Icons
{
    /// <summary>
    /// 「图标资产」的自定义图标存储：目录扫描/列表缓存、导入与删除。
    /// 只做文件系统与纯数据操作（零 WPF）；位图源与图标提取在 Ui 层的图标资产服务。
    /// </summary>
    public sealed class CustomIconStore
    {
        private readonly Func<string> _appDataFolderProvider;
        private List<CustomIconItem>? _cachedCustomIcons;

        /// <summary>目录根默认取宿主内核的应用数据目录；测试可注入替代目录提供者。</summary>
        public CustomIconStore(Func<string>? appDataFolderProvider = null)
        {
            _appDataFolderProvider = appDataFolderProvider ?? AppDataPaths.GetAppDataFolder;
        }

        /// <summary>返回自定义图标目录（应用数据目录/CustomIcons），不存在时先创建。</summary>
        public string GetCustomIconsDirectory()
        {
            string dir = Path.Combine(_appDataFolderProvider(), "CustomIcons");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        /// <summary>
        /// 扫描自定义图标目录并返回全部条目（首次调用后缓存）：.svg 解析路径数据，
        /// 其余位图（png/jpg/jpeg/ico/bmp/webp）仅记录文件路径。
        /// </summary>
        public IReadOnlyList<CustomIconItem> GetCustomIcons()
        {
            if (_cachedCustomIcons != null) return _cachedCustomIcons;

            var list = new List<CustomIconItem>();
            try
            {
                string dir = GetCustomIconsDirectory();
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir);
                    foreach (var file in files)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        string key = "custom:" + fileName;

                        if (ext == ".svg")
                        {
                            try
                            {
                                string text = File.ReadAllText(file);
                                string pathData = IconCatalog.ExtractSvgPathData(text);
                                list.Add(new CustomIconItem
                                {
                                    Key = key,
                                    DisplayName = fileName,
                                    FilePath = file,
                                    SvgData = pathData
                                });
                            }
                            catch { }
                        }
                        else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".ico" || ext == ".bmp" || ext == ".webp")
                        {
                            list.Add(new CustomIconItem
                            {
                                Key = key,
                                DisplayName = fileName,
                                FilePath = file,
                                SvgData = ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GetCustomIcons Error]: {ex.Message}");
            }

            _cachedCustomIcons = list;
            return list;
        }

        /// <summary>
        /// 导入自定义图标文件到自定义图标目录：文件名清洗非法字符后追加时间戳避免重名，
        /// 复制成功后使缓存失效并返回新条目。源文件不存在或复制失败返回 null。
        /// </summary>
        public CustomIconItem? ImportCustomIcon(string sourceFilePath, string? customName = null)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                return null;

            try
            {
                string dir = GetCustomIconsDirectory();
                string ext = Path.GetExtension(sourceFilePath).ToLower();
                string safeName = string.IsNullOrWhiteSpace(customName)
                    ? Path.GetFileNameWithoutExtension(sourceFilePath)
                    : customName.Trim();

                // 把文件名中的非法字符替换为下划线，保证可安全落盘。
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c, '_');
                }

                string targetFileName = $"{safeName}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
                string targetPath = Path.Combine(dir, targetFileName);

                File.Copy(sourceFilePath, targetPath, true);

                _cachedCustomIcons = null; // 使缓存失效，下次读取时重新扫描。
                var all = GetCustomIcons();
                return all.FirstOrDefault(i => i.FilePath == targetPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportCustomIcon Error]: {ex.Message}");
                return null;
            }
        }

        /// <summary>把一段 SVG 路径数据保存为自定义 .svg 图标文件（名称清洗并追加时间戳），成功后返回新条目。</summary>
        public CustomIconItem? ImportCustomSvgData(string svgPathData, string iconName)
        {
            if (string.IsNullOrWhiteSpace(svgPathData)) return null;

            try
            {
                string dir = GetCustomIconsDirectory();
                string safeName = string.IsNullOrWhiteSpace(iconName) ? "Vector" : iconName.Trim();
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c, '_');
                }

                string targetFileName = $"{safeName}_{DateTime.Now:yyyyMMddHHmmss}.svg";
                string targetPath = Path.Combine(dir, targetFileName);

                File.WriteAllText(targetPath, svgPathData.Trim());

                _cachedCustomIcons = null; // 使缓存失效。
                var all = GetCustomIcons();
                return all.FirstOrDefault(i => i.FilePath == targetPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImportCustomSvgData Error]: {ex.Message}");
                return null;
            }
        }

        /// <summary>按键删除自定义图标（同时删除磁盘文件并使缓存失效）；未找到或删除失败返回 false。</summary>
        public bool DeleteCustomIcon(string key)
        {
            try
            {
                var item = GetCustomIcons().FirstOrDefault(i => i.Key == key);
                if (item != null && File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                    _cachedCustomIcons = null;
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
