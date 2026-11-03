# 开发手记 · CharonMascot

这份文档记录卡戎桌宠的实现要点、素材接口和踩过的坑，方便以后照葫芦画瓢做别的角色。

---

## 1. 素材接口（UTTU Archive）

角色页：`https://uttu.merui.net/profiles/charon/`（用浏览器 UA 请求，才能拿到完整的装扮列表）

| 用途 | 地址 |
| --- | --- |
| Q 版 Spine 骨骼 | `https://model.merui.net/chibi/{garmentId}/{garmentId}.skel` / `.atlas` / `.webp` |
| Q 版静态图 | `https://assets.merui.net/chibi/{garmentId}.webp` |
| 小头像（136×136） | `https://assets.merui.net/headicon_small/{id}.webp` |
| 立绘（1062×2048） | `https://assets.merui.net/headicon_img_l2d/{id}.webp` |
| 战斗用高精度模型 | `https://model.merui.net/spine/{id}/{id}.{skel,atlas,webp}` |

卡戎（Charon）的装扮 id：

| garmentId | 中文名 | 英文名 |
| --- | --- | --- |
| 312801 | 默认 | Default |
| 312802 | 沉默与悲鸣 | Wailing Silence |
| 312803 | 亡者的恩赐 | Gift of the Dead |

三套模型的动画名：

- 312801 / 312803：`born, die, freeze, giddy, hit, idle, posture, skill1, skill2, skill3, sleep, unique`
- 312802：`born, change, die, giddy, hit, idle, posture, skill1, skill2, skill3, sleep, unique`（多 `change`，少 `freeze`）

图集是单页（512×1024 或 2048×512），`scale:0.7`，纹理为 webp，需转 PNG 并把 atlas 第一行的页面名改成 `.png`。

## 2. 渲染方案（这一段是血泪教训）

**不要用 `SKCanvas.DrawVertices` 画 Spine 网格。** 在 SkiaSharp 2.88.9 上，不论 texCoords 传
归一化 UV（0..1）还是像素坐标，输出都会变成一堆互不相干的色块（整只角色"碎掉"）。
这个坑极难通过日志发现：顶点数、三角形索引、UV 范围全部正常。

正确做法是照 spine 官方 canvas 运行时的思路，**逐三角形求「纹理空间 → 屏幕空间」的仿射矩阵**：

1. 取三角形三个顶点的屏幕坐标 (x,y) 与归一化 UV (u,v)；
2. 解 2×3 仿射矩阵（UV 差分矩阵求逆），得到 `SKMatrix(ma, mc, me, mb, md, mf, 0,0,1)`；
3. `canvas.ClipPath(三角形)` → `canvas.Concat(matrix)` → `canvas.DrawBitmap(bmp, Rect(0,0,1,1))`；
4. 每帧约 5~8ms（270px 高的窗口，5000 个三角形），60fps 绰绰有余。

其余铁律：

- **贴图必须显式按 `Bgra8888 + Unpremul` 解码**（`SKCodec.GetPixels`），保证 alpha 通道完整；
  默认 `SKBitmap.Decode` 给的是 Premul，叠上 `paint.Color` 的 alpha 容易二次预乘、边缘发脏。
- **V 轴不要翻转**：顶点用 `cy - worldY * scale`（世界 y 向上 → 屏幕 y 向下），UV 直接用 `v`。
- **`MeshAttachment.ComputeWorldVertices(slot, start, count, ...)` 的 `count` 是 float 长度**（= `WorldVerticesLength`），
  不是顶点数；传成顶点数会让后半段顶点变成脏数据。
- **`RegionAttachment.ComputeWorldVertices(slot, buf, offset, stride)`** 输出 4 个顶点，顺序是 左下/左上/右上/右下，
  索引固定 `{0,1,2, 2,3,0}`。
- 图集里的 **90° 旋转 spine-csharp 原生支持**；180/270 不做 OUV 旋转修正 —— 实测把 180/270 的 mesh UV 再旋转一次，
  反而会让衣褶、绑带方向全反（因为 mesh 的 UV 已经按打包结果烘焙好了）。

## 3. Spine 4.2 的 API 要点

- `new Atlas(path, textureLoader)`，`TextureLoader` 需要实现 `Load(AtlasPage, string)` 和 `Unload(object)` 两个方法。
- `new SkeletonBinary(atlas).ReadSkeletonData(stream)`。
- `skeleton.UpdateWorldTransform(Skeleton.Physics.None)` —— 参数不能省。
- 颜色在 `Skeleton` / `Slot` 上直接是 `R/G/B/A` 四个 float，没有 `Color` 对象；混合模式在 `slot.Data.BlendMode`。
- `Animation.Apply(skeleton, lastTime, time, loop, null, 1f, MixBlend.Setup, MixDirection.In)` 可用于逐帧采样包围盒。
- `MeshAttachment.Region` 是 `AtlasRegion`，贴图在 `((AtlasRegion)region).page.rendererObject`。

## 4. 静态包围盒

只用「身体动画」（idle / posture / hit / giddy / freeze / change）逐帧采样的顶点并集，取 1%/99% 分位数，
再与所有动画的包围盒求交（允许在身体基准上外扩 20%）。这样既不会因为某个动画的位移/拖尾把窗口撑大，
也不会把角色压成一小只。卡戎三套装扮的包围盒都稳定在 ~180×340 单位。

## 5. Avalonia 踩坑

- **`Control` 默认 `DesiredSize` 为 0**，不重写 `MeasureOverride` 时 `Render` 根本不会被调用（窗口里一片空白）。重写后返回可用尺寸即可。
- **透明窗口要三件套**：`SystemDecorations.None` + `TransparencyLevelHint = { Transparent }` + `Background = Brushes.Transparent`。
- **无 GPU 环境**（Xvfb / 远程桌面 / 虚拟机）Avalonia 的 GLX 渲染会因 llvmpipe 被拉黑而失败，需在 `X11PlatformOptions.RenderingMode` 里把 `Software` 排在前面。
- **不要依赖 `DrawingContext.TryGetFeature`**：本项目改用「SkiaSharp 光栅化到 SKSurface → 拷进 WriteableBitmap → `DrawImage`」的方式，
  跨版本、跨平台都稳（代价是多一次像素拷贝，240px 高的窗口完全无感）。
- `WriteableBitmap` 用 `Bgra8888 + Premul`，与 `SKImageInfo(SKColorType.Bgra8888, SKAlphaType.Premul)` 直接对齐，`ReadPixels` 一次完成拷贝。

## 6. 打包

- 项目把 `Assets/**` 作为 `Content` 复制到输出目录（不是 `AvaloniaResource`），因为 Spine 需要真实文件路径读取 `.skel/.atlas/.png`。
- 单文件发布时 `Assets/` 仍需与 exe 同目录分发。
- `ApplicationIcon` 指向 `Assets/icons/app.ico`；`AllowUnsafeBlocks` 用于 ReadPixels 失败时的逐行拷贝兜底。
