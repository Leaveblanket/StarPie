using SharpHook.Data;
using StarPie.Sdk.Models;

namespace StarPie.Ui.Services.Input
{
    /// <summary>
    /// 配置键名 → SharpHook 鼠标键的解析：捕获侧唯一认得 <see cref="TriggerButtonNames"/> 词表的地方。
    /// 缺键与未知取值同语义，一律回退默认右键；解析绝不抛异常——它运行在钩子线程的
    /// 每个触发键按下 / 抬起事件上。
    /// </summary>
    public static class TriggerButtonParser
    {
        /// <summary>按词表解析键名（序数忽略大小写、容忍首尾空白）；未知取值返回 false 且按钮为右键。</summary>
        public static bool TryParse(string? name, out MouseButton button)
        {
            switch (name?.Trim().ToLowerInvariant())
            {
                case "leftbutton":
                    button = MouseButton.Button1;
                    return true;
                case "rightbutton":
                    button = MouseButton.Button2;
                    return true;
                case "middlebutton":
                    button = MouseButton.Button3;
                    return true;
                case "xbutton1":
                    button = MouseButton.Button4;
                    return true;
                case "xbutton2":
                    button = MouseButton.Button5;
                    return true;
                default:
                    button = MouseButton.Button2;
                    return false;
            }
        }

        /// <summary>解析为 SharpHook 鼠标键；缺键 / 非法配置值回退默认右键。</summary>
        public static MouseButton ParseOrDefault(string? name)
            => TryParse(name, out MouseButton button) ? button : MouseButton.Button2;
    }
}
