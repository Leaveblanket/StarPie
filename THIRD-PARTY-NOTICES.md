# 第三方许可声明

产品（`StarPie`）随发布物以动态装载形式分发下列第三方组件。分发时请连同本文件与
`licenses/` 目录一并附上——CI 的单文件发布产物同此口径（见 `.github/workflows/build-and-test.yml`）。
裁决与义务依据见 `docs/adr/0052-input-stack-sharphook.md`。

## SharpHook

- 用途：全局鼠标钩子捕获与鼠标事件注入（`StarPie.Ui`）
- 版本：8.0.0（NuGet；`Directory.Packages.props` 锁定）
- 许可：MIT
- 版权：Copyright (c) 2021 Anatoliy Pylypchuk
- 许可正文：`licenses/SharpHook-MIT.txt`
- 上游：https://github.com/TolikPylypchuk/SharpHook

## libuiohook（随 SharpHook 包分发的原生库）

- 用途：SharpHook 的 Windows 后端（`runtimes/win-*/native/uiohook.dll`）
- 版本：随 SharpHook 8.0.0 固定的上游提交
- 许可：GNU Lesser General Public License v3.0（库本体）；上游仓库同时附 GPL-3.0 全文
- 许可正文：`licenses/libuiohook-LGPL-3.0.txt`、`licenses/libuiohook-GPL-3.0.txt`
- 上游（SharpHook 使用的分支）：https://github.com/TolikPylypchuk/libuiohook

### 可替换性声明（LGPL-3.0 §4）

- libuiohook 作为**独立原生动态库**（`uiohook.dll`）随发布物分发，运行时动态装载；产品未修改其源码。
- 单文件自包含发布形态下，该库由 .NET 宿主在启动时解压到运行目录后装载。需要改用自行编译的版本时，
  替换同名的原生库文件（或使用自行构建的发布物）即可。
