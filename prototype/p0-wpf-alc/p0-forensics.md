# P0 追加取证：卸载根的 SOS 定位与「穷举清缓存」实验

> **性质**：`p0-report.md` 的追加取证（手工维护，非生成物）。回答两个问题：「扫描命中归零但 ALC 仍存活」的那个根到底是什么；「靠清缓存换真卸载」能不能成立。
> **上游**：`README.md` §3.4、ADR-0030、`docs/architecture/plugins.md` §5.2。

## 1. 复跑（排除环境因素）

- 健康环境（完整 Windows 环境变量；.NET SDK 10.0.400 / WindowsDesktop 运行时 10.0.11；Windows 10 Pro 22H2 19045）下重建并全量复跑 13 项探针。
- 结果与已提交的 `p0-report.md` **逐字一致**（0/13 可卸载；差异仅输出路径与扫描耗时毫秒数）。
- 上一会话的受限 shell（缺 `APPDATA` 导致 NuGet 全线失败、命令被安全守卫拦截、代理不通）只卡住了流程，未影响结论。

## 2. 取证方法（可复现）

按微软官方调试配方（[unloadability #debug-unloading-issues](https://learn.microsoft.com/dotnet/standard/assembly/unloadability#debug-unloading-issues)）：

```bash
dotnet build src/P0.Host/P0.Host.csproj -c Release
dotnet src/P0.Host/bin/Release/net10.0-windows/P0.Host.dll --diag J --hold 180   # 单用例诊断后保持存活
dotnet-dump collect -p <pid> -o p0-j.dmp
dotnet-dump analyze p0-j.dmp -c "dumpheap -type LoaderAllocator" -c "gcroot <addr>" -c "exit"
```

`--diag <用例前缀> --hold <秒>` 与 `RootFinder.PurgeBamlTypeTable` / `RootFinder.PurgeReachable` 均为**不受支持的私有反射**，只用于打样取证，不属于目标态设计。

## 3. 逐轮结果：清缓存换不回卸载

| 轮次 | 追加清除（均成功移除） | ALC | 转储新暴露的根 |
|---|---|---|---|
| 0（原打样） | 原 5 处 WPF/BCL 全局缓存（实验 J/K/N） | 存活 | 扫描 0 命中 |
| 1 | `WpfSharedBamlSchemaContext._masterTypeTable`（共享 BAML 类型表） | 存活 | `ResourceManagerWrapper` 注册表（持插件程序集）、`DTypeMap`（持插件类型） |
| 2 | `ResourceContainer.s_registeredResourceManagers` | 存活 | `GlobalEventManager` 的 DTypeMap |
| 3 | `DataBindEngine._accessorTable`；嵌套容器清扫 | 存活 | `PreloadedPackages` / `ResourceContainer` 打包层缓存链 |
| 4 | `GlobalEventManager._dTypedClassListeners._activeDTypes.List` | 存活 | 见 §4 |

（第 3 轮起 `PurgeReachable` 修复了「定长数组不能用 RemoveAt」的坑，才能清掉 `DependencyObjectType[]` 里的槽位。）

## 4. 清完 8 处缓存后的残余根（第 4 份转储）

```text
HandleTable:
    00000197555413e8 (strong handle)
      -> System.Object[]（静态存储）
      -> System.Collections.Specialized.HybridDictionary
      -> System.Collections.Specialized.ListDictionary
      -> System.Collections.Specialized.ListDictionary+DictionaryNode
      -> MS.Internal.IO.Packaging.PreloadedPackages+PackageThreadSafePair
      -> MS.Internal.AppModel.ResourceContainer
      -> System.Collections.Generic.SortedList<PackUriHelper+ValidatedPartUri, PackagePart>
      -> System.IO.Packaging.PackagePart[]
      -> MS.Internal.AppModel.ResourcePart
      -> System.Collections.Generic.List<System.IO.Stream>
      -> System.IO.Stream[]
      -> MS.Internal.AppModel.BamlStream
      -> System.Reflection.RuntimeAssembly（插件程序集）
      -> System.Reflection.LoaderAllocator

    0000019755542fe0 (10)  -> System.RuntimeType -> LoaderAllocator
    0000019755542fe8 (10)  -> System.RuntimeType -> LoaderAllocator
    0000019755542ff8 (10)  -> System.RuntimeType -> LoaderAllocator
```

- 打包层缓存直接持有插件程序集的 BAML 流；`(10)` 类句柄指向插件的 `RuntimeType`，属运行时内部持有，托管代码无从释放。
- 每清一层，`gcroot` 就暴露出下一层——层数不收敛，且最终残余不在托管可清理面内。

## 5. 结论

1. 根已定位：不是单个隐藏全局变量，而是**多层 WPF 内部缓存 + 运行时内部句柄**的组合；各层相互独立，「清掉一层」不会让卸载条件成立。
2. 「清缓存换真卸载」在支持手段内不成立：穷举清除所有可达的托管缓存后，残余仍包含无法释放的运行时句柄。
3. 因此维持 ADR-0030 的降级语义（UI 插件不承诺程序集回收；更新 = 隔离旧版本 + 下次启动生效）；「P3 之前再试一次」的待办就此关闭，仅当 .NET/WPF 提供受支持的缓存失效或 ALC 卸载 API 时另行评估。
