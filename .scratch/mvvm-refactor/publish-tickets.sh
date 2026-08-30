#!/usr/bin/env bash
set -e
GH=/d/MyCmdTools/gh.exe
REPO=Leaveblanket/StarPie
cd /d/CSharp/StarPie
mkdir -p .scratch/mvvm-refactor/tickets
rm -f .scratch/mvvm-refactor/ticket-ids.txt
declare -A NUM ID

cat > .scratch/mvvm-refactor/tickets/01.md <<'EOF'
## Parent

#1

## What to build

引入 CommunityToolkit.Mvvm 作为唯一 MVVM 基建依赖；现有源文件按层归入 Models / ViewModels / Views / Services / Controls 文件夹（命名空间不变，纯机械移动）；新建 WinPieGestures.Tests（xUnit）并跑通冒烟测试；I18n 增加语言切换广播事件（暂无消费者）。

## Acceptance criteria

- [ ] 所有源文件归入分层文件夹，项目编译通过
- [ ] CommunityToolkit.Mvvm 已引入
- [ ] dotnet test 全绿（含至少一条冒烟测试）
- [ ] I18n 语言切换广播事件存在，现有行为不变
- [ ] 应用启动、手势、设置、托盘行为与迁移前一致

## Blocked by

None — can start immediately.（原生依赖边已在本 issue 上声明）
EOF

cat > .scratch/mvvm-refactor/tickets/02.md <<'EOF'
## Parent

#1

## What to build

新建组合类集中装配全部依赖；移除应用启动时自动创建设置窗口的默认机制，由组合根显式创建并显示初始设置窗口；托盘从设置窗口构造中上移到组合根，生命周期归应用层；真退出改走应用级关停。见 ADR-0003。

## Acceptance criteria

- [ ] 启动自动创建机制已移除，设置窗口由组合根创建
- [ ] 托盘不再由设置窗口创建，双击与菜单行为不变
- [ ] 关闭设置窗口 = 隐藏到托盘；托盘退出 = 真退出，行为与重组前一致
- [ ] 单实例互斥与开发实例并存行为不变
- [ ] 手动冒烟：启动 → 关窗 → 托盘重现 → 托盘退出，全链路正常

## Blocked by

01 基建：工具库引入与分层重组
EOF

cat > .scratch/mvvm-refactor/tickets/03.md <<'EOF'
## Parent

#1

## What to build

定义配置服务接口（加载、保存、按前台进程取配置方案、全局方案兜底）并由实现类接管文件读写；注册进组合根；手势链路与应用启动侧调用方切换为注入；配置服务获得第一批单元测试。静态配置管理器暂保留，供尚未迁移的设置窗口继续使用（expand 阶段，见 ADR-0002）。

## Acceptance criteria

- [ ] 配置接口与实现落地，手势链路与应用侧不再直接读静态配置
- [ ] 单元测试覆盖：正常加载、文件缺失建默认、损坏 JSON 兜底、保存往返
- [ ] config.json 格式与路径不变，存量配置直接可用
- [ ] 立即生效语义保持，应用行为不变

## Blocked by

02 组合根与宿主重组
EOF

cat > .scratch/mvvm-refactor/tickets/04.md <<'EOF'
## Parent

#1

## What to build

鼠标钩子转为适配层，输出无任何 UI 框架类型的事件参数；把手势判定逻辑抽成无 WPF 依赖的纯状态机（按下等待阈值 → 激活 → 拖动选择 → 松开执行/取消，含转义状态）；前台窗口进程检测与全屏检测合并为可注入的窗口上下文接口；定义轮盘工厂接口，首版实现暂包装现有轮盘窗口；状态机经工厂与轮盘交互。

## Acceptance criteria

- [ ] 钩子事件参数不含任何 UI 框架类型
- [ ] 手势状态机不引用 WPF 类型，可在纯测试进程构造与驱动
- [ ] 状态迁移全覆盖测试：阈值触发、方向选择、转义、取消、前台进程方案匹配、全屏禁用、修饰键禁用
- [ ] 手势手感与重构前一致（触发阈值、选中高亮、执行与取消）

## Blocked by

03 配置服务缝（expand）
EOF

cat > .scratch/mvvm-refactor/tickets/05.md <<'EOF'
## Parent

#1

## What to build

轮盘窗口视图状态迁入 ViewModel（选中扇区、转义状态、扇区集合、圆心坐标）；四个风格渲染器与基类改为从 ViewModel 读状态，绘制代码留在视图层；轮盘工厂切换为创建 ViewModel 化窗口；手势状态机驱动 ViewModel 而非直接调用窗口方法。

## Acceptance criteria

- [ ] 四种皮肤（经典环、纯净扇区、毛玻璃、猫爪）渲染结果与迁移前一致
- [ ] 选中高亮与外圈转义提示行为不变
- [ ] 轮盘 ViewModel 状态逻辑有单元测试
- [ ] 手势全链路手动冒烟通过（钩子 → 状态机 → ViewModel → 渲染 → 执行）

## Blocked by

04 手势引擎抽取
EOF

cat > .scratch/mvvm-refactor/tickets/06.md <<'EOF'
## Parent

#1

## What to build

定义对话框服务并落地打样（设计见 ADR-0004）：每类对话框一个具名方法、同步签名、可空结果对象（取消与无效统一返回 null）、验证回调由调用方传入；Owner 采用惰性回填化解服务与窗口的循环依赖；注册进组合根。程序选择窗口完整 ViewModel 化（扫描编排、搜索过滤、选择结果），垃圾可执行过滤、跨源去重、显示名升级提炼为纯函数并测试。设置窗口的程序选择调用点切换到服务。服务实现对其余对话框暂用旧窗口（迁移期混装，接口不变）。

## Acceptance criteria

- [ ] 设置内添加/更换程序走 ViewModel 化链路，扫描与过滤行为不变
- [ ] 纯函数单测覆盖：卸载器/安装器/更新器等垃圾过滤、跨源去重、名称升级
- [ ] 程序选择 ViewModel 有单元测试（mock 对话框服务与扫描编排）
- [ ] 对话框服务 Owner 回填正确，对话框模态归属设置窗口
- [ ] 管线打样结论记录：基建 → 组合根 → 服务 → ViewModel → 测试 全程可用

## Blocked by

02 组合根与宿主重组
EOF

cat > .scratch/mvvm-refactor/tickets/07.md <<'EOF'
## Parent

#1

## What to build

输入对话框（标题/提示/默认值/验证回调）与系统文件对话框经对话框服务提供 ViewModel 化实现；设置窗口相应调用点切换；取消与无效输入统一返回 null 的语义全调用方一致。

## Acceptance criteria

- [ ] 输入框验证回调行为与迁移前一致，取消与无效输入返回 null
- [ ] 文件对话框过滤参数透传行为不变
- [ ] 对应 ViewModel 有单元测试
- [ ] 服务实现不再直接创建输入对话框与文件对话框的旧窗口

## Blocked by

06 对话框服务与程序选择器打样
EOF

cat > .scratch/mvvm-refactor/tickets/08.md <<'EOF'
## Parent

#1

## What to build

图标选择器（按当前图标键、返回新键）、颜色选择器（按初始色、返回色值）、屏上取色器（全屏置顶、不使用 Owner）经对话框服务提供 ViewModel 化实现；设置窗口相应调用点全部切换。

## Acceptance criteria

- [ ] 三种对话框行为与迁移前一致
- [ ] 取色器全屏置顶行为不变（Owner 语义由服务实现内部处理，不进接口）
- [ ] 对应 ViewModel 有单元测试
- [ ] 服务实现对全部对话框不再创建旧窗口

## Blocked by

06 对话框服务与程序选择器打样
EOF

cat > .scratch/mvvm-refactor/tickets/09.md <<'EOF'
## Parent

#1

## What to build

主题管理器转为可注入服务（当前主题状态、把主题应用到窗口、跟随系统）；注册进组合根；设置页与各窗口的主题调用切换为服务。

## Acceptance criteria

- [ ] 深/浅/跟随系统/自定义主题切换行为不变
- [ ] 主题服务状态迁移有单元测试
- [ ] 静态主题管理器删除（本票内完成收缩）

## Blocked by

02 组合根与宿主重组
EOF

cat > .scratch/mvvm-refactor/tickets/10.md <<'EOF'
## Parent

#1

## What to build

设置窗口外观分区迁移到外观子 ViewModel：皮肤选择、轮盘尺寸、文字显示、自定义配色、高亮效果。XAML 绑定保留表达式结构、只换绑定源；对应 code-behind 逻辑删除。

## Acceptance criteria

- [ ] 外观分区所有设置项立即生效且行为不变
- [ ] 自定义配色预设的增删改行为不变
- [ ] 外观子 ViewModel 有单元测试
- [ ] 该分区 code-behind 无业务逻辑残留

## Blocked by

08 颜色/图标对话框 VM 化；09 主题服务化
EOF

cat > .scratch/mvvm-refactor/tickets/11.md <<'EOF'
## Parent

#1

## What to build

配置方案编辑分区的列表侧迁移：按前台进程匹配方案展示、扇区数切换、方向槽位集合与选中态迁入正式 ViewModel（取代窗口内私有的槽位 ViewModel）、槽位名称编辑。

## Acceptance criteria

- [ ] 方案切换、扇区数变更、槽位选择行为不变
- [ ] 槽位名称编辑行为不变（输入验证一致）
- [ ] 列表侧 ViewModel 有单元测试
- [ ] 私有槽位 ViewModel 移除，正式 ViewModel 落位

## Blocked by

07 小对话框 VM 化
EOF

cat > .scratch/mvvm-refactor/tickets/12.md <<'EOF'
## Parent

#1

## What to build

槽位动作编辑闭环迁移到 ViewModel：动作类型切换（启动程序/热键/系统命令）、程序选择经对话框服务、热键录制控件经依赖属性绑定、图标设置、参数编辑；编辑结果写回配置方案并立即生效。

## Acceptance criteria

- [ ] 三类动作的编辑闭环行为不变（含录制热键、选择程序、选择图标）
- [ ] 编辑结果立即生效写回配置
- [ ] 动作编辑 ViewModel 有单元测试（mock 对话框服务）
- [ ] 该子域 code-behind 无业务逻辑残留

## Blocked by

06 对话框服务与程序选择器打样；07 小对话框 VM 化；08 颜色/图标对话框 VM 化；11 设置·配置方案分区（一）
EOF

cat > .scratch/mvvm-refactor/tickets/13.md <<'EOF'
## Parent

#1

## What to build

设置窗口剩余分区迁移：手势行为（触发阈值、修饰键禁用、全屏禁用、进程黑名单）、托盘选项、通用（语言、开机自启、退出）。

## Acceptance criteria

- [ ] 各设置项立即生效且行为不变
- [ ] 语言切换经广播事件刷新全部界面文本
- [ ] 剩余子 ViewModel 有单元测试
- [ ] 设置窗口 code-behind 仅剩构造与基础设施胶水

## Blocked by

03 配置服务缝（expand）；06 对话框服务与程序选择器打样
EOF

cat > .scratch/mvvm-refactor/tickets/14.md <<'EOF'
## Parent

#1

## What to build

根设置 ViewModel 聚合各子 ViewModel；设置窗口 DataContext 统一切换；分区间共享状态经根协调；清点并清空 code-behind。

## Acceptance criteria

- [ ] DataContext 单一根源，绑定路径全部打通
- [ ] 分区间联动行为不变
- [ ] 运行时无绑定错误输出，全量绑定检查通过
- [ ] 手动冒烟：设置全分区逐项过一遍

## Blocked by

10 外观分区；11 配置方案分区（一）；12 配置方案分区（二）；13 行为/托盘/通用分区
EOF

cat > .scratch/mvvm-refactor/tickets/15.md <<'EOF'
## Parent

#1

## What to build

动作执行器转为可注入服务（启动程序/热键/系统命令三类路由）；手势执行端切换为注入；路由逻辑可 mock 测试。

## Acceptance criteria

- [ ] 三类动作执行行为不变（含参数与工作目录语义）
- [ ] 路由逻辑单元测试（mock 系统调用层）
- [ ] 静态动作执行器删除（本票内完成收缩）

## Blocked by

04 手势引擎抽取
EOF

cat > .scratch/mvvm-refactor/tickets/16.md <<'EOF'
## Parent

#1

## What to build

删除全部被取代的旧形态（静态配置管理器、旧手势控制壳等），全仓清点确认无未迁移残留；作为整场迁移的完成验收票。

## Acceptance criteria

- [ ] 静态配置管理器与旧壳类已删除，编译干净
- [ ] dotnet test 全绿
- [ ] Python 端到端测试通过
- [ ] 手动冒烟：启动、设置全分区、四皮肤手势、托盘、退出
- [ ] ADR 状态与实现一致，无未记录偏差

## Blocked by

05 轮盘 MVVM 化；14 设置根 ViewModel 聚合收口；15 动作执行服务化
EOF

create() {
  local key="$1" title="$2" bodyfile="$3"
  local url num id
  url=$($GH issue create --repo "$REPO" --title "$title" --label "ready-for-agent" --body-file "$bodyfile")
  num=${url##*/}
  id=$($GH api "repos/$REPO/issues/$num" --jq .id)
  NUM[$key]=$num; ID[$key]=$id
  echo "$key -> #$num (db $id)" >> .scratch/mvvm-refactor/ticket-ids.txt
}

create 01 "T01 基建：工具库引入与分层重组" .scratch/mvvm-refactor/tickets/01.md
create 02 "T02 组合根与宿主重组" .scratch/mvvm-refactor/tickets/02.md
create 03 "T03 配置服务缝（expand）" .scratch/mvvm-refactor/tickets/03.md
create 04 "T04 手势引擎抽取" .scratch/mvvm-refactor/tickets/04.md
create 05 "T05 轮盘 MVVM 化" .scratch/mvvm-refactor/tickets/05.md
create 06 "T06 对话框服务与程序选择器打样" .scratch/mvvm-refactor/tickets/06.md
create 07 "T07 小对话框 VM 化" .scratch/mvvm-refactor/tickets/07.md
create 08 "T08 颜色/图标对话框 VM 化" .scratch/mvvm-refactor/tickets/08.md
create 09 "T09 主题服务化" .scratch/mvvm-refactor/tickets/09.md
create 10 "T10 设置·外观分区 VM 化" .scratch/mvvm-refactor/tickets/10.md
create 11 "T11 设置·配置方案分区（一）槽位列表与选择" .scratch/mvvm-refactor/tickets/11.md
create 12 "T12 设置·配置方案分区（二）动作编辑闭环" .scratch/mvvm-refactor/tickets/12.md
create 13 "T13 设置·行为/托盘/通用分区 VM 化" .scratch/mvvm-refactor/tickets/13.md
create 14 "T14 设置根 ViewModel 聚合收口" .scratch/mvvm-refactor/tickets/14.md
create 15 "T15 动作执行服务化" .scratch/mvvm-refactor/tickets/15.md
create 16 "T16 收尾收缩与迁移验收" .scratch/mvvm-refactor/tickets/16.md

edge() {
  $GH api --method POST "repos/$REPO/issues/${NUM[$1]}/dependencies/blocked_by" -F issue_id="${ID[$2]}" > /dev/null && echo "edge: T$2 (#${ID[$2]}) blocks T$1 (#${NUM[$1]})"
}

edge 02 01
edge 03 02
edge 04 03
edge 05 04
edge 06 02
edge 07 06
edge 08 06
edge 09 02
edge 10 08
edge 10 09
edge 11 07
edge 12 06
edge 12 07
edge 12 08
edge 12 11
edge 13 03
edge 13 06
edge 14 10
edge 14 11
edge 14 12
edge 14 13
edge 15 04
edge 16 05
edge 16 14
edge 16 15

echo "=== ALL DONE ==="
cat .scratch/mvvm-refactor/ticket-ids.txt
