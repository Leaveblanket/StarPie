# StarPie 内存修剪方案与真实占用实测报告

> **被测版本**：StarPie v1.4.1（`StarPie.Ui` 程序集名 `StarPie`，`AssemblyVersion` 1.4.1.0），工作副本 `main` @ `0d39d10`
> （工作区另有未提交在途修改：`StarPie.Ui/App.xaml.cs`、`Composition.cs`、`ShellHost.cs` 的测试实例退出消息，与内存无关）
> **构建方式**：`dotnet build StarPie.Ui/StarPie.Ui.csproj -c Release`（框架依赖，`net10.0-windows10.0.19041.0`，
> 运行时 10.0.1126.37416，win-x64，Workstation 并发 GC）
> **测量日期 / 环境**：2026-09-14，Windows 10.0.19045 x64，16 逻辑处理器
> **测量方式**：真实运行进程（非估算、非静态分析）——外部 `psapi!GetProcessMemoryInfo(PROCESS_MEMORY_COUNTERS_EX2)` 50~100 ms 逐帧采样，
> 进程内 EventPipe 真值（`dotnet-counters` 1 Hz 计量 + `dotnet-trace` 逐次 GC 事件）
> **原始数据**：`.scratch/memprobe/runs/<RUN>/`（逐步采样 CSV、计数器 CSV、`*.nettrace.gc.csv` 逐次 GC 记录）
> **脚手架与复现**：`.scratch/memprobe/README.md`

---

## 一、结论速览

1. **本项目的"内存修剪"只做纯托管 GC 收敛，完全不碰工作集**：唯一实现是 `MemoryOptimizer.CollectGarbage`
   ——两轮 `GC.Collect(2, Forced, blocking, compacting)` + `WaitForPendingFinalizers()`；
   上游的 `EmptyWorkingSet` / `SetProcessWorkingSetSize` 已被整体删除，并有机械断言（禁词表）阻止回归。
   **实测形态印证**：设置台关窗出账后工作集回落 **185.42 → 176.46 MB（−8.96 MB）**；静默冷启动后
   工作集停在 **134.7 MB 纹丝不动**。对照组是上游同场景的 **203.50 → 16.01 MB** 与 **84.36 → 0.72 MB**
   ——两者不是同一类方案，不可用同一把尺子比较。
2. **修剪记录可从运行时事件逐条复核**（不是从内存曲线反推）：每次关窗出账 = **2 次 gen2 诱导压缩回收**
   （`InducedCompacting`，暂停 4.71~15.80 ms）；启动兜底 = 同款 2 次（t=873.1 / 886.7 ms，暂停 9.51 / 4.17 ms）。
   B 场两次关窗共 4 次、C 场三次关窗共 6 次，与源码的"两轮循环"一一对应（全场共 9 次修剪 / 18 次诱导回收）。
3. **修剪能回收的"对象层"内存只有个位数 MB**：两次关窗修剪后托管堆生存集为
   **gen2 6.87 → 4.43 MB、LOH 0.52 MB**，GC 堆提交 11.9~29.8 MB；而进程提交在 **91~193 MB**。
   也就是说，**内存主体在托管堆之外**（WPF 渲染栈、D3D、字体/纹理缓存、本机页），GC 修剪天生够不到它们。
    `System.GC.HeapHardLimit`（256 MiB）在整场测量中从未被逼近，它的作用是兜住病态增长，不是日常约束。
   甚至：重开不久的会话再关一次时，修剪在进程层面**没有任何净回收**（177.07 → 178.19 MB）——它回收的是会话垃圾，不是"内存水位"。
4. **轮盘唤出是当前最大的单项内存事件，但会收敛、不累积**：首次唤出 **+21.68 MB 工作集 / +54.6 MB 提交**，
   第 2、3 次 +14.21 / +5.22 MB，第 4~8 次在 ±3 MB 内波动（第 9 次出现一次自然 GC，回收 10.81 MB），
   连续 10 次唤出后静置稳定在 **168.96 MB**，无逐次累加。
   轮盘是"每次手势一个实例"（三次唤出的 HWND 各不相同），启动离屏预热把 BAML/渲染器工厂等一次性成本前移到了启动期。
5. **未发现累积型内存泄漏**：三轮"开控制台 → 逐页 → 关窗出账"的工作集为 180.49 / 177.93 / 187.00 MB（振荡），
   句柄 715 / 725 / 723、GDI 对象 31~39、线程 25~27 均不单调增长；十次唤出后静置 20 s 也不回落但也不增长。
6. **现成的内存断言里有 1 项在 Release 下失败**：`NavigationSuspensionTests.插件页VM随出账真实回收_无静态根`
   （全量 980 项：979 过 / 1 败）。Debug 通过、`DOTNET_TieredCompilation=0` 下 Release 也通过
   → 是**测试自身的 JIT 局部变量活跃性问题**（断言的桩不牢），不是产品泄漏；但会让
   `dotnet test -c Release` 门禁一直红（见 §5.1）。
7. **顺带发现并修掉一个发布阻断**：`dotnet publish -r win-x64 --self-contained` 因插件打包把宿主 RID
   下传给平台中立的插件工程而报 `NETSDK1047`（与 CI 最近几次失败的落点一致，见 §5.7）。
8. **本轮已修正并提交三项**：测试断言桩 `2bec976`、文档口径 `740d17b`、发布阻断 `c1b24af`
   （验证与影响面见 §八）。

---

## 二、修剪方案全貌（源码级）

### 2.1 唯一实现：`MemoryOptimizer.CollectGarbage(bool force = false)`

全部 GC 收敛在一个文件：`StarPie.Host/Kernel/ShellIntegration/MemoryOptimizer.cs`（`public static`，零 WPF、纯托管）。

```csharp
// 节流 + 防重入（MemoryOptimizer.cs:22-29）
if (!force && (DateTime.UtcNow - _lastTrimTime).TotalSeconds < 2.0) return;
if (Interlocked.Exchange(ref _isTrimming, 1) == 1) return;
Task.Run(() =>                                 // 永远在后台线程池执行，不占 UI 线程
{
    // 两轮：终结器执行可能复活对象并产生新垃圾，排空后再收一次（MemoryOptimizer.cs:38-40）
    GC.Collect(2, GCCollectionMode.Forced, true, true);
    GC.WaitForPendingFinalizers();
    GC.Collect(2, GCCollectionMode.Forced, true, true);
});
```

要点：

- **只有一档力度**。`force` 的唯一作用是**绕开 2 秒节流**（`force: true` 时首条件短路）；两轮全量压缩回收
  在任何档位都执行。这与上游"深度档（含 `EmptyWorkingSet`）/ 温和档（`GCCollectionMode.Optimized`，
  不碰工作集）"的双档设计不同——**工作集裁剪能力已被整体删除**。
- **`_lastTrimTime` 写在后台任务内**（首个任务尚未开始时窗口内可能重复入队），但 `_isTrimming` 已先置 1，
  重复调用会在重入检查处返回，故不存在重复修剪的竞态。
- 异常被 `catch (Exception ex)` 吞进 `Debug.WriteLine`，`finally` 复位 `_isTrimming`——**修剪失败永远静默**。
- **回归护栏**：`StarPie.Tests/MemoryResidencyTests.cs` 以禁词表（`TrimMemory`、`EmptyWorkingSet`）扫描全仓库
  源码树，出现即失败（本类自身被排除以免自匹配）。

### 2.2 全部触发点（无定时器、无手动入口）

| # | 触发时机 | 档位 | 前置顺序 | 代码位置 |
|---|---|---|---|---|
| 1 | **启动兜底**（启动编排末尾） | `force: true` | 轮盘预热之后（#150）：`WarmUpWheelCorePath()` → `RunStartupMemoryHousekeeping()` | `StarPie.Ui/ShellHost.cs:223-224`、`:236` |
| 2 | **设置台关窗进托盘** | `force: false`（受 2 s 节流） | `FlushPendingSave → 导航视图出账 → 图标缓存出账 → SendMinimized → CollectGarbage` | `StarPie.Ui/SettingsConsole.cs:175-177`；顺序由 `StarPie.Host/Kernel/ShellIntegration/TrayStateSignal.cs:59` 决策 |
| 3 | **插件卸载**（两处） | 内部私有 `CollectGarbage()` | 卸载管线内 | `StarPie.Host/PluginRuntime/Unloading/PluginUnloadPipeline.cs:452`、`:478` |

- **没有周期性/定时修剪**：全部为事件驱动或一次性。
- **没有用户可见的"内存整理"入口**：设置页手动按钮与四语言文案已随 #149 删除（不留"留作诊断"死路径）。
- **静默后台形态（`--background`，e2e 用）整体禁用出账动作**：`TrayStateSignal.Resolve` 在该形态下把
  `FlushPendingSave / ReleaseNavigation / ReleaseIconCaches / CollectGarbage` 全部略去，只发 `SendMinimized`
  （`TrayStateSignal.cs:57-60`）。启动兜底那次 GC 不受影响（不经过托盘信号）。

### 2.3 与修剪配套的非 GC 回收（本项目内存策略的主体）

修剪只是兜底；真正决定内存形态的是下面这几条（第 #149~#158 系列的工作）：

- **出账序列**（进托盘固定顺序）：`FlushPendingSave → 导航视图出账（NavigationSuspension.Release）→
  图标缓存出账（IIconAssetService.ReleaseTransientCaches）→ MinimizedToTrayMessage → CollectGarbage`；
  恢复按最后导航槽位重放导航后发 `RestoredFromTrayMessage`。
- **设置台是瞬态租户**（ADR-0039）：关窗即销毁窗口与 VM 树（`Window_Closing` 不再取消），
  托盘驻留由常驻壳层（`ShellHost` + 托盘消息窗口）承担；关窗收尾走
  `TransientWindowTeardown.Complete`（清动画 → 丢弃内容与 DataContext → Close → 排空 Dispatcher →
  `Application.MainWindow` 回退常驻锚窗口）。
- **页面 VM 会话作用域化**（#157）：`ConsolePageSession` 在会话内缓存页面 VM 实例，
  会话结束时 `End()` 清空记账并释放作用域（整批释放）；`scoped`/`singleton` 的差别由注册生命周期表达。
- **轮盘：每次手势一个实例**（`GestureEngine.cs:111` `_wheelFactory.Create(...)`，`:246` `Close()` 后置 null），
  配合 **启动离屏预热** `WheelWarmup.Run(...)` 把首次唤出的一次性成本前移。
- **图标资产出账**：`IIconAssetService.ReleaseTransientCaches()` → `CustomIconStore.ClearCache()`
  （丢掉自定义图标目录列表缓存，下次按需重扫；见 `StarPie.Tests/IconCacheReleaseTests.cs` 的"清空幂等 + 清后重建"断言）。
  **注意**：本项目**没有**上游那种"常驻 pinned + LRU 动态"位图层缓存，出账清的是列表缓存。

### 2.4 运行时与项目配置

- `StarPie.Ui/runtimeconfig.template.json`：`"System.GC.HeapHardLimit": 268435456`（256 MiB）；
  已并入编译产物 `StarPie.runtimeconfig.json`（`MemoryResidencyTests` 对该声明面与产物双断言）。
- `StarPie.Ui/StarPie.Ui.csproj`：`<ServerGarbageCollection>false</ServerGarbageCollection>`、
  `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`。SOS 实测确认运行时为
  **Workstation mode**（`!eeversion`：`10.0.1126.37416 free / Workstation mode`），单 GC 堆。
- ⚠️ **"GC 堆硬顶生效值"日志在 Release 构建里并不存在**：该行 `Debug.WriteLine`
  （`ShellHost.cs:229-235`）被 `[Conditional("DEBUG")]` 在 Release 编译期整体移除。
  实测校验：UTF-16 字面量 `HardLimit 生效值` 在 `bin/Release/.../StarPie.dll` 命中 **0** 次、
  在 `bin/Debug/.../StarPie.dll` 命中 **1** 次。文档里"生效值由启动日志记录"的表述只在 Debug 构建成立。

---

## 三、实测方法

### 3.1 三个内存口径（报告全程区分）

| 口径 | 含义 | 数据来源 |
|---|---|---|
| **总工作集** | 进程当前占用的全部物理页面，**含共享 DLL 代码页** | `psapi!GetProcessMemoryInfo`（`PROCESS_MEMORY_COUNTERS_EX2.WorkingSetSize`） |
| **专用工作集** | 只算进程私有物理页面（**任务管理器默认「内存」列**） | 同结构 `PrivateWorkingSetSize` |
| **提交量** | 已在页面文件中"预定"的私有字节（**任务管理器「提交大小」**） | 同结构 `PrivateUsage` / `GetProcess.PrivateMemorySize64` |

**三源交叉校验**（静默稳态同一次运行内读取）：

| 来源 | 总工作集 | 专用工作集 | 提交 |
|---|---|---|---|
| 本报告探针（psapi EX2） | 133.55 MB | 41.40 MB | 90.67 MB |
| 性能计数器 `\Process(StarPie)\Working Set[- Private]` | 133.66 MB | 41.40 MB | — |
| `Get-Process StarPie`（`WorkingSet64` / `PrivateMemorySize64`） | 133.56 MB | — | 90.67 MB |

三源偏差 ≤ 0.11 MB，故下文数字可直接与任务管理器/性能监视器对照复现。
（另：`IsDevInstance` 由编译期 `DEBUG` 定死——Release 产物读写 `%LOCALAPPDATA%\StarPie`，
本报告全部运行经 `LOCALAPPDATA` 环境变量重定向到沙箱目录，未触碰真实用户配置。）

### 3.2 实验矩阵

| 实验 | 场景 | 形态 | 进程内真值 |
|---|---|---|---|
| **A / A2 / A3** | 静默冷启动（`--background`）与启动兜底修剪 | 静默 | A3 经 `DOTNET_DiagnosticPorts=suspend` 从进程启动瞬间采 GC 事件 |
| **B** | 设置台完整生命周期：开 → 逐页（5 页）→ 关窗出账 → 托盘驻留 20 s → 单实例恢复重开 → 再关 | **可见** | 计数器 + 逐次 GC 事件 |
| **C** | 三轮"开 → 逐页 → 关窗出账"，泄漏判定；另有静默形态对照组 | 可见 + 静默对照 | 计数器 + 逐次 GC 事件 |
| **D** | 冷实例连续三次唤出轮盘（`SendInput` 真实手势） | 可见（鼠标钩子生效） | 逐次 GC 事件 |
| **E** | 三次唤出后关窗：手势残留是否由下一次出账回收；再重开做热态唤出 | 可见 | 计数器 |
| **F** | 连续 10 次唤出，判定"逐次累加"还是"收敛到稳态" + 末尾静置 20 s | 可见 | 采样 |

### 3.3 探针与工具链

- **外部采样**：`GetProcessMemoryInfo(PROCESS_MEMORY_COUNTERS_EX2)` 取工作集/专用工作集/提交/峰值/缺页计数，
  `GetGuiResources` 取 GDI/User 句柄，Toolhelp 快照取线程数，采样间隔 50~100 ms。
- **进程内真值**：`dotnet-counters collect --counters System.Runtime[dotnet.gc.collections,
  dotnet.gc.last_collection.heap.size, dotnet.gc.last_collection.memory.committed_size,
  dotnet.gc.pause.time, dotnet.gc.heap.total_allocated]`（1 Hz，`--refresh-interval` 只接受整数秒）。
- **逐次 GC 记录**：`dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:0x1:4`
  采到 nettrace 后由本报告自带的 `GcTrace`（`Microsoft.Diagnostics.Tracing.TraceEvent`）解析为
  "序号 / 起始 ms / 代数 / 原因 / 暂停 ms / 回收后各代堆大小"。
  B/C/D 三场实验的采集通过 `DOTNET_DiagnosticPorts=<port>,suspend,connect` 让运行时**启动即挂起**，
  等追踪工具连上再放行——否则启动兜底那次 GC（发生在 ~0.9 s）会落在采集窗口之外
  （首版 `dotnet-counters` 会话正是因此漏掉了它，改用挂起启动后捕获成功）。
- **手势注入**：`SendInput`（`MOUSEEVENTF_ABSOLUTE|MOVE` + 右键按下/抬起），与真人操作同一条输入队列，
  经项目自身的 `WH_MOUSE_LL` 钩子链路处理。
- **UI 驱动与收尾**：pywinauto（uia 后端）按 `AutomationId` 选中侧边栏导航项并等待页面锚点控件；
  关窗用 `WM_CLOSE` 投递（与点关闭按钮同一条路径）；退出用测试实例退出消息
  （`RegisterWindowMessage("StarPie_TestInstance_Exit")`）走**真实退出编排**，不用硬杀（避免通知区幽灵图标）。

### 3.4 实验安全性（对被测机与用户配置的影响）

- 手势模拟为**零动作风险设计**：右键按下 → 拖出 25 px 激活阈值 → **以 92 px 半径环绕一周悬停全部扇区
  （只高亮，不释放）** → 回到圆心（距起点 0 px，位于 15 px 死区内，`selectedSector = -1`）→ 抬起。
  默认 `Global` 方案的 8 个动作（Ctrl+C / 锁定电脑 / 显示桌面 / 截图 / Ctrl+V / 音量± / 记事本）
  **一个都不会执行**；环绕半径 92 px 恒小于外甩阈值 186 px，也不触发外甩取消。
- 应用数据全程沙箱化（`LOCALAPPDATA`/`APPDATA`/`TEMP` 重定向到 `.scratch/memprobe/runs/<RUN>/`），
  **未读写真实用户配置、未改注册表、未创建计划任务**（开机自启只在用户手动切换开关时写入，全程未触发）。
- 所有实验进程均走真实退出路径收尾并确认无残留：`tasklist` 复核无 `StarPie.exe` 残留。
- **副作用说明**：B/C/D/E/F 采用可见形态，测量期间设置台窗口会在屏幕上出现并被激活（约 40~70 s/场），
  鼠标光标会被脚本移动以完成手势；`Application.MainWindow` 归属、焦点与剪贴板均无改动。

---

## 四、实测结果

### 4.1 实验 A/A3：静默冷启动与启动兜底修剪

| 时间点 | 总工作集 | 专用工作集 | 提交量 | 事件 |
|---|---|---|---|---|
| t = 0.03 s | 2.51 MB | 0.23 MB | 0.44 MB | 进程创建（运行时处于诊断挂起） |
| t ≈ 0.42 s | 65.89 MB | 12.36 MB | 19.71 MB | CLR/WPF 初始化 |
| t ≈ 0.82 s | 116.60 MB | 30.38 MB | 59.49 MB | 设置台窗口即将呈现 |
| t ≈ 0.9 s | **134.74 MB** | 42.30 MB | 91.83 MB | 设置台窗口呈现（启动峰值） |
| **t ≈ 0.87 / 0.89 s** | — | — | — | **启动兜底修剪：2 次 gen2 `InducedCompacting`，暂停 9.51 / 4.17 ms** |
| t = 1.98~20 s | **134.74 ~ 135.20 MB** | 42.30 MB | 91.75~91.83 MB | 静默稳态（**无任何回落**） |

- 修剪后的托管堆（运行时事件随附的 `GCHeapStats`）：gen1 = 3.75 MB、**gen2 = 0.00 MB**、LOH = 0.29 MB、POH = 0.06 MB；
  GC 堆提交 11.9 MB。
- 静默稳态：句柄 643、线程 21、GDI 对象 37、User 对象 30。
- **与上游的形态差异**：上游同场景是 84.36 MB →（1.5 s 后深度档 `EmptyWorkingSet`）→ **0.72 MB**；
  本项目是 134.74 MB →（两轮 GC）→ **134.74 MB**。前者把物理页面全推出去（代价是首次手势补页 +111 MB），
  后者原地不动（GC 只回收托管对象，实测本场景可回收量不足 0.1 MB 提交）。

### 4.2 实验 B：设置台完整生命周期（可见形态）

| 阶段 | 总工作集 | 专用工作集 | 提交量 | 事件/备注 |
|---|---|---|---|---|
| 启动后静默 | 136.99 MB | 43.68 MB | 94.38 MB | 设置台首次 Show 之前 |
| 控制台开启（含逐页导航 6 次） | 中位 179.35（五页全开 185.41，峰值 189.70） | 中位 72.96（全开 73.66） | 中位 135.32（全开 148.28） | 五页全部构造过一遍 |
| **关窗 #1（出账 + GC 修剪）** | **185.42 → 176.46（−8.96）**，随后自沉到 173.03 | 73.66 → 64.43（−9.23） | 148.28 → 139.59（−8.69） | 修剪分两轮，见下表 #9/#10 |
| 托盘驻留 20 s（145 帧） | 173.03 ~ 177.02 MB | 60.44 ~ 64.48 MB | 135.53 ~ 139.91 MB | 无隐式释放；无诊断会话的重复运行是 **170.02~170.11 的死平线**（见 §3.4 扰动说明） |
| 单实例恢复重开 | 173.07 → 177.47 MB | 60.32 → 63.90 MB | 139.00 → 153.27 MB | 重建 VM 树 + 重放 5 页导航：工作集 +4 MB 级、提交 +14 MB |
| **二次关窗 #2（出账 + GC 修剪）** | **177.07 → 178.19（+1.12，未净降）** | 63.49 → 64.31 | 137.56 → 139.40 | **修剪近乎空转**：重开的会话页面栈浅、可回收对象少（见下） |
| 驻留至退出 | 178.19 MB | 64.31 MB | 139.40 MB | 退出编排把工作集降到 152.97 → 进程结束 |

**逐次 GC 记录**（`B_20260914_181755/lifecycle.nettrace.gc.csv`，共 12 条）：

| # | 起始 (ms) | 代数 | 原因 | 暂停 (ms) | 修剪后 gen1/gen2/LOH (MB) | 对应动作 |
|---|---|---|---|---|---|---|
| 1 | 899.1 | 2 | InducedCompacting | 8.96 | — | 启动兜底第 1 轮 |
| 2 | 912.0 | 2 | InducedCompacting | 4.78 | 3.88 / 0.00 / 0.29 | 启动兜底第 2 轮 |
| 3~8 | 5919~13577 | 0 | AllocSmall | 1.96~7.25 | — | 逐页导航的自然回收（6 次） |
| 9 | 18358.2 | 2 | **InducedCompacting** | 14.15 | — | **关窗出账第 1 轮** |
| 10 | 18378.4 | 2 | **InducedCompacting** | 5.86 | 1.27 / 6.87 / 0.52 | **关窗出账第 2 轮** |
| 11 | 46762.2 | 2 | **InducedCompacting** | 7.91 | — | 二次关窗第 1 轮 |
| 12 | 46773.0 | 2 | **InducedCompacting** | 5.34 | 0.78 / 4.43 / 0.52 | 二次关窗第 2 轮 |

- **规律**：每次关窗恰好 2 次 gen2 诱导压缩回收，两轮相隔 ~20 ms，与源码的两轮循环严格对应。
- **修剪到底回收了什么**：第二次修剪后整个托管堆只有 `0.78 (gen1) + 4.43 (gen2) + 0.52 (LOH) ≈ 5.7 MB` 存活；
  而进程此刻提交 **139 MB**。差值 ~134 MB 全在托管堆之外。
- **修剪的收益取决于"有多少垃圾"**：关窗 #1 回收 8.96 MB 工作集 / 8.69 MB 提交（刚跑完 5 页导航，会话对象图大）；
  关窗 #2 在进程层面**没有净下降**（177.07 → 178.19，提交 137.56 → 139.40）——重开的会话生命周期短、
  页面栈浅，出账时已几乎无不可达对象，两轮全量压缩也就空转（仍耗时 ~13 ms）。
  这说明**不能把"关窗修剪"当成稳定的省内存手段**，它更像是把会话垃圾还回去的收尾动作。
- 20 s 托盘驻留期间**没有任何 GC 发生**（既无诱导也无自然），内存保持在同一水平带内。

### 4.3 实验 C：三轮"开 → 逐页 → 关窗出账"（泄漏判定）

**可见形态**（出账 + GC 生效，`C_20260914_181952`）：

| 轮次 | 状态 | 总工作集 | 专用工作集 | 提交量 | 句柄 | 线程 | GDI |
|---|---|---|---|---|---|---|---|
| 1 | 五页全开 | 184.72 MB | 77.41 MB | 150.41 MB | 716 | 26 | 39 |
| 1 | 关窗出账后 | 180.49 MB | 69.10 MB | 142.71 MB | 715 | 26 | 33 |
| 2 | 五页全开 | 185.86 MB | 70.62 MB | 142.64 MB | 727 | 27 | 38 |
| 2 | 关窗出账后 | 177.93 MB | 62.31 MB | 130.37 MB | 725 | 27 | 32 |
| 3 | 五页全开 | 190.61 MB | 73.76 MB | 149.92 MB | 727 | 26 | 37 |
| 3 | 关窗出账后 | 187.00 MB | 69.97 MB | 138.73 MB | 723 | 25 | 31 |

- 工作集/专用工作集/提交量与句柄**都不单调增长**（工作集 180.5 / 177.9 / 187.0 振荡，
  句柄 715 / 725 / 723，GDI 31~39，线程 25~27），与上游"固定常驻集而非每轮泄漏"的判定一致。
- 该场共 26 条 GC 记录：**8 条诱导 gen2**（启动 2 + 每轮关窗 2×3）+ 18 条自然 gen0。

**静默后台形态对照组**（`--background`，出账动作整体禁用，`C_20260914_181136`）：

| 轮次 | 状态 | 总工作集 | 专用工作集 | 提交量 |
|---|---|---|---|---|
| 1 | 五页全开 | 181.47 MB | 76.00 MB | 142.74 MB |
| 1 | 关窗后（无出账） | 185.14 MB | 76.33 MB | 143.28 MB |
| 2 | 五页全开 | 189.98 MB | 77.60 MB | 153.38 MB |
| 2 | 关窗后 | 189.88 MB | 77.46 MB | 141.30 MB |
| 3 | 五页全开 | **194.17 MB** | 80.47 MB | 151.80 MB |
| 3 | 关窗后 | **194.20 MB** | 80.45 MB | 145.61 MB |

- 全程 55 s 只有 **1 次自然 gen2 回收**、没有任何诱导回收，工作集单调爬升 +12.7 MB
  （181.47 → 194.20）。这正是"e2e 静默形态不覆盖出账路径"的直接后果：
  它适合功能用例，但**不适合用作长跑/内存用例的形态**。

### 4.4 实验 D/E/F：轮盘唤出的真实内存代价

**D：冷实例连续三次唤出**（`D_20260914_182112`，每次手势 ~1.5 s，间隔 5 s）

| 次数 | 唤出前基线 | 手势释放时刻 | **净增工作集** | 新增缺页 | 释放 5 s 后静置 |
|---|---|---|---|---|---|
| 第 1 次（冷态） | 139.70 MB | 159.16 MB | **+19.46 MB** | +9,663 | 161.51 MB（不回落） |
| 第 2 次 | 161.51 MB | 172.99 MB | **+11.48 MB** | +7,763 | 174.78 MB |
| 第 3 次 | 174.78 MB | 180.04 MB | **+5.26 MB** | +5,496 | 179.94 MB |

- 三次唤出的轮盘 HWND **各不相同**（`3936120` / `3146980` / `9440988`）→ 确实是每次手势新建实例。
- 轮盘窗口出现的那一刻是内存跳变点：提交量在 ~50 ms 内从 93.87 MB 跳到 132.58 MB（首个手势）。
- 三次手势期间**只有 1 次自然 gen0 回收**，没有任何诱导回收；手势结束后也不触发修剪（设计如此）。

**F：连续 10 次唤出（收敛判定）**

| 次数 | Δ 工作集 | 次数 | Δ 工作集 |
|---|---|---|---|
| 1 | **+21.68 MB** | 6 | +2.34 MB |
| 2 | **+14.21 MB** | 7 | +0.65 MB |
| 3 | **+5.22 MB** | 8 | +2.80 MB |
| 4 | +0.60 MB | 9 | **−10.81 MB**（自然 GC 回收） |
| 5 | −2.71 MB | 10 | +4.23 MB |

基线 134.58 MB → 十次后 172.79 MB，末尾静置 20 s：**168.96 MB**（专用 62.33 / 提交 175.65，句柄 656 / 线程 17）。
**结论：前三次是"热起来"的成本（合计 +41 MB），第 4 次起在 ±3 MB 内波动，无逐次累加**——
本轮唯一的 −10.81 MB 来自一次自然 GC（第 9 次期间），说明手势产生的托管垃圾平时不被回收，
要等下一次自然/诱导 GC。

**E：手势残留是否由下一次出账回收**

| 阶段 | 总工作集 | 专用工作集 | 提交量 | 缺页累计 |
|---|---|---|---|---|
| 手势前基线 | 137.76 MB | 42.80 MB | 94.23 MB | 45,784 |
| 3 次手势后 | 179.12 MB | 73.00 MB | 191.41 MB | 69,766 |
| **关窗出账（含 2 轮 GC）** | **169.12 MB**（−9.84） | 62.51 MB | **178.48 MB**（−12.93） | 70,822 |
| 驻留 35 s | 169.12 MB | 62.40 MB | 179.41 MB | 70,921 |
| 重开后再唤出（热态） | 173.04 → 178.18 MB | 64.82 → 69.16 MB | 164.65 → 178.79 MB | 77,369 |

- 出账 GC 只能回收手势残留的一小部分（工作集 −9.84 MB、提交 −12.93 MB，合计约 22 MB 中的一半），
  **相对手势前基线仍高出 ~31 MB**，且此后 35 s 稳定不动 —— 这部分是 WPF/渲染侧的热态常驻
  （窗口类注册、渲染线程与 D3D 资源、字体/纹理缓存等），**GC 修剪碰不到，只有进程退出才归还**。

### 4.5 修剪记录汇总（本次测量全部诱导回收）

| 实验 | 触发 | 诱导 gen2 次数 | 单次暂停 (ms) | 修剪后存活堆（gen1/gen2/LOH） |
|---|---|---|---|---|
| A3 | 启动兜底（`force: true`） | 2 | 9.51 / 4.17 | 3.75 / 0.00 / 0.29 MB |
| B | 启动兜底 | 2 | 8.96 / 4.78 | 3.88 / 0.00 / 0.29 MB |
| B | 关窗出账 #1 | 2 | 14.15 / 5.86 | 1.27 / 6.87 / 0.52 MB |
| B | 关窗出账 #2 | 2 | 7.91 / 5.34 | 0.78 / 4.43 / 0.52 MB |
| C（可见） | 启动兜底 | 2 | 8.67 / 4.91 | 3.88 / 0.00 / 0.29 MB |
| C（可见） | 关窗出账 ×3 | 6 | 11.91~15.80 / 6.10~7.26 | 0.17 / 7.74~8.37 / 0.52 MB |
| D | 启动兜底 | 2 | 8.61 / 4.67 | 3.86 / 0.00 / 0.29 MB |
| **合计** | 9 次修剪（启动 4 + 关窗 5） | **18 次** | 4.17 ~ 15.80（中位 ~6.4） | — |

- 全部为 **`InducedCompacting` gen2**，来源只有 `MemoryOptimizer`；没有一次自然 gen2 后台回收被观测到
  （唯一一次自然回收是 F 第 9 次手势期间的 gen0/gen2，见 §4.4）。
- 单次修剪总暂停 12~22 ms（两轮之和），对 UI 无感的量级；修剪本身在后台线程池执行。
- **诊断会话未改变被测行为**：未挂会话的 A 与挂会话的 A3 内存曲线同形（稳态 134.47 / 134.74 MB），
  挂会话的 B 与早前未挂会话的 B 在关窗回收幅度上也一致（−9 MB 级）。

### 4.6 与上游方案（`StarPie-Upsteam`）的对照

| 维度 | 上游（v1.7.4-beta.2） | 本项目（v1.4.1 @ 0d39d10） |
|---|---|---|
| 修剪手段 | 深度档 `EmptyWorkingSet` + `SetProcessWorkingSetSize`；温和档只 GC | **只有 GC**（两轮全量压缩 + finalizer）；工作集裁剪整体删除 |
| 静默启动后工作集 | 84.36 → **0.72 MB** | 134.74 → **134.74 MB**（不动） |
| 关窗出账后工作集 | 203.50 → **16.01 MB**（−92%） | 185.42 → **176.46 MB**（−4.8%；重开的会话再关一次则几乎不降） |
| 首次唤出轮盘净增 | **+111.71 MB**（补页为主，缺页 +43,243） | **+21.68 MB**（冷启动成本，缺页 +9,663） |
| 轮盘实例 | 进程级常驻单例（手势结束不回收） | **每次手势一个实例** + 启动离屏预热 |
| 修剪后回弹 | 明显（B 回弹到 111 MB） | 无回弹问题（本就未丢页面） |
| GC 堆约束 | 无硬顶、无 GC 配置 | `System.GC.HeapHardLimit = 256 MiB` |
| 手动修剪入口 | 「立即压缩内存」按钮 | 已删除（不留死路径） |
| 托盘驻留态 | ≈18~31 MB 工作集 | ≈170 MB 工作集 / 62 MB 专用（含已退出控制台的热态） |

**读法**：上游把"物理占用"做到极小、代价是冷启动后首次手势要补 110 MB 页面；
本项目把"补页"消除掉、代价是任务管理器里驻留数字大。二者是**同一权衡的两端**，
不是"谁实现得更好"；如果对外要给用户一个数字，应给**专用工作集**（62 MB，任务管理器默认列）
而不是总工作集（170 MB，含共享代码页与 WPF 渲染侧常驻）。

---

## 五、发现与建议

### 5.1 Release 门禁下有一条内存断言失败（优先级：高｜已修正，见 §八）

- **现象**：`dotnet test StarPie.Tests/StarPie.Tests.csproj -c Release` 全量 980 项 → **979 过 / 1 败**，
  失败项固定为 `NavigationSuspensionTests.插件页VM随出账真实回收_无静态根`
  （`StarPie.Tests/NavigationSuspensionTests.cs:130`，断言在 `:150`）。
- **定位**：Debug 配置通过；Release 下加 `DOTNET_TieredCompilation=0` 也通过
  → 不是产品代码的静态根滞留，而是**测试自身的 JIT 局部变量活跃性**：断言用的
  `WeakReference` 建立在同一方法内、且方法内还留着 `created`/`instance` 的调用链局部变量，
  优化后的分层 JIT 可以让这些栈槽在本帧内继续可达。
- **建议**：把"最后一次丢引用 + GC"挪进独立方法（参数只带 `WeakReference`），断言方法本身不持有强引用
  ——同 `ConsolePageSessionTests` 的既有先例；`ConsolePageSession` 的会话缓存只作用于固定页
  （`NavigationExecutor.Show` 的工厂分支：插件页每次导航由目录工厂新建，不经会话缓存），
  因此"插件页 VM 随出账可回收"这条口径本身成立，需要修的只是断言的桩。落地见 §八（提交 `2bec976`）。
- **旁证**：CI 的 `build-and-test.yml` 用的是 `dotnet test -c Release`，所以这条红是门禁级的；
  另外查到的最近几次 CI 失败停在 publish 步骤（`NETSDK1047`：`plugins/src/StarPie.Plugin.Programs`
  的 assets 缺 `net10.0/win-x64` 目标），且 `#158` 之后没有新的 CI 记录，当前 main 的测试门禁**未被 CI 覆盖**。
  该 publish 失败已单独定位并修正，见 §5.7。

### 5.2 文档口径与实现不一致：硬顶生效值日志在 Release 不存在（优先级：中｜已修正，见 §八）

- **现象**：`docs/architecture/shell.md`、`docs/architecture/host.md` 表述为"生效值由启动日志
  （`GC.GetConfigurationVariables`）记录"，但该 `Debug.WriteLine`（`ShellHost.cs:229-235`）受
  `[Conditional("DEBUG")]` 约束，在 Release 构建中**连同字面量一起消失**
  （实测：Release DLL 命中 0 次、Debug DLL 命中 1 次）。
- **影响**：正式版出问题时拿不到这条现场记录，而这正是"硬顶是否真的生效"的唯一运行时自证。
- **建议**：改为写入插件启动报告同类的落盘诊断（或复用宿主既有的诊断报告通道），
  至少在 Debug/诊断模式下可核。

### 5.3 轮盘唤出的首三次成本应当在文档里写清楚（优先级：中）

- **现象**：首次唤出 +21.68 MB 工作集 / +54.6 MB 提交，第二、三次 +14.21 / +5.22 MB，
  之后收敛到 ±3 MB；轮盘每次手势新建窗口（HWND 各不相同）。
- **含义**：这是**冷态一次性成本**（窗口类/渲染资源/视觉树/图标位图），启动预热只覆盖了
  "视图模型 + 渲染器工厂 + 调色板画刷"这一层（`WheelWarmup`），**没有覆盖 HWND 与渲染资源创建**。
  若希望首次唤出不掉帧/不尖峰，可把预热扩到"离屏创建并立刻关闭一个真实轮盘窗口"，
  代价是启动期多 10~20 MB 提交（当前启动已是 91 MB 提交，可接受）。
- 不建议为了数字去加"手势结束修剪"：实测残留里 GC 能碰的只有约 10~13 MB，
  修剪还会带来暂停；要减小驻留只能从渲染侧或复用轮盘窗口入手。

### 5.4 静默后台形态的"无出账"应显式写入测试纪律（优先级：中）

- **现象**：`--background` 下 `TrayStateSignal` 把出账动作（含 GC）全部略去，
  三轮开关后工作集单调 +12.7 MB 且无任何回收。
- **含义**：该形态是为"不打扰用户"设计的 e2e 形态，本身没错；但它**不能用于内存/长跑类断言**。
- **建议**：在 `docs/agents/` 或 e2e 说明里写明；若确有后台长跑用例，加一次显式
  `MemoryOptimizer.CollectGarbage(true)` 的测试专用触发（或直接跑可见形态）。

### 5.5 工作集口径需要一句"这是设计选择"的说明（优先级：低）

- 本项目现在**故意不做工作集裁剪**，所以任务管理器「内存」列会长期显示 130~190 MB；
  但**专用工作集**（同列的另一口径）只有 42~80 MB，且静默态提交稳定在 91 MB。
- 建议在 `docs/architecture/shell.md` 的内存小节补一句：*"本方案只做托管 GC 收敛、不做工作集裁剪；
  对外报告内存请用专用工作集（任务管理器默认列），并注明共享代码页与渲染侧常驻不随出账归还。"*
  这也能避免后来者把它当成"没修剪干净"而重新引入 `EmptyWorkingSet`（那会退回上游的补页代价）。

### 5.6 修剪的收益量级应被记录（优先级：低）

- 实测修剪真正回收的是**托管对象层**：两次关窗修剪后存活堆 `≈5.7 MB`（gen1 0.78 + gen2 4.43 + LOH 0.52），
  GC 堆提交 11.9~29.8 MB，进程提交 91~193 MB。
- 也就是说：**GC 修剪不是"省内存"的主力，而是"把设置台会话的对象图还回去"的收尾动作**；
  真正的驻留控制靠出账（导航/图标/会话作用域）与每个手势一个轮盘实例这类**释放时机**设计。
  建议在文档里按这个口径描述，避免把 256 MiB 硬顶误读成"日常占用上限"。

### 5.7 发布步骤失败：插件打包把宿主 RID 下传给了平台中立的插件（优先级：高｜已修正，见 §八）

- **现象**：`dotnet publish StarPie.Ui/StarPie.Ui.csproj -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true` 报 `NETSDK1047`（`plugins/src/StarPie.Plugin.Programs` 的 assets
  缺 `net10.0/win-x64` 目标），本地可稳定复现——与 CI `build-and-test.yml` 的 publish 步骤失败落点一致。
- **根因**：`plugins/src/*/Package.targets` 的 `ResolveBuiltIn*PluginPackage` 用 MSBuild 任务构建插件工程，
  宿主 publish 的全局属性（`RuntimeIdentifier`/`SelfContained`/`PublishSingleFile`）随任务一并下传，
  于是按 plugins.md §2 以平台中立 TFM（`net10.0`）声明的插件工程被要求提供 RID 专属资产目标。
- **修正**：两处嵌套 MSBuild 增加
  `RemoveProperties="RuntimeIdentifier;SelfContained;PublishSingleFile"`（见 §八 提交 `c1b24af`）。

---

## 六、局限与声明

1. **单机单次会话数据**：全部数字来自同一台 Windows 10.0.19045 / .NET 10.0.1126.37416 / 16 逻辑处理器机器；
   工作集受系统内存压力、磁盘缓存与其它进程活动影响，绝对值存在运行间差异（本次同一场景重复运行偏差
   在 ±2 MB 内，见 A 与 A3 两场：134.47 / 134.74 MB）。
2. **有诊断会话的两个场景存在轻微扰动**：`dotnet-counters`/`dotnet-trace` 会在被测进程内建立事件管道缓冲
   （并让运行时在启动时挂起等待连接）。所有**工作集/专用工作集/提交量**数字取自这类运行，
   但同一场景"挂会话"（A3/B 末次）与"不挂会话"（A/B 早前各次）的内存数值一致，
   故未单独区分；唯一确定受影响的是 `DUMP` 探针那次（写转储把工作集推到 205.98 MB），该数据未用于结论。
3. **缺页类型未区分**：报告中的"缺页"为 `PageFaultCount` 累计值（含软/硬缺页）。
   要区分硬缺页需 WPR/ETW 追踪，本次未做。
4. **托管堆存活集口径**：`GCHeapStats` 在诱导回收后随事件给出的是**回收后**各代大小，
   故"修剪前堆有多大"无法从本数据直接读出；文中所有"存活堆"均指**修剪后**。
5. **手势为注入输入**：虽经项目自身 `WH_MOUSE_LL` 钩子链路处理，但与真人肌肉操作的时序
   （按压时长、抖动、轨迹平滑度）仍有差异，轮盘呈现相关峰值可能略有偏差。
6. **GC 硬顶未被压测**：256 MiB 硬顶在整场测量中远未逼近（GC 堆提交峰值 29.8 MB），
   本报告不构成"硬顶生效时行为正确"的验证；验证它需要构造病态分配场景（未做）。
7. **工作副本含未提交改动**：`StarPie.Ui` 三个文件（App/Composition/ShellHost 的测试实例退出消息）
   处于在途状态，但均不在内存路径上；§5.1 的失败项与之无关（该用例不经过 `ShellHost`）。

---

## 七、复现方式

脚手架与实验脚本在仓库内 `.scratch/memprobe/`（使用说明见其 `README.md`），原始数据在 `.scratch/memprobe/runs/`。
**`.scratch/` 是本地测量工作区、未入库**（33 MB 逐步采样 CSV 与 nettrace 不适合进仓库），
故本表的路径指向的是测量机上的产物；复制脚手架脚本到任意机器即可按下列要点重跑：

| 目录 | 内容 | 本报告章节 |
|---|---|---|
| `A_20260914_175635` / `A2_20260914_181109` / `A3_20260914_181649` | 静默冷启动（A3 含启动期 GC 事件与 `*.gc.csv`） | §4.1 |
| `B_20260914_181755` | 设置台完整生命周期（含 12 条 GC 记录） | §4.2、§4.5 |
| `C_20260914_181952` | 可见形态三轮开关（含 26 条 GC 记录） | §4.3 |
| `C_20260914_181136` | 静默后台形态对照组（无出账，`STARPIE_C_BACKGROUND=1`） | §4.3 |
| `D_20260914_182112` / `E_20260914_180932` / `F_20260914_182651` | 轮盘唤出三次 / 残留回收 / 连续十次收敛 | §4.4 |
| `DUMP_182217/sos.txt` | `dotnet-dump` + SOS `eeversion`/`eeheap -gc`/`gcheapstat`（Workstation GC、单堆、存活堆真值） | §2.4、§4.2 |
| `runs/*/counters*.csv` | `dotnet-counters` 1 Hz 计量（分代回收数、末次回收堆大小、堆提交、暂停时间、分配速率） | §3.3、§4.5 |

复现要点：

```powershell
# 1. 构建（框架依赖，需本机 .NET 10 运行时）
dotnet build StarPie.Ui/StarPie.Ui.csproj -c Release

# 2. 采集（.venv 内已装 pywinauto/pywin32）
$env:STARPIE_TRACE = "1"                       # 同时采逐次 GC 事件
python .scratch/memprobe/runB.py               # 亦可 runA3 / runC / runD / runE / runF

# 3. 逐次 GC 记录解析（工具须在仓库外构建，见 .scratch/memprobe/README.md）
dotnet "$env:TEMP\starpie_memprobe\GcTrace\bin\Release\net10.0\GcTrace.dll" <trace>.nettrace out.csv

# 4. 与时间线对齐阅读
python .scratch/memprobe/analyze.py .scratch/memprobe/runs/B_20260914_181755

# 5. 关键口径随时可与任务管理器/性能监视器对照
Get-Counter "\Process(StarPie)\Working Set"                 # 总工作集
Get-Counter "\Process(StarPie)\Working Set - Private"       # 专用工作集（任务管理器「内存」列）
(Get-Process StarPie).PrivateMemorySize64 / 1MB             # 提交量（任务管理器「提交大小」）
```

*报告完 — 全部结论均可由 `.scratch/memprobe/runs/` 下的原始 CSV 与 `*.gc.csv` 逐行复核。*

---

## 八、修正记录（本报告结论落地）

| 提交 | 类型 | 内容 | 验证 |
|---|---|---|---|
| `c1b24af` | chore | 插件打包嵌套构建不再继承宿主 RID：两处 `plugins/src/*/Package.targets` 的嵌套 MSBuild 增加 `RemoveProperties="RuntimeIdentifier;SelfContained;PublishSingleFile"`（§5.7） | 自包含单文件发布由 `NETSDK1047` 失败转为通过（`StarPie.exe` + `plugins/starpie.builtin.*` 就位）；普通 build 的插件落点不变 |
| `2bec976` | test | `NavigationSuspensionTests.插件页VM随出账真实回收_无静态根` 与栈上强引用解耦：导航→出账→丢引用移入独立方法、只返回弱引用（§5.1） | 全量 xUnit **Release 980/980**（修复前 979/980）、Debug 980/980 |
| `740d17b` | docs | 硬顶生效值口径修正：`shell.md` 写清"声明面/产物双断言 + Debug 构建可见的运行时生效值日志 + 正式版以产物 runtimeconfig.json 为准"，并同步修正 ShellHost / App.xaml.cs / MemoryResidencyTests 三处注释（§5.2） | 文档与注释改动，无行为变化；构建与全量 xUnit 同上门 |

说明：

- 三项改动均未命中 `docs/agents/git-commits.md` 的 e2e 必跑表（分别为构建目标、测试自身、文档与注释），
  故按免跑判定执行；数值类结论仍以本报告的实测数据为准。
- §5.3~§5.6 是"应写清楚/应记录"的建议，未改代码：其中 §5.3（轮盘首三次成本）与 §5.5（工作集口径说明）
  建议随下一次动 `shell.md` 时一并落笔。
- 修正记录只覆盖本轮实测发现的三个问题；CI 侧还需一次推送才能确认 publish 步骤真的转绿
  （`#158` 之后尚无 CI 记录，见 §5.1 旁证）。
