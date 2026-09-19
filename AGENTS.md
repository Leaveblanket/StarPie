# AGENTS.md

直接动手,build + xUnit 必绿,提交消息人能看懂就行。

e2e 触发约定:不进 CI,由 agent 显式发起——日常/迭代首轮 `-Smoke`(巡检快检,失败按报告里的
复审判例节点 `-TestPath` 单跑);收尾/提交前 `-Full`(全量门禁,无参默认同 `-Full`)。

术语见 `CONTEXT.md`;架构/分层/插件读 `docs/architecture/` 仍保留的叶子。

# 上游项目

- 当前项目的上游项目指向D:\Project\.net\StarPie-Upsteam
