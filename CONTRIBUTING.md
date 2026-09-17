# 贡献指南 (Contributing Guide)

感谢你对 **StarPie (星盘)** 的关注与支持！我们欢迎一切形式的代码贡献、文档优化、设计建议与 Bug 反馈。

---

## 🛠️ 本地开发环境准备

1. **操作系统**:Windows 10 / 11 (x64);
2. **.NET 10.0 SDK**:[下载并安装 .NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)(仓库根 `global.json` 固定 SDK 基线);
3. **IDE / 编辑器**:Visual Studio 2022(带 .NET 桌面开发工作负载)或 VS Code / JetBrains Rider;
4. **Python 3.14**(可选,仅用于 pywinauto 端到端 GUI 测试):依赖栈见 `tests/requirements.txt`,运行器默认用仓库内隔离 venv(`.venv`)。在仓库根执行:
   ```powershell
   python -m venv .venv
   .venv\Scripts\python -m pip install -r tests/requirements.txt
   ```
   `scripts/run-e2e.ps1` 解析顺序:`-Python` 显式 > 仓库 `.venv` > PATH(回退 PATH 会警告"解释器未锁定")。

### 🧩 本地开发(dev 实例)

开发实例(Debug 构建)与正式安装版**同闸互斥、不并行运行** —— 启动 dev 前先退出正式版:

```bash
dotnet run --project StarPie.Ui        # Debug 构建(默认)即 dev 实例
# Release 构建即正式形态:dotnet run --project StarPie.Ui -c Release
```

dev 实例与正式版的行为差异:

- **独立配置目录**:读写 `%LOCALAPPDATA%\StarPie-Dev`,首次启动自动从正式版 `StarPie` 目录复制一份配置作为起点,正式版配置永不被修改;
- **不写自启动注册表**:dev 实例切换「开机自启」不影响正式版的注册表项;
- 托盘提示与设置窗口标题附带 `(Dev)` 后缀,便于区分正在操作的实例。

> e2e 测试套件(`tests/conftest.py`)通过 `--allow-multiple` 与 `LOCALAPPDATA` 环境变量沙盒运行,不受 dev 判定影响。

---

## 🚀 提交流程

1. **Fork 并克隆**;
2. **从 `main` 开分支**,按改动意图命名(`feat-…` / `fix-…` / `chore-…`,扁平名);
3. **编写与构建**:
   - 仓库根构建:`dotnet build StarPie.slnx`;
   - 测试:`dotnet test --project StarPie.Tests/StarPie.Tests.csproj`(勿加 `--nologo`:SDK 会把它转发给测试体,而 MTP 不认该参数,表现为「运行了零个测试」+ 退出码 5);
   - 用户可见改动(UI、对话框、配置 schema、本地化键、e2e 本身)再跑 e2e:`pwsh -File scripts/run-e2e.ps1`;
   - e2e 两档:日常快检走巡检层 `pwsh -File scripts/run-e2e.ps1 -NoBuild -TestPath tests/test_smoke.py`
     (少量实例覆盖页面/控件/目录/i18n;失败报告附带复审判例节点,按节点 `-TestPath <文件>::<用例>` 单跑);提交前跑全量(默认 `tests`);
4. **提交**:清晰写改了什么 + 为什么;`feat` / `fix` / `refactor` 工作引一句 issue 编号即可(不强求);
5. **开 PR**:背景 / 目的 / 改动摘要 + 必要的界面截图或录屏。
