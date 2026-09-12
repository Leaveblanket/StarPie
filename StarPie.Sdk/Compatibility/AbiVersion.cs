namespace StarPie.Compatibility
{
    /// <summary>
    /// ABI 版本串（"主.次"）的解析：headless 与 WPF 两条 ABI 政策共用同一文本形态判定。
    /// </summary>
    /// <remarks>版本号本身各自独立——两条政策分别持有自己的主/次常量，共用的只是「主.次」文本形态。</remarks>
    public static class AbiVersion
    {
        /// <summary>解析 "主.次" 形态的版本串；格式不合法返回 false（不抛异常，失败时输出参数归零）。</summary>
        public static bool TryParse(string? text, out int major, out int minor)
        {
            major = 0;
            minor = 0;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] parts = text.Trim().Split('.');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out major)
                || !int.TryParse(parts[1], out minor)
                || major < 0
                || minor < 0)
            {
                major = 0;
                minor = 0;
                return false;
            }

            return true;
        }
    }
}
