# 对话框服务设计（IDialogService）

设置窗口 code-behind 散落 12 处模态对话框调用（程序选择器 ×3、输入框 ×4、图标选择 ×2、颜色选择 ×1、屏上取色 ×1、系统文件对话框 ×1）。决定全部收敛到 `IDialogService`，VM 层达成零对话框类型引用。

## 接口形状

- **具名方法**，每类对话框一个：`ShowProgramPicker` / `ShowInputDialog(title, prompt, defaultText, validator)` / `ShowIconPicker(currentKey)` / `ShowColorPicker(initialHex)` / `ShowEyedropper` / `ShowOpenFileDialog(filter)`。验证回调作为参数由调用方 VM 传入。
- **同步签名**：`ShowDialog` 是同步原语，不做假异步。
- **可空结果对象**（record）：取消与无效统一返回 `null`；有效性规则收进对话框 VM。调用方只判一次 null，不再做"确认 + 内容非空"双重判断。
- `OpenFileDialog`（BCL 抽象）一并入服务，保持边界完整。否决了泛型 `ShowDialog<TViewModel, TResult>` 方向——mock 要搭类型脚手架，违反"测试只测外部行为"。

## Owner 与生命周期

- **单 Owner 模型**：组合根先建服务、后建窗口，窗口创建完成后惰性回填 Owner（化解服务 ↔ 窗口循环依赖）。
- Owner 的使用方式是实现内部自由：取色器是全屏置顶工具，不用 Owner。Owner 概念不泄露进接口签名。
- 将来若轮盘窗口也需要对话框，升级为按活动窗口动态解析。
- 迁移期允许服务实现内部暂 `new` 旧 code-behind 窗口，随票替换为 VM 化窗口；接口稳定不变。

## 测试

- mock `IDialogService` 测调用方 VM 流程。
- 对话框自身 VM 不为扫描 IO 开新缝：垃圾过滤、去重、显示名升级提为静态纯函数直接测；注册表与文件系统扫描保持集成性质不测。

## 排查结论

- 12 处均为模态调用；对话框确认后一次性写回，与"立即生效"语义无中间态冲突。
