# P0 打样报告：collectible ALC 中的 WPF 插件能否真正卸载

> 由 `prototype/p0-wpf-alc` 的宿主一键生成（`run.cmd` / `./run.sh`）。
> 判定口径与 `docs/architecture/plugins.md` §8 一致：资产登记表清零 + 全局根扫描 + WeakReference + GC。

## 1. 环境与包约束

- 插件包目录：`D:\Project\.net\StarPie\prototype\p0-wpf-alc\src\P0.Plugin\bin\Release\net10.0-windows`
- 包内文件：`P0.Plugin.deps.json`、`P0.Plugin.dll`、`P0.Plugin.pdb`
- 包内 `P0.Contracts.dll` 副本数（§5.1 约束 3，期望 0）：**0**
- ServerGC：False；GC LatencyMode：Interactive

## 2. 判定汇总

| 探针 | 维度 | 上下文反射 | cleanup 后残留 | cleanup 后对象存活 | Unload 后 ALC | Unload 后程序集 | 判定 |
|---|---|---|---|---|---|---|---|
| `baml-noctx` | BAML / pack URI（对照：无上下文反射） | 否 | 0 | 0/2 | 存活 | 存活 | 未回收 |
| `baml-ctx` | BAML / pack URI（EnterContextualReflection） | 是 | 0 | 0/2 | 存活 | 存活 | 未回收 |
| `view` | 插件视图（XAML UserControl） | 是 | 0 | 0/2 | 存活 | 存活 | 未回收 |
| `window` | 插件窗口（XAML Window） | 是 | 0 | 0/2 | 存活 | 存活 | 未回收 |
| `resdict` | 插件资源字典 | 是 | 0 | 0/2 | 存活 | 存活 | 未回收 |
| `template` | 插件 DataTemplate | 是 | 0 | 0/3 | 存活 | 存活 | 未回收 |
| `timer` | 宿主签发 DispatcherTimer | 是 | 0 | 0/2 | 存活 | 存活 | 未回收 |
| `animation` | 宿主中介动画（Storyboard） | 是 | 0 | 0/2 | 存活 | 存活 | 未回收 |
| `subscription` | 宿主中介事件订阅 | 是 | 0 | 0/3 | 存活 | 存活 | 未回收 |
| `binding` | 插件 Binding | 是 | 0 | 0/2 | 存活 | 存活 | 未回收 |
| `neg-static-cache` | 负对照：插件静态缓存 | 是 | 0 | 1/2 | 存活 | 存活 | 未回收 |
| `neg-app-resources` | 负对照：绕过契约直并全局资源 | 是 | 2（已隔离摘除） | 0/1 | 存活 | 存活 | 未回收 |
| `neg-dependency-property` | 负对照：插件自建 DependencyProperty | 是 | 0 | 0/2 | 存活 | 存活 | 未回收 |

## 3. 逐探针明细

### baml-noctx — BAML / pack URI（对照：无上下文反射）

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - view/baml-view: ProbeView 已进宿主容器
  - 登记表: 登记=1 已创建=1 句柄=0 插件窗口=0
  - 插件回执 packuri-merge=ok
  - 插件回执 loose-xaml=ok: StackPanel
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 未使用 EnterContextualReflection（对照）
  - 宿主事件订阅数（cleanup 后）=0

### baml-ctx — BAML / pack URI（EnterContextualReflection）

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - view/baml-view: ProbeView 已进宿主容器
  - 登记表: 登记=1 已创建=1 句柄=0 插件窗口=0
  - 插件回执 packuri-merge=ok
  - 插件回执 loose-xaml=fail: XamlParseException: “对类型“P0.Plugin.Views.ProbeView”的构造函数执行符合指定的绑定约束的调用时引发了异常。”，行号为“5”，行位置为“3”。 <- Exception: 组件“P0.Plugin.Views.ProbeView”不具有由 URI“/P0.Plugin;component/views/probeview.xaml”识别的资源。
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0

### view — 插件视图（XAML UserControl）

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - view/view: ProbeView 已进宿主容器
  - 登记表: 登记=1 已创建=1 句柄=0 插件窗口=0
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0

### window — 插件窗口（XAML Window）

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - window/window: shown=True loaded=True
  - 登记表: 登记=1 已创建=1 句柄=0 插件窗口=1
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=3
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0
- 全局根残留（cleanup 前）：
  - [cleanup 前] 命中 1 条全局根残留
  - Application.Windows[ProbeWindow] → P0.Plugin.Views.ProbeWindow

### resdict — 插件资源字典

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - 登记表: 登记=1 已创建=1 句柄=0 插件窗口=0
  - 插件回执 resdict-uri=ok
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 资源字典已走 pack URI 路径，跳过工厂回退
  - 宿主事件订阅数（cleanup 后）=0

### template — 插件 DataTemplate

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：3（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - template/template: DataType=ProbeVm 内容已套用
  - 登记表: 登记=1 已创建=2 句柄=0 插件窗口=0
  - 插件回执 template-uri=ok
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0

### timer — 宿主签发 DispatcherTimer

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - 登记表: 登记=0 已创建=0 句柄=1 插件窗口=0
  - 插件回执 timer-ticks=11
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0

### animation — 宿主中介动画（Storyboard）

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - animation/animation: 已在宿主元素上 Begin
  - 登记表: 登记=1 已创建=1 句柄=0 插件窗口=0
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=0.37
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0

### subscription — 宿主中介事件订阅

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：3（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - subscription/subscription-handle: 句柄已登记
  - 登记表: 登记=1 已创建=1 句柄=1 插件窗口=0
  - 插件回执 event-last=ping
  - 宿主事件订阅数（运行中）=1
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0

### binding — 插件 Binding

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - binding/binding: 已挂到宿主 TextBlock
  - 登记表: 登记=1 已创建=1 句柄=0 插件窗口=0
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='bound' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0

### neg-static-cache — 负对照：插件静态缓存

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 1，程序集存活 True
- Unload 后：对象存活 1，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - view/neg-static-view: ProbeView 已进宿主容器
  - 登记表: 登记=1 已创建=1 句柄=0 插件窗口=0
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0

### neg-app-resources — 负对照：绕过契约直并全局资源

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=True Unload=True
- 对象探针数：1（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - 登记表: 登记=0 已创建=0 句柄=0 插件窗口=0
  - 插件回执 bypass=直接并入全局资源：P0.Plugin.Dictionaries.ProbeDictionary（宿主登记表不可见）
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0
  - 隔离摘除：Application.Current.Resources.MergedDictionaries 移除插件字典 1 个
- 全局根残留（cleanup 前）：
  - [cleanup 前] 命中 1 条全局根残留
  - Application.Current.Resources.MergedDictionaries[0] → P0.Plugin.Dictionaries.ProbeDictionary
- 全局根残留（cleanup 后）：
  - [cleanup 后] 命中 1 条全局根残留
  - Application.Current.Resources.MergedDictionaries[0] → P0.Plugin.Dictionaries.ProbeDictionary

### neg-dependency-property — 负对照：插件自建 DependencyProperty

- 流程：Configure=True Materialize=True Stop=True Cleanup=True 隔离摘除=False Unload=True
- 对象探针数：2（entry + 资产 + 插件委托）
- cleanup 后：对象存活 0，程序集存活 True
- Unload 后：对象存活 0，程序集存活 True，ALC 存活 True，entry 存活 False
- 运行观测：
  - view/neg-dp-view: ProbeControl 已进宿主容器
  - 登记表: 登记=1 已创建=1 句柄=0 插件窗口=0
  - 宿主事件订阅数（运行中）=0
  - ContentHost 视觉子元素=1
  - BindingTarget.Text='' AnimationTarget.Opacity=1.00
  - Application.Windows=2
  - ALC 私有程序集=P0.Plugin
- 宿主备注：
  - 已进入 EnterContextualReflection 作用域
  - 宿主事件订阅数（cleanup 后）=0

## 4. ALC 生命周期归因（逐变量隔离）

```text
=== ALC 生命周期诊断（逐变量隔离） ===
A 仅装载（resolver + Load覆写）           alc=已回收 asm=已回收 entry=- assets=0/0 
B 仅装载（无 resolver）                  alc=已回收 asm=已回收 entry=- assets=0/0 
C 仅装载（纯 ALC，无覆写）                   alc=已回收 asm=已回收 entry=- assets=0/0 
D 插件 POCO（不进宿主容器）                  alc=已回收 asm=已回收 entry=已回收 assets=0/1 assets=View
D2 插件 POCO（进宿主容器）                  alc=存活 asm=存活 entry=已回收 assets=0/1 assets=View
      root: System.Windows.SystemResources._dictionaries[key] → Assembly(P0.Plugin)
      root: System.ComponentModel.ReflectTypeDescriptionProvider.s_attributeCache[key] → Type(P0.Plugin.Models.ProbeVm)
      root: System.ComponentModel.TypeDescriptor.s_defaultProviderInitialized[key] → Type(P0.Plugin.Models.ProbeVm)
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=205ms 命中=3
E BAML 字典（只含框架类型）                  alc=存活 asm=存活 entry=已回收 assets=0/0 （无资产）
      root: System.Windows.SystemResources._dictionaries[key] → Assembly(P0.Plugin)
      root: MS.Internal.PresentationCore.SafeSecurityHelper._assemblies[key] → Assembly(P0.Plugin)
      root: System.ComponentModel.ReflectTypeDescriptionProvider.s_attributeCache[key] → Type(P0.Plugin.Models.ProbeVm)
      root: System.ComponentModel.TypeDescriptor.s_defaultProviderInitialized[key] → Type(P0.Plugin.Models.ProbeVm)
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=41ms 命中=4
F BAML 字典（含插件类型）                   alc=存活 asm=存活 entry=已回收 assets=0/0 （无资产）
      root: System.Windows.SystemResources._dictionaries[key] → Assembly(P0.Plugin)
      root: MS.Internal.PresentationCore.SafeSecurityHelper._assemblies[key] → Assembly(P0.Plugin)
      root: MS.Internal.WindowsBase.SafeSecurityHelper._assemblies[key] → Assembly(P0.Plugin)
      root: System.ComponentModel.ReflectTypeDescriptionProvider.s_attributeCache[key] → Type(P0.Plugin.Models.ProbeVm)
      root: System.ComponentModel.TypeDescriptor.s_defaultProviderInitialized[key] → Type(P0.Plugin.Models.ProbeVm)
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=45ms 命中=5
G BAML 视图（UserControl）             alc=存活 asm=存活 entry=已回收 assets=0/1 assets=View
      root: System.Windows.SystemResources._dictionaries[key] → Assembly(P0.Plugin)
      root: MS.Internal.PresentationCore.SafeSecurityHelper._assemblies[key] → Assembly(P0.Plugin)
      root: MS.Internal.WindowsBase.SafeSecurityHelper._assemblies[key] → Assembly(P0.Plugin)
      root: System.Windows.DependencyObjectType.DTypeFromCLRType[key] → Type(P0.Plugin.Views.ProbeView)
      root: System.ComponentModel.ReflectTypeDescriptionProvider.s_attributeCache[key] → Type(P0.Plugin.Models.ProbeVm)
      root: System.ComponentModel.TypeDescriptor.s_defaultProviderInitialized[key] → Type(P0.Plugin.Models.ProbeVm)
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=43ms 命中=6
H BAML 窗口（Window）                  alc=存活 asm=存活 entry=已回收 assets=1/1 assets=Window
      root: System.Windows.SystemResources._dictionaries[key] → Assembly(P0.Plugin)
      root: MS.Internal.PresentationCore.SafeSecurityHelper._assemblies[key] → Assembly(P0.Plugin)
      root: MS.Internal.WindowsBase.SafeSecurityHelper._assemblies[key] → Assembly(P0.Plugin)
      root: System.Windows.DependencyObjectType.DTypeFromCLRType[key] → Type(P0.Plugin.Views.ProbeView)
      root: System.Windows.DependencyObjectType.DTypeFromCLRType[key] → Type(P0.Plugin.Views.ProbeWindow)
      root: System.ComponentModel.ReflectTypeDescriptionProvider.s_attributeCache[key] → Type(P0.Plugin.Models.ProbeVm)
      root: System.ComponentModel.TypeDescriptor.s_defaultProviderInitialized[key] → Type(P0.Plugin.Models.ProbeVm)
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=41ms 命中=7
I 视图 + 不 Unload（对照）                alc=存活 asm=存活 entry=已回收 assets=0/1 assets=View
      root: System.Windows.SystemResources._dictionaries[key] → Assembly(P0.Plugin)
      root: MS.Internal.PresentationCore.SafeSecurityHelper._assemblies[key] → Assembly(P0.Plugin)
      root: MS.Internal.WindowsBase.SafeSecurityHelper._assemblies[key] → Assembly(P0.Plugin)
      root: System.Windows.DependencyObjectType.DTypeFromCLRType[key] → Type(P0.Plugin.Views.ProbeView)
      root: System.Windows.DependencyObjectType.DTypeFromCLRType[key] → Type(P0.Plugin.Views.ProbeWindow)
      root: System.Windows.DependencyObjectType.DTypeFromCLRType[key] → Type(P0.Plugin.Views.ProbeView)
      root: System.ComponentModel.ReflectTypeDescriptionProvider.s_attributeCache[key] → Type(P0.Plugin.Models.ProbeVm)
      root: System.ComponentModel.TypeDescriptor.s_defaultProviderInitialized[key] → Type(P0.Plugin.Models.ProbeVm)
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=37ms 命中=8
      purge: System.Windows.SystemResources._dictionaries：移除 1 条
      purge: MS.Internal.PresentationCore.SafeSecurityHelper._assemblies：移除 1 条
      purge: MS.Internal.WindowsBase.SafeSecurityHelper._assemblies：移除 1 条
      purge: System.Windows.DependencyObjectType.DTypeFromCLRType：移除 4 条
      purge: System.ComponentModel.ReflectTypeDescriptionProvider.s_attributeCache：移除 1 条
      purge: System.ComponentModel.TypeDescriptor.s_defaultProviderInitialized：移除 1 条
J 视图 + 清 WPF 全局缓存                  alc=存活 asm=存活 entry=已回收 assets=0/1 assets=View
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=32ms 命中=0
K BAML 字典 + 清 WPF 全局缓存             alc=存活 asm=存活 entry=已回收 assets=0/0 （无资产）
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=48ms 命中=0
L 视图（无 resolver，有覆写）               alc=存活 asm=存活 entry=已回收 assets=0/1 assets=View
      root: System.Windows.DependencyObjectType.DTypeFromCLRType[key] → Type(P0.Plugin.Views.ProbeView)
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=33ms 命中=1
M 视图（无 resolver，无覆写）               alc=存活 asm=存活 entry=已回收 assets=0/1 assets=View
      root: System.Windows.DependencyObjectType.DTypeFromCLRType[key] → Type(P0.Plugin.Views.ProbeView)
      root: System.Windows.DependencyObjectType.DTypeFromCLRType[key] → Type(P0.Plugin.Views.ProbeView)
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=30ms 命中=2
      purge: System.Windows.DependencyObjectType.DTypeFromCLRType：移除 3 条
N 视图 + 清缓存 + 无 resolver            alc=存活 asm=存活 entry=已回收 assets=0/1 assets=View
      root: [scan] 覆盖：类型=6177 静态字段=19334 用时=28ms 命中=0

```

## 5. 说明

- `cleanup 前` 的全局根命中共存是**预期**：资产此时仍在宿主容器与全局资源里，用来证明扫描器确实看得见插件对象。
- 负对照的期望结果是**未回收**：它们证明判定器不是永远报成功。
- 插件内部静态缓存这类根不属于 WPF 全局根，扫描器扫不到——这正是 §8 要求 WeakReference 判定兜底的原因。

