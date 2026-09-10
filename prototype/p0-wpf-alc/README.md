# P0 打样：collectible ALC 中的 WPF 插件能否被真正卸载

> **性质**：一次性打样（throwaway）。产物只服务于一个决策：P3「插件 UI 宿主」的规模，乃至「UI 插件」这条路是否保留。
> **上游**：[issue #108](https://github.com/Leaveblanket/StarPie/issues/108)（父 issue）、`docs/adr/0027`/`0028`/`0029`、`docs/architecture/plugins.md`。
> **归属**：本目录不进产品代码树，只在 `prototype/p0-wpf-alc` 分支存活；结论回填 `docs/architecture/plugins.md`。

## 1. 一条命令跑完

```bash
./run.sh          # Linux/macOS/Git Bash
run.cmd           # Windows
```

脚本做三件事：Release 构建插件包与宿主 → 跑 13 项探针 → 输出 `p0-report.md`（逐探针原始证据 + 归因诊断）。

> **本机环境坑（与打样无关，但会挡住所有 dotnet 命令）**：本会话的 shell 环境缺少 Windows 的 `APPDATA` 与 `ProgramFiles` 变量，NuGet 在 `NuGetEnvironment.CalculateFolderPath` 里做 `Path.Combine(null, …)` 直接抛 `Value cannot be null. (Parameter 'path1')`，表现为**全机器 restore 失败**（连 `dotnet new classlib` 都构建不了）。先补齐再跑：
>
> ```bash
> export APPDATA='C:\Users\<user>\AppData\Roaming' ProgramFiles='C:\Program Files'
> ```

## 2. 打样怎么搭的

对齐目标态的三集形态，只保留必需的部分：

| 工程 | 模拟目标态 | 关键约束 |
|---|---|---|
| `src/P0.Contracts` | `StarPie.Sdk` / `StarPie.Sdk.Wpf` | 默认 ALC 加载；插件以 `ExcludeAssets="runtime"` 引用（包内不得出现契约副本） |
| `src/P0.Plugin` | 第三方 UI 插件包 | 只带自己的 dll + deps.json；输出版本目录里没有 `P0.Contracts.dll`（已断言） |
| `src/P0.Host` | 宿主内核 + `PluginHosting` | STA harness、collectible `PluginLoadContext`、资产登记表、全局根扫描、泄漏判定、报告 |

判定口径与 `plugins.md` §8 完全一致，未做任何放宽：

```text
StopAsync → UI 线程清理（容器/窗口/资源根/句柄）→ 断言登记表清零
→ GC × 4 → WeakReference(ALC/程序集/entry/资产/插件委托) 判定
→ ALC.Unload() → GC × 4 → 二次判定
```

13 项探针 = 8 项白名单条目（视图/窗口/资源字典/DataTemplate/定时器/动画/事件订阅/绑定）
+ BAML·pack URI 两态对照（有/无 `EnterContextualReflection`）
+ 3 项负对照（插件静态缓存 / 绕过契约直并全局资源 / 插件自建 `DependencyProperty`）。

## 3. 结论

### 3.1 一句话

**插件 UI 资产可以被宿主清干净，但插件程序集卸载不了。**因此 UI 插件不能承诺 ALC 真卸载，只能承诺「托管清理 + 可验证 + 泄漏隔离 + 重启生效」。

### 3.2 受支持特性白名单（首版，实测口径）

| 特性 | 资产清理（登记表清零 + 对象回收） | ALC 真卸载 |
|---|---|---|
| 插件视图（XAML UserControl） | 受支持 | **不支持** |
| 插件窗口（XAML Window） | 受支持（Close + 等 Closed） | **不支持** |
| 插件资源字典（pack URI 并入插件资源根） | 受支持（整根摘除） | **不支持** |
| 插件 DataTemplate | 受支持 | **不支持** |
| 宿主签发 DispatcherTimer | 受支持（Stop + 摘回调） | **不支持** |
| 宿主中介动画（Storyboard） | **有条件支持**：必须 `Storyboard.Remove(element)`，只 `Stop` 会残留 1 个存活对象 | **不支持** |
| 宿主中介事件订阅 | 受支持（宿主吊销即断，订阅计数回 0） | **不支持** |
| 插件 Binding | 受支持（ClearBinding + 清 DataContext） | **不支持** |

### 3.3 不支持列表

- **任何触及 WPF 的插件都不可热卸载**：只要插件程序集里的类型或 BAML 资源被 WPF 碰过（进视觉树、被模板套用、被资源查找、被依赖属性/类型描述符缓存），该程序集就再也回收不了——实测清空全部可定位的 WPF 全局缓存后仍然如此。
- **物理上界**：把**纯 POCO**（无 XAML、不进视觉树）放进宿主容器，就足以钉住插件程序集。
- **松散 XAML（`XamlReader.Parse` + `assembly=` 类型引用）**：不稳定，不纳入支持面。插件 XAML 必须走编译期 BAML。
- **`Application.LoadComponent(绝对 pack URI)`**：.NET Core 下抛 `无法使用绝对 URI`。宿主合并插件资源字典必须用 `new ResourceDictionary { Source = packUri }`。
- **`EnterContextualReflection` 包裹 XAML 解析**：不需要，且有害。进入上下文反射域后 `XamlReader.Parse` 触发的 `InitializeComponent` 无法再解析自身的 BAML 资源（`组件不具有由 URI 识别的资源`）。
- **`AssemblyDependencyResolver`**：与卸载无关，实测（无 resolver 变体）不构成泄漏源，但**本打样不构成「可以不校准 `AssemblyDependencyResolver`」的依据**。

### 3.4 归因证据（`p0-report.md` §4）

只装载程序集（resolver / 无 resolver / 纯 ALC 三种变体）都能回收；一旦插件类型或 BAML 资源被 WPF 触碰就不行。扫描 6177 个 WPF/System.Xaml/BCL 类型的 19334 个静态字段，定位到 5 处全局缓存：

```text
System.Windows.SystemResources._dictionaries[key]                     → Assembly(P0.Plugin)
MS.Internal.PresentationCore.SafeSecurityHelper._assemblies[key]      → Assembly(P0.Plugin)
MS.Internal.WindowsBase.SafeSecurityHelper._assemblies[key]           → Assembly(P0.Plugin)
System.Windows.DependencyObjectType.DTypeFromCLRType[key]             → Type(P0.Plugin.Views.ProbeView)
System.ComponentModel.ReflectTypeDescriptionProvider.s_attributeCache  → Type(P0.Plugin.Models.ProbeVm)
System.ComponentModel.TypeDescriptor.s_defaultProviderInitialized      → Type(P0.Plugin.Models.ProbeVm)
```

用私有反射把这 5 处全部清空（实验 J/K/N）后：扫描命中归零，**但 ALC 与程序集依旧存活** → 存在至少一个未能定位的 Assembly/Type 级根。因此「靠清缓存换真卸载」这条路在支持手段内不成立。

### 3.5 对设计的影响（P3 输入）

1. **承诺分级落地**（ADR-0028 决策 6 已被证实是经验事实）：headless 插件真卸载；UI 插件只承诺托管清理 + 可验证 + 泄漏隔离。
2. **「重载」对 UI 插件没有意义**：更新 UI 插件 = 隔离旧实例 + 装载新版本会同时留下两个程序集版本；必须改为「下次启动生效」。
3. **资产登记表 + 全局根扫描必须保留**：负对照证明扫描能抓到「绕过契约直并 `Application.Current.Resources`」的插件（命中并摘除后对象即回收），这是契约之外唯一的兜底手段。
4. **插件静态缓存这类根扫描扫不到**（负对照 1 实测 1 个对象残留）：只有 WeakReference 判定 + 隔离才能兜住。
5. `StarPie.Ui/PluginHosting` 的定时器/动画/订阅注册器要按上表的「有条件支持」实现（动画必须 `Remove`）。

## 4. 复现要点

- **必须 Release + 工作站 GC**（`ServerGarbageCollection=false`），Debug 下 JIT 局部变量生命周期会给出假泄漏。
- 探针的 `WeakReference` 判定与插件对象创建拆在不同 `[MethodImpl(MethodImplOptions.NoInlining)]` 帧里，避免局部变量留活引用。
- 每个探针一个全新 collectible ALC，互不污染；探针内的 `ProbeAssetKind` 与宿主登记表一一对应。

## 5. 目录

```text
prototype/p0-wpf-alc/
├── README.md            # 本文（结论 + 白名单 + 复现要点）
├── p0-report.md         # 生成物：13 项探针逐条原始证据 + 归因诊断
├── run.sh / run.cmd     # 一条命令跑完
└── src/
    ├── P0.Contracts/    # 共享契约（模拟 Sdk / Sdk.Wpf）
    ├── P0.Plugin/       # 插件包（XAML 视图/窗口/字典 + 13 项探针）
    └── P0.Host/         # STA harness + collectible ALC + 登记表 + 泄漏验证 + 报告
```
