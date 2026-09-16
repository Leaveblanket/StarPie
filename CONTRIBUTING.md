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

### 🧩 本地开发（dev 实例）

开发实例（Debug 构建）与正式安装版**同闸互斥、不并行运行**——启动 dev 前先退出正式版，否则会按「已有实例」路径把正式版窗口置前后退出：

```bash
dotnet run --project StarPie.Ui        # Debug 构建（默认）即 dev 实例
# Release 构建即正式形态：dotnet run --project StarPie.Ui -c Release
```

dev 实例与正式版的行为差异：

- **独立配置目录**：读写 `%LOCALAPPDATA%\StarPie-Dev`，首次启动会自动从正式版的 `StarPie` 目录复制一份配置作为起点，正式版配置永不被修改；
- **不写自启动注册表**：在开发实例中切换「开机自启」不会影响正式版的注册表项；
- 手势触发与正式版相同（右键拖动唤出轮盘）；
- 托盘提示与设置窗口标题附带 `(Dev)` 后缀，便于区分当前正在操作哪个实例。

> 提示：测试套件（`tests/conftest.py`）通过 `--allow-multiple` 与 `LOCALAPPDATA` 环境变量沙盒运行，不受 dev 判定影响。

---

## 🚀 提交流程与规范

1. **Fork 代码库** 并克隆至本地；
2. **基于 `main` 分支创建特性分支**，命名 `<type>/#<issue>-<slug>`：
   ```bash
   git checkout -b feat/#123-my-feature
   # 或修复分支
   git checkout -b fix/#124-my-bug-fix
   ```
3. **编写与验证代码**：
   - 保持 C# 编码风格与项目现有架构一致；
   - 新增 UI 字符串请在 `StarPie.Host/Localization/Strings.resx`（及 zh-TW/en/ja 卫星）补四语言键值；声明式文案经 XAML `{DynamicResource}`，动态文案经 `ILocalizationService` 即时取词；
   - 从仓库根构建全解决方案：`dotnet build StarPie.slnx`；
   - 运行全量 xUnit：`dotnet test --project StarPie.Tests/StarPie.Tests.csproj`；涉及用户可见 UI 时再运行 e2e
     （免跑判定见 `docs/agents/git-commits.md`）：
     `pwsh -File scripts/run-e2e.ps1`（默认静默后台形态：被测应用离屏、不抢前台、不动物理光标，输出落
     `artifacts/e2e/`；调试用 `-OnScreen` 让窗口正常显示，`-Status` 查最近一次结果）。
   - 纯文档改动（只改 `docs/**`、`*.md` 等非代码文件）免除上述 build 与测试，直接进第 4 步。
4. **提交 Commit**：约定式提交、主题用中文；`feat`/`fix`/`refactor` 类工作必须在主题末尾引用未关闭的 issue：
   ```text
   feat: 增加新的轮盘渲染形态 (#123)
   fix: 修复高分辨率缩放下的光晕偏移问题 (#124)
   ```
   完整规则（分支与 merge 提交、原子性、验证义务分层）见 `docs/agents/git-commits.md`。
5. **发起 Pull Request (PR)**：
   - 清晰描述修改的背景、目的与实现细节；
   - 附带必要的界面截图或录屏。

---

再次感谢你为 StarPie 开源社区做出的贡献！🎉
