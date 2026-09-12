# 插件示例：最小 headless 与最小 UI

两个可独立构建、部署、运行的最小插件，覆盖插件开发的全部入口路径。约束性契约以
[docs/architecture/plugins.md](../../docs/architecture/plugins.md) 为正典；开发者手册见
[docs/plugin-dev-handbook.md](../../docs/plugin-dev-handbook.md)。

| 示例 | 路径 | 演示内容 | 清单 id |
|---|---|---|---|
| 最小 headless | [MinimalHeadless/](MinimalHeadless/) | `IPlugin` 生命周期 + 宿主日志 | `sample.minimal.headless` |
| 最小 UI | [MinimalUi/](MinimalUi/) | `IPluginUiModule`：资源字典 + 导航页 + 双向绑定 | `sample.minimal.ui` |

## 1. 构建

两个示例都在 `StarPie.slnx` 内，构建解决方案即一起编译：

```bash
dotnet build StarPie.slnx
```

（示例只引 `StarPie.Sdk` / `StarPie.Sdk.Wpf` 且 `Private=false`——SDK 不随包分发，
运行期由宿主统一提供共享契约。）

## 2. 部署

把示例构建产物 + 仓库里的 `plugin.json` 复制到**用户插件目录**，
包目录名必须等于清单 id（校验强约束）：

```text
# 构建输出在 <repo>/plugins/samples/<示例>/bin/Debug/net10.0-windows*/
%LOCALAPPDATA%\StarPie\plugins\sample.minimal.headless\
    StarPie.Plugin.MinimalHeadless.dll
    plugin.json
%LOCALAPPDATA%\StarPie\plugins\sample.minimal.ui\
    StarPie.Plugin.MinimalUi.dll
    plugin.json
```

> dev 实例（`StarPie-Dev` 数据目录）与正式实例的用户插件目录相互隔离，见
> `docs/architecture/plugins.md` §6。

## 3. 启用与验证

1. 启动 StarPie，进入「插件」管理页。
2. 第三方来源的包默认被拒——两条启用路径（ADR-0029）：
   - **开发者模式**（本地开发）：开启管理页的开发者模式开关，确认全信任风险披露后，
     未命中清单的插件经开发者模式放行；
   - **审核清单**（发布路径）：插件 (id, version) 列入首方签名的审核清单
     （`plugins/review-catalog.json`，签名工具 `scripts/sign-review-catalog.ps1`），
     且包带可信签名或发布者指纹被 pin。
3. 管理页条目显示「活动」即装载成功；headless 示例写日志，UI 示例的侧边栏出现
   `NavPlugin_sample.minimal.ui` 导航页（标题「最小 UI 示例」）。

## 4. 卸载与移除

- 停用：管理页「停用」——安全点卸载并落停用意图；
- 彻底移除：管理页「彻底移除」（确认后清包/配置/数据/宿主状态），
  或直接删除 `%LOCALAPPDATA%\StarPie\plugins\<id>\` 目录。
