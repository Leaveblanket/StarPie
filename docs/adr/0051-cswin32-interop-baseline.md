# Win32 互操作基线:CsWin32 源生成,逐集 internal 生成,不新增手写 DllImport

**状态**:accepted

**修订**:「H.Hooks 高层钩子库」一节的否决在 ADR-0052 中被重新裁决——捕获改由 SharpHook 接管,而抑制决策、回放窗口与看门狗仍留在自研侧;本 ADR 其余条款(唯一声明面、白名单、防回流扫描)不变。

`StarPie.Ui` 与 `StarPie.Host` 合计 39 处手写 `[DllImport]`(11 个文件),形态是 .NET Framework 时代的模板:靠 `CharSet` 隐含 A/W 后缀、`POINT`/`GetCursorPos`/`RegisterWindowMessage`/`DestroyIcon` 跨文件重复声明、`Process.GetCurrentProcess().MainModule` 取主模块句柄、运行时封送 `Marshal.PtrToStructure`。决定:**引入 `Microsoft.Windows.CsWin32` 源生成作为 Win32 互操作基线**——每集各一份 `NativeMethods.txt` 声明清单(签入仓库,作为唯一声明面),生成物构建期产生、不入库;生成类型默认 `internal`,不跨集暴露,跨集公开签名不出现 Windows 类型;调用面默认只调 raw 重载(失败语义与现状一致),friendly 重载保留生成、例外使用需注明;`AllowUnsafeBlocks` 只授予 `StarPie.Ui` 与 `StarPie.Host`。迁移以「可观察行为零变化」为硬约束,`mouse_event`→`SendInput` 一类换代单独提交。三处高风险点(手工封送的安全敏感路径 / 非 blittable 结构 / 数组签名)允许保留 `DllImport`,逐条登记白名单并注释原因;防回流由 `StarPie.Tests` 的源码扫描断言承接。

## 考虑过的方案

- **维持手写 `DllImport`**:否决。声明即模板——A/W 后缀靠 `CharSet` 推测、结构体与函数跨文件重复、手工 `MarshalAs`/`Marshal.SizeOf` 自维护,与 `AnalysisLevel=latest` 的现代互操作方向背离。
- **手写 `[LibraryImport]` 原地迁移**:否决。能去掉运行时 IL 存根,但 39 处签名、结构体与重复声明仍要自己维护;CsWin32 由 win32metadata 保证签名、句柄类型与后缀正确,增量成本更低。
- **H.Hooks 高层钩子库**(只替换鼠标钩子):否决。只覆盖 39 处中的 6 处,且不提供健康检查重注册、点击回放与「只忽略自己一次注入」的粒度;把轮盘交互拦截这条每次右键都走的核心路径的语义交给第三方,风险与收益不成比例。
- **CsWin32(采纳)**:按需生成、裁剪/AOT 友好、被广泛采用;代价是 pre-1.0 依赖(锁 0.3.x 线)与生成代码进入编译(需 `AllowUnsafeBlocks`),声明处的中文注释失去落点、注释改挂调用点。
- **给 Ui/Host 设 `PlatformTarget=x64`**(CsWin32 对架构特定 API 的官方解法):本次不做。`Shell_NotifyIcon`/`SHGetFileInfo` 在 win32metadata 中标记为架构特定(诊断 PInvoke005),AnyCPU 下无法生成,改用白名单保留手写;如日后发布形态收敛到 x64(现状 CI 即 win-x64),这两条可连同其结构体一起回收进生成面。

## 后果

- 新增依赖 `Microsoft.Windows.CsWin32`(版本在 CPM 锁定);`StarPie.Sdk`/`StarPie.Sdk.Wpf`/`StarPie.Tests`/插件工程不引入。
- `NativeMethods.txt` 成为新的「声明清单」审计面:新增 API 面 = 清单加一行,不再写 `DllImport`。
- 保留 `DllImport` 的条目继续走运行时封送,白名单逐条注释;`[ComImport]` 的 COM 互操作与本基线无关,另线评估。
- 顺带收口的 A/W 歧义(`FindWindow`/`RegisterWindowMessage` 统一到 W 变体)在提交信息里注明。
- 迁移顺序:先 `MouseHook.cs` 试点验穿工程配置与门禁(warnings-as-errors / 生成代码 / e2e),再 Ui 其余文件,最后 Host。
