# CharonMascot · 卡戎桌宠

《重返未来：1999》卡戎的桌面分身。透明无边框、可置顶或置底、随手拖到屏幕任何角落，单击会动，也能绑全局快捷键遥控。

| | |
|---|---|
| **技术栈** | .NET 10 · Avalonia 11.3 · spine-csharp 4.2 · SkiaSharp · SharpHook |
| **平台** | Windows（完整功能）· macOS / Linux 可运行，窗口特效与开机自启仅 Windows 生效 |

## 下载

到 [Releases](https://github.com/HBrOcean/CharonMascot/releases) 下载 `CharonMascot-win-x64.zip`，解压后双击 `CharonMascot.exe`。

自包含单文件，**不需要另外安装 .NET 运行时**。

## 特性

- 30fps 实时渲染 Spine 动画，透明背景，默认不占任务栏
- 冷启动播一遍入场动画，之后自然转入待机（换皮肤不再播入场，不会有"换皮时往左上飘"的错觉）
- 拖动移动、位置记忆；**允许拖到屏幕外**，贴边不回弹
- 单击播放一个动作，播完自动回待机
- 定时随机动作：默认每 20 秒自己动一下，间隔 5~180 秒可调，可整开关
- 窗口随动作幅度自动长高长宽（视框扩张立即生效、收缩延迟 1 秒），脚的落点始终不动
- 右键菜单 / 托盘菜单双入口：设置、播放动作、切皮肤、隐藏、重置位置、退出
- 皮肤自动扫描目录，设置面板里以卡片网格呈现（带缩略图）
- 全局快捷键可视化编辑，支持一个按键绑定多个动作（随机播一个）
- 单实例保护：重复启动会唤醒已有实例，不会开出第二只

## 三种"动"的规则

容易被搞混，先说清楚：

| 触发方式 | 播什么 |
|---|---|
| **单击 / 右键菜单 / 托盘 / 快捷键** | 若配置了「点击动作」就播它，否则从池子里随机 |
| **定时器（每 20 秒）** | **完全忽略**「点击动作」配置，纯随机 —— 否则配了固定动作后定时器每次都在重播同一个 |
| **待机** | 播完一次性动作后回到 `idle`（可在设置里改成别的动画） |

- 随机池排除状态型动画：`idle` / `born` / `sleep` / `die` / `posture`
- 连续两次不会抽到同一个
- 任何一次手动动作都会把定时器重新计时，不会出现"刚点完两秒又被催一下"

## 默认快捷键

全部为 `Ctrl + 键`，可在「设置 → 按键」里改，支持 `Ctrl+Shift+A` 这类多修饰组合。

| 快捷键 | 动作 | 快捷键 | 动作 |
|---|---|---|---|
| `Ctrl + Y` | 打开设置面板 | `Ctrl + G` | giddy |
| `Ctrl + N` | 关闭设置面板 | `Ctrl + H` | hit |
| `Ctrl + 1` | skill1 | `Ctrl + I` | idle |
| `Ctrl + 2` | skill2 | `Ctrl + P` | posture |
| `Ctrl + D` | die | `Ctrl + S` | sleep |
| `Ctrl + Q` | unique | `Ctrl + B` | born |

> 全局钩子只监听、不拦截。在编辑器里按 `Ctrl+S` / `Ctrl+B` 会同时触发编辑器自身功能，介意就改成 `Ctrl+Shift+字母`。

## 皮肤

**没有任何硬编码列表**：启动时扫描 `Assets/characters/` 下的每个子目录，目录名即皮肤 ID，凑齐同名的 `.atlas` + `.skel` 即为可用皮肤。删掉目录，它就自动从列表消失。

当前附带卡戎的三套装扮：

| ID | 名称 |
|---|---|
| 312801 | 默认 |
| 312802 | 沉默与悲鸣 |
| 312803 | 亡者的恩赐 |

### 显示名的三级兜底

1. `config.json` 里的 `SkinNames`（`{"312801": "默认"}`，改完重启生效）
2. 皮肤目录里的 `display.txt`（UTF-8，第一行即名字）
3. 目录名

### 添加自己的皮肤

1. 在 `Assets/characters/` 下新建子目录，目录名就是皮肤 ID
2. 放入同名文件：`<id>.atlas`、`<id>.skel`，以及 atlas 里声明的图集
3. 想要自定义显示名，放一个 `display.txt`
4. 设置 → **皮肤** → 「刷新列表」

资源不齐的皮肤会**置灰并标注「资源缺失」**，点不动，也不会让列表变空。

缩略图是运行时用同一套渲染器离屏渲的（图集是散件拼图，不能直接当预览图），首次缓存到配置目录；换模型后对不上就点「重建预览图」。

## 配置

配置文件：`%AppData%\CharonMascot\config.json`（macOS / Linux 为 `~/.config/CharonMascot/`）
日志：同目录 `log.txt`，排查问题先看它。

几乎所有选项都能在设置面板里改。手改 JSON 也是支持的，改完重启生效。

## 从源码构建

需要 [.NET 10 SDK](https://dotnet.microsoft.com/download)。

```bash
# Windows
build.bat

# macOS / Linux
chmod +x build.sh && ./build.sh

# 或者直接跑
dotnet run
```

打包成自包含单文件：

```bash
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

推到 GitHub 后 Actions 会在 `windows-latest` 上自动构建；打 `v*` 标签会额外发 Release：

```bash
git tag v1.3.0
git push origin v1.3.0
```

## 常见问题

**桌宠不见了**
先看托盘图标（可能被系统折叠进「隐藏的图标」）。窗口允许拖到屏幕外，所以托盘菜单里的「重置位置（拉回主屏右下角）」是唯一永远点得到的自救入口。

**窗口大小会自己变**
那是「动态尺寸」：动作幅度大时窗口临时长大，动作结束约 1 秒后慢慢缩回。不想要就在设置里关掉。

**快捷键没反应**
看 `log.txt` 里有没有 `[Key] 匹配 ...`。没有就说明按键被系统或其他软件吞了；有记录但不触发，检查格式是否为 `修饰键 + Vc键名`（如 `Ctrl+Vc1`），设置面板里格式错误会标红。

**换了皮肤，某些快捷键不动了**
不同角色动画名不一定通用。绑定里失效的动作会变红，重选即可。

**开了鼠标穿透后拖不动**
穿透模式下点击落到下层窗口。用托盘菜单关掉。

## 许可

代码 MIT（见 [LICENSE](LICENSE)）。`Assets/` 下的角色素材不在 MIT 范围内，版权归原权利方，仅供个人非商业使用。

- Spine 运行时来自 [esotericsoftware/spine-runtimes](https://github.com/esotericsoftware/spine-runtimes)（spine-csharp 4.2），遵循 Spine Runtime License
- 角色素材来自 UTTU Archive（《重返未来：1999》卡戎）
