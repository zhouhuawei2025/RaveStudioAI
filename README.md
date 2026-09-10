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
