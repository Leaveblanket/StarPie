# 模块：导航

> 本文是 [docs/architecture.md](../architecture.md) 的拆分文档；新增/修改设置页导航时读本篇。

## 职责

设置控制台页面切换；页面状态常驻、View 按导航重建。

## 组成文件

- `Services/Navigation/`：`NavigationStore`、`INavigationService<T>`/`NavigationService<T>`。
- `ViewModels/Navigation/`：`MainViewModel`（主归属 S5；壳层成员 `WindowTitle`/`IsExiting`/`Save()` 按
  [modules.md](modules.md) §5 D3 借调 M5）、`NavigationItemViewModel`。
- `Views/Navigation/MainView.xaml`（R4：文件级归 S5，DataTemplate 映射；`MainView.xaml.cs` 壳层
  code-behind 见 [shell.md](shell.md)）。

## 关键流程

1. `MainViewModel` 构造 `NavigationItemViewModel` 列表：`AutomationId`、`TitleKey`（I18n 键）、`IconData`、`TargetViewModelType`、导航 `Action`。
2. 点击导航项 → `NavigationService<T>.Navigate()` → `NavigationStore.CurrentViewModel = 容器解析的页面 VM`（单例 → 状态常驻）。
3. `MainView` 的 `ContentControl` `Content="{Binding CurrentViewModel}"`，由 `MainView.Resources` 中 `DataTemplate DataType="{x:Type vm:Xxx}"` 映射到 `{Page}Page`（View 无参、按导航重建、不经容器）。
4. 托盘直达 = 同一 `INavigationService<T>.Navigate()` + `MainView.ShowAndActivate()`（`Composition.NavigateAndShow`，见 [host.md](host.md)）。

## 扩展点

新增页面 = 页面 VM + `INavigationService<T>` 字段（组合根解析）+ `MainViewModel` 导航项 + `MainView.xaml` DataTemplate + [naming.md](naming.md) 映射表登记；任何一步缺失都算未完成。完整清单见 [extending.md](extending.md)（原型 B）。

## 参见 ADR

[0005](../adr/0005-di-container-for-navigation.md)（DI 导航）。
