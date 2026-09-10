# Rave Studio AI

单一 WPF 应用，整合 EDC/SDS、Matrix、Edit Check 和 RWS 数据功能。

## 当前结构

- 一个 `.NET 9 WPF` 项目
- 一个主窗口和五个页面（包含 AI 配置页）
- HandyControl 全局主题
- JSON 多 AI 配置，下拉选择后可即时应用
- 每次 AI 请求使用开始时的配置快照，切换 Profile 从下一次请求生效
- 日志按模块保存在 `Logs` 子目录，可在应用内的“日志中心”打开
- 生成的 Excel 等成果保存在 `Output`，不与日志混放
- Excel 统一采用 ClosedXML
- Word 延续使用 DocX

## AI 配置

默认配置文件位于 `Config/ai-configs.json`。可以预设多个 Profile，并在应用内选择、编辑和保存。新配置从下一次 AI 请求开始生效，不需要重启应用。

## RWS 配置

RWS 配置位于 `Config/rws-configs.json`，按“租户 → 试验 → 环境 → Forms”维护。租户节点同时保存用户名和密码。

Rave 生产环境返回的 `Environment` 可能是空字符串，因此生产环境必须使用空字符串 `""` 作为配置 key。界面和历史记录会显示为 `Prod`，但配置查找以及传给 RWS SDK 的值仍为 `""`；在界面手工输入 `Prod` 保存时也会自动转换为 `""`。
