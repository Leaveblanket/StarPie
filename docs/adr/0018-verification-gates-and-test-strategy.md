# 验证义务分层：提交级全量 xUnit + e2e 免跑判定，合入门全量

模块化程序集拆分（7 程序集，#75–#83）完成后，“每次 feature/bug-fix 提交都跑全量 xUnit + pywinauto e2e”成为分支内最高频成本；评估“按修改模块拆分测试子集以省全量”时实测：xUnit 全量 569 例仅 0.5s（含构建 12.8s），e2e 单文件 19 例、function-scope 每例冷启动（全量估 2–4 分钟）。决定：不按模块拆分测试工程或用例组，改为两级验证门 + e2e 免跑判定，全量兜底放在合入门；e2e 每用例冷启动予以保留。

## Status

Accepted（grilling 共识 Q1–Q5，2026-09-07，issue #86）。

## Considered Options

1. **按模块拆分 xUnit/e2e 子集（多测试工程或影响集脚本）**：xUnit 全量 0.5s，无可省空间；测试文件平铺且跨程序集引用（如 MainViewModelTests 同时依赖 Host/Shell/Gestures 注册器），归属映射与收口集需持续维护 → 否。
2. **e2e 按页面拆组或会话级复用同一应用实例**：每例冷启动 = 每例独立沙箱 + 默认配置确定性；19 例多数写穿 config 不还原（AppTheme/Theme/UiStyle/SectorCount/WheelRadius/CoreIconType/Action Type 等），断言隐含“未被前例污染”；移除重启引入顺序依赖与单例崩溃级联失败，收益上限约 40–90s → 否。
3. **两级验证门 + e2e 免跑判定** → **采纳**。

## Decision

1. **提交级（分支内）**：build 通过 + 全量 xUnit 绿（569 例，纯执行约 0.5s，不拆）；e2e 采用免跑判定——仅当改动触及用户可见面（resx/文案键与语言回退、页面/窗口 XAML 与模板字典、导航槽位/AutomationId、主题字典/令牌、设置交互与对话框可见行为、`config.json` 默认值与兼容、e2e 自身）时跑全量 `python -m pytest tests/test_settings.py -v`；拿不准时跑全量。
2. **合入门（merge 到 main 前）**：主干同步后一次全量 xUnit + 一次全量 pywinauto e2e，agent 本地执行。
3. **不做**：不按模块拆 xUnit/e2e 测试、不引入影响集脚本、不移除 e2e 每用例冷启动。
4. **CI**：`.github/workflows/build-and-test.yml` 增加 xUnit 步骤作机械门；e2e 因 GitHub runner 的 WPF UI 自动化可靠性问题暂不上 CI。
5. **提速边界**：只允许无损手段（如 conftest 固定 sleep 改条件等待窗口就绪）；保留 function-scope fixture 与独立沙箱语义。

## Consequences

- `docs/agents/git-commits.md` 验证义务改为两层门并附免跑判定表；`AGENTS.md`、`docs/architecture/assemblies.md` §8/§8.2、`docs/architecture.md`（技术栈/维护义务/ADR 索引）同步回填。
- 未来新增 e2e 用例继续遵循“每例可独立冷启动、不依赖用例顺序”的写法。
- 记录“为什么不按模块拆分”，防止后续被再次提议而重复评估。
