# 贡献指南

谢谢你想帮这个项目变得更好 🎉

## 提 Issue 之前

- 先看看 [README](README.md) 的「常见问题」和 [GUIDE.md](GUIDE.md) 的踩坑记录，很可能已经有答案。
- 贴日志/截图时，请包含：系统版本、装扮 id、做了什么操作、预期与实际表现。

## 本地开发

```bash
git clone https://github.com/HBrOcean/CharonMascot.git
cd CharonMascot/src/CharonMascot
dotnet run
```

需要 **.NET SDK 10.0+**。

### 代码结构

| 文件 | 职责 |
| --- | --- |
| `PetWindow.cs` | 透明置顶窗口、右键菜单、拖拽/缩放/点击 |
| `PetControl.cs` | SkiaSharp 光栅化 Spine 骨架（逐三角形仿射绘制） |
| `SpineRuntime/SkeletonModel.cs` | 模型加载、动画状态机、静态包围盒 |
| `AppHost.cs` | 中枢：配置、模型、窗口、热键 |
| `SettingsWindow.cs` | 设置面板（纯代码 UI，无 XAML） |
| `HotkeyService.cs` | SharpHook 全局热键 |

### 提交 PR

1. 从 `main` 切一个分支：`git checkout -b fix/xxx`
2. 保持改动聚焦，一个 PR 只做一件事
3. 提交前跑一次 `dotnet build -c Release`，确保 0 error
4. 如果改了渲染相关的代码，请附一张/一段改动前后的对比截图

## 换角色 / 做自己的桌宠

`tools/fetch_assets.py` 里的 `GARMENTS` 改成目标角色的 garmentId，
`AppHost.Garments` 同步改一下，再跑 `tools/make_icons.py` 生成图标即可。
素材接口规律见 [GUIDE.md](GUIDE.md) 第 1 节。

## 版权提醒

请**不要**提交任何商业素材、破解资源或他人作品的二次分发。
角色形象属于 Bluepoch，本项目只做个人学习用途。
