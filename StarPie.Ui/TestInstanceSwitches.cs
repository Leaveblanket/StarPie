using System;
using SharpHook.Data;

namespace StarPie.Ui
{
    /// <summary>
    /// 测试实例的触发键覆盖：命令行 <c>--trigger-button=&lt;n&gt;</c> 令本次实例的轮盘触发键取
    /// <see cref="MouseButton"/> 的第 n 个按键（n 取 1–5），供 e2e 以鼠标侧键驱动轮盘手势链路。
    /// </summary>
    /// <remarks>
    /// 正式触发键已开放配置面（运行态 config.json 的 <c>TriggerButton</c>，ADR-0056）；测试实例覆盖
    /// 高于配置：声明了测试实例且带合法开关时无视配置直取覆盖，组合根把它折叠进捕获侧的触发键
    /// 实时读数（见 <c>WheelGestureContributor</c>）。非测试实例、缺开关或取值非法时返回 null——
    /// 触发键完全由运行态配置决定（配置缺键或非法值回退右键，见 <c>TriggerButtonParser</c>）。
    /// 侧键（<see cref="MouseButton.Button4"/>／<see cref="MouseButton.Button5"/>，即 Windows 的
    /// XBUTTON1／XBUTTON2）作 e2e 观察面的价值：未被抑制时不弹出上下文菜单，轮盘手势链路的外部观测
    /// 不必先收菜单再判定（见 tests/mouse_input.py）。
    /// </remarks>
    public static class TestInstanceSwitches
    {
        /// <summary>命令行开关前缀（取值紧跟等号）。</summary>
        private const string SwitchPrefix = "--trigger-button=";

        /// <summary>
        /// 命令行是否声明了测试实例（e2e 冷启动绕过单实例闸门、受理测试实例退出消息的同一标记）。
        /// 这是该标记的唯一解析处——单实例闸门与触发键覆盖共用一份判定，避免两处字符串漂移。
        /// </summary>
        public static bool IsTestInstance(string? commandLine) =>
            !string.IsNullOrEmpty(commandLine) &&
            (commandLine.Contains("--allow-multiple", StringComparison.OrdinalIgnoreCase) ||
             commandLine.Contains("--test-instance", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 解析测试实例的触发键覆盖：非测试实例、缺 <c>--trigger-button</c> 或取值非法时返回
        /// null（触发键交由运行态配置决定）。合法范围为 Button1–Button5（NoButton 不构成轮盘手势）。
        /// </summary>
        /// <param name="commandLine">完整命令行（<see cref="Environment.CommandLine"/>）。</param>
        public static MouseButton? Resolve(string? commandLine)
        {
            if (!IsTestInstance(commandLine)) return null;

            int at = commandLine!.IndexOf(SwitchPrefix, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return null;

            int start = at + SwitchPrefix.Length;
            int end = start;
            while (end < commandLine.Length && char.IsAsciiDigit(commandLine[end]))
            {
                end++;
            }

            if (end == start) return null;
            if (!int.TryParse(commandLine.AsSpan(start, end - start), out int number)) return null;

            return number is >= 1 and <= 5 ? (MouseButton)number : null;
        }
    }
}
