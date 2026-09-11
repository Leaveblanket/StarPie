# 贡献指南 (Contributing Guide)

感谢你对 **StarPie (星盘)** 的关注与支持！我们欢迎一切形式的代码贡献、文档优化、设计建议与 Bug 反馈。

---

## 🛠️ 本地开发环境准备

1. **操作系统**：Windows 10 / 11 (x64)；
2. **.NET 10.0 SDK**：[下载并安装 .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（仓库根 `global.json` 固定 SDK 基线，`rollForward` 允许更高特性带）；
3. **IDE / 编辑器**：Visual Studio 2022 (带 .NET 桌面开发工作负载) 或 VS Code / JetBrains Rider；
4. **Python 3.14**（可选，仅用于 pywinauto 端到端 GUI 测试）：依赖栈与实测解释器锁定在 `tests/requirements.txt`，运行器默认使用仓库内隔离 venv（`.venv`）。在仓库根执行：
   ```powershell
   python -m venv .venv
   .venv\Scripts\python -m pip install -r tests/requirements.txt
   ```
   `scripts/run-e2e.ps1` 的解释器解析顺序：`-Python` 显式指定 > 仓库 `.venv` > PATH（回退 PATH 时会警告"解释器未锁定"）。

### 🧩 与正式版并存开发（--dev 模式）

本机已安装并正在运行正式版 StarPie 时，开发实例可通过 `--dev` 参数与之并存，互不干扰：

```bash
dotnet run --project StarPie.Ui        # launchSettings 已默认附加 --dev
# 或显式指定：dotnet run --project StarPie.Ui -- --dev
```

`--dev` 开发实例与正式版的行为差异：

- **独立配置目录**：读写 `%LOCALAPPDATA%\StarPie-Dev`，首次启动会自动从正式版的 `StarPie` 目录复制一份配置作为起点，正式版配置永不被修改；
- **独立单实例锁**：开发实例与正式版可同时运行（各自仍只允许一个实例）；
- **手势触发键改为鼠标中键**：正式版继续占用右键，开发版用「按住中键拖动」唤出轮盘，避免两个轮盘同时弹出；
- **不写自启动注册表**：在开发实例中切换「开机自启」不会影响正式版的注册表项；
- 托盘提示与设置窗口标题附带 `(Dev)` 后缀，便于区分当前正在操作哪个实例。

> 提示：测试套件（`tests/conftest.py`）通过 `--allow-multiple` 与 `LOCALAPPDATA` 环境变量沙盒运行，不受 `--dev` 影响。

---

## 🚀 提交流程与规范

1. **Fork 代码库** 并克隆至本地；
2. **基于 `main` 分支创建特性分支**：
   ```bash
   git checkout -b feature/your-feature-name
   # 或修复分支
   git checkout -b fix/your-bug-fix
   ```
3. **编写与验证代码**：
   - 保持 C# 编码风格与项目现有架构一致；
   - 新增 UI 字符串请在 `StarPie.Core/Services/Localization/Strings.resx`（及 zh-TW/en/ja 卫星）补四语言键值；声明式文案经 XAML `{DynamicResource}`，动态文案经 `ILocalizationService` 即时取词；
   - 从仓库根构建全解决方案：`dotnet build StarPie.slnx`；
   - 运行全量 xUnit：`dotnet test --project StarPie.Tests/StarPie.Tests.csproj`；涉及用户可见 UI 时再运行 e2e：
     `pwsh -File scripts/run-e2e.ps1`（默认静默后台形态：被测应用离屏、不抢前台、不动物理光标，输出落
     `artifacts/e2e/`；调试用 `-OnScreen` 让窗口正常显示，`-Status` 查最近一次结果）。
4. **提交 Commit**（推荐采用约定式提交规范）：
   ```text
   feat: 增加新的轮盘渲染形态
   fix: 修复高分辨率缩放下的光晕偏移问题
   docs: 完善多语言配置文档
   ```
5. **发起 Pull Request (PR)**：
   - 清晰描述修改的背景、目的与实现细节；
   - 附带必要的界面截图或录屏。

---

再次感谢你为 StarPie 开源社区做出的贡献！🎉
