using System;
using SharpHook.Data;

namespace StarPie.Ui
{
    /// <summary>
    /// 测试实例的触发键覆盖：命令行 <c>--trigger-button=&lt;n&gt;</c> 令本次实例的轮盘触发键取
    /// <see cref="MouseButton"/> 的第 n 个按键（n 取 1–5），供 e2e 以鼠标侧键驱动轮盘交互链路。
    /// </summary>
    /// <remarks>
    /// ADR-0052 的「触发键在栈内参数化（默认右键，不暴露配置面与 UI）」不变：本入口只对声明了
    /// 测试实例的进程生效（与单实例闸门绕过、退出消息受理同一标记），既不写配置、也不出现在界面上，
    /// 非测试实例、缺开关或取值非法时一律取默认右键。
    /// 侧键（<see cref="MouseButton.Button4"/>／<see cref="MouseButton.Button5"/>，即 Windows 的
    /// XBUTTON1／XBUTTON2）作 e2e 观察面的价值：未被抑制时不弹出上下文菜单，轮盘交互链路的外部观测
    /// 不必先收菜单再判定（见 tests/mouse_input.py）。
    /// </remarks>
    public static class TestInstanceSwitches
    {
        /// <summary>命令行开关前缀（取值紧跟等号）。</summary>
        private const string SwitchPrefix = "--trigger-button=";

        /// <summary>默认触发键（右键）；与 <c>MouseInputHook</c> 的默认实参同值。</summary>
        public const MouseButton DefaultButton = MouseButton.Button2;

        /// <summary>
        /// 命令行是否声明了测试实例（e2e 冷启动绕过单实例闸门、受理测试实例退出消息的同一标记）。
        /// 这是该标记的唯一解析处——单实例闸门与触发键覆盖共用一份判定，避免两处字符串漂移。
        /// </summary>
        public static bool IsTestInstance(string? commandLine) =>
            !string.IsNullOrEmpty(commandLine) &&
            (commandLine.Contains("--allow-multiple", StringComparison.OrdinalIgnoreCase) ||
             commandLine.Contains("--test-instance", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// 解析生效的触发键：非测试实例、缺 <c>--trigger-button</c> 或取值非法时回退
        /// <see cref="DefaultButton"/>。合法范围为 Button1–Button5（NoButton 不构成轮盘交互）。
        /// </summary>
        /// <param name="commandLine">完整命令行（<see cref="Environment.CommandLine"/>）。</param>
        public static MouseButton Resolve(string? commandLine)
        {
            if (!IsTestInstance(commandLine)) return DefaultButton;

            int at = commandLine!.IndexOf(SwitchPrefix, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return DefaultButton;

            int start = at + SwitchPrefix.Length;
            int end = start;
            while (end < commandLine.Length && char.IsAsciiDigit(commandLine[end]))
            {
                end++;
            }

            if (end == start) return DefaultButton;
            if (!int.TryParse(commandLine.AsSpan(start, end - start), out int number)) return DefaultButton;

            return number is >= 1 and <= 5 ? (MouseButton)number : DefaultButton;
        }
    }
}
