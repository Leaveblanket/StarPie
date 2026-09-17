# StarPie 架构文档(入口)

> 当前规范分散在 3 片叶子里;按问题找对叶子。代码结构变化直接同步对应叶子,
> 不再维护本文与叶子以外的正典。

## 路由

| 关心什么 | 读哪片 |
|---|---|
| 谁可以引用谁 / VM/Service/Model/View 边界 / DI 解析点 | [layering.md](architecture/layering.md) |
| 程序集划分 / 依赖方向 / DI 注册管线 / 导航五槽 | [assemblies.md](architecture/assemblies.md) |
| 插件发现/装载/卸载/能力/UI 托管/ABI 与 ALC 政策 | [plugins.md](architecture/plugins.md) |

## 技术栈

- .NET 10 / WPF(`net10.0-windows10.0.19041.0`、`UseWPF`)
- 形态:`StarPie.Sdk` / `StarPie.Host` / `StarPie.Sdk.Wpf` + `StarPie.Ui`(WinExe,程序集名 `StarPie`) + 能力插件
- `CommunityToolkit.Mvvm`(MVVM 唯一框架)
- `Microsoft.Extensions.DependencyInjection`(仅组合根 `Composition.cs`)
- 本地化:`Strings*.resx`(zh-CN + zh-TW/en/ja 卫星),`VocaDb.ResXFileCodeGenerator` 强类型
- xUnit v3(`StarPie.Tests`;范围见 [adr/0050-test-scope.md](adr/0050-test-scope.md))
- e2e:pywinauto(`tests/`)

## 维护

新增/修改规范只动这三片叶子对应的那片,不要新增第四片。
