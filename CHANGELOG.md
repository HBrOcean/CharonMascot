# Changelog

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

## [1.1.0] - 2026-11-02

### Fixed
- **重写渲染管线**：`SKCanvas.DrawVertices` 在 SkiaSharp 2.88.9 上会把 Spine 网格的贴图采样搞乱
  （角色碎成一堆色块），改为照 spine-canvas 的思路**逐三角形求仿射矩阵 + clip + drawBitmap**，
  渲染恢复正常，270px 高的窗口每帧约 5~8ms。
- 贴图改用 `SKCodec` 显式按 `Bgra8888 + Unpremul` 解码，保证 alpha 通道完整，边缘不再发脏。
- 修复 312802「沉默与悲鸣」部件散开的问题（与上条同源）。
- 修复配置文件里 `WindowX/WindowY` 为 `null` 时整个配置反序列化失败、设置被重置的问题（改用 `double?`）。

### Added
- 右键菜单「保存截图…」，图片存到 `图片\CharonMascot\`。
- `push.bat` / `push.sh` 一键推送脚本。
- `LICENSE`、`CONTRIBUTING.md`、`.gitattributes`、`.editorconfig`、Issue 模板、Release 工作流。
- 文档：`GUIDE.md` 补充渲染踩坑记录，`docs/` 放预览图。

## [1.0.0] - 2026-11-01

### Added
- 首个版本：卡戎（Charon）桌宠，C# / Avalonia 复刻 NameDayMascot 架构。
- 透明置顶窗口、Spine 骨骼实时渲染、三套装扮、12 个动作。
- 左键拖动 / 单击互动、右键菜单、`Ctrl+滚轮` 缩放、全局热键（`Ctrl+Alt+H / S / Q`）。
- 配置持久化到 `%APPDATA%\CharonMascot\config.json`。
- 三平台 GitHub Actions 构建工作流。
