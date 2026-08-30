# 应用宿主重组：移除 StartupUri，托盘上移应用层

现状是"设置窗口即应用宿主"：`App.xaml` 的 `StartupUri="SettingsWindow.xaml"` 让框架用无参构造自动创建设置窗口，托盘在 `SettingsWindow` 构造函数里寄生创建，窗口靠拦截 `Closing` + `Hide()` 续命。MVVM 的构造注入与 `StartupUri` 不兼容。决定：移除 `StartupUri`，组合根装配全部服务（含 `ITrayIconService`）后显式创建并显示初始设置窗口；托盘生命周期归应用层；真退出由托盘菜单触发 `Application.Shutdown`。

## Consequences

- 用户可见行为不变：关闭设置窗口仍是隐藏到托盘，托盘菜单"退出"才真正退出。仅改变实现的归属（窗口自管的 `_isClosingFromTray` 标志改为应用层协调）。
- 未来读者不要把 `StartupUri` 的缺失当作遗漏"修复"回去——它会静默绕过构造注入。
