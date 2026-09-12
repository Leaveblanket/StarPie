using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarPie.PluginRuntime.State
{
    /// <summary>
    /// 宿主插件状态存储：独占 <c>plugin-state.json</c> 的读写。
    /// </summary>
    /// <remarks>
    /// 宿主唯一权威，插件不可读写。加载语义：文件缺失即空状态（开发者模式关闭、无条目）；
    /// JSON 损坏时回退空状态、不覆盖原文件，交由后续保存重建。首次读取当前状态时自动加载一次
    /// （调用方无需先 <see cref="Load"/>，避免在启动扫描前用空文档覆盖已有状态）。
    /// 保存失败只输出 Debug 日志、不抛异常——状态文件损坏不应阻断启动。
    /// </remarks>
    public sealed class PluginStateStore
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters = { new JsonStringEnumConverter() },
        };

        private PluginStateDocument _document = new();
        private bool _loaded;

        /// <summary>构造存储：路径经构造函数注入，生产路径由组合根经 <see cref="PluginPaths"/> 计算。</summary>
        public PluginStateStore(string statePath)
        {
            StatePath = statePath;
        }

        /// <summary>状态文件路径。</summary>
        public string StatePath { get; }

        /// <summary>当前宿主状态；首次读取时自动加载一次，此后读内存态直到显式 <see cref="Load"/>。</summary>
        public PluginStateDocument Current
        {
            get
            {
                if (!_loaded)
                {
                    Load();
                }

                return _document;
            }
        }

        /// <summary>从磁盘加载状态；文件缺失或损坏均回退空状态，不向调用方抛异常。</summary>
        public void Load()
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    _document = new PluginStateDocument();
                    _loaded = true;
                    return;
                }

                string json = File.ReadAllText(StatePath);
                _document = JsonSerializer.Deserialize<PluginStateDocument>(json, Options) ?? new PluginStateDocument();
                Normalize(_document);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load plugin state: {ex.Message}");
                _document = new PluginStateDocument();
            }

            _loaded = true;
        }

        /// <summary>把当前状态写回磁盘；目录不存在时先创建，失败只输出 Debug 日志。</summary>
        public void Save()
        {
            try
            {
                string? directory = Path.GetDirectoryName(StatePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(StatePath, JsonSerializer.Serialize(_document, Options));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save plugin state: {ex.Message}");
            }
        }

        /// <summary>反序列化后的防御性归一：字典与条目集合不因手改文件而出现 null。</summary>
        private static void Normalize(PluginStateDocument document)
        {
            document.Plugins ??= new Dictionary<string, PluginStateEntry>(StringComparer.Ordinal);
        }
    }
}
