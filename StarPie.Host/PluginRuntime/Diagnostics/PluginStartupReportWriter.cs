using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarPie.PluginRuntime.Diagnostics
{
    /// <summary>
    /// 启动报告落盘：把 <see cref="PluginStartupReport"/> 写为缩进 JSON，供诊断与问题定位。
    /// </summary>
    /// <remarks>报告是诊断产物，写入失败不抛异常、不阻断启动，只输出 Debug 日志。</remarks>
    public sealed class PluginStartupReportWriter
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        };

        /// <summary>构造写盘器：路径经构造函数注入。</summary>
        public PluginStartupReportWriter(string reportPath)
        {
            ReportPath = reportPath;
        }

        /// <summary>报告文件路径。</summary>
        public string ReportPath { get; }

        /// <summary>写出报告（每次启动覆写）；目录不存在时先创建。</summary>
        public void Write(PluginStartupReport report)
        {
            try
            {
                string? directory = Path.GetDirectoryName(ReportPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, Options));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write plugin startup report: {ex.Message}");
            }
        }
    }
}
