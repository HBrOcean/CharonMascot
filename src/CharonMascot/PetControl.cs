using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CharonMascot.SpineRuntime;
using SkiaSharp;
using Spine;

namespace CharonMascot;

/// <summary>用 SkiaSharp 光栅化 Spine 骨架，再交给 Avalonia 合成。
///
/// 绘制方式说明：这里没有用 SKCanvas.DrawVertices（SkiaSharp 2.88 上实测会把
/// 贴图采样搞乱，整只角色碎成一堆色块），而是按 spine 官方 canvas 运行时的思路，
/// 逐个三角形求「纹理空间 → 屏幕空间」的仿射变换，再用 clip + drawBitmap 绘制。
/// 270px 高的窗口下每帧约 5~8ms，足够 60fps。</summary>
public class PetControl : Control
{
    public SkeletonModel Model { get; set; }
    public double ZoomFactor { get; set; } = 1.0;

    static readonly int[] QuadIndices = { 0, 1, 2, 2, 3, 0 };

    WriteableBitmap _target;
    SKSurface _surface;
    SKImageInfo _info;
    int _pw, _ph;

    float[] _buf = new float[32768];
    readonly SKPath _triPath = new SKPath();
    readonly SKPaint _paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Low };

    readonly bool _showPerf = Environment.GetEnvironmentVariable("PET_PERF") == "1";
    int _perfFrames;

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 320 : availableSize.Height;
        return new Size(w, h);
    }

    void EnsureSurface(int pw, int ph, double scaling)
    {
        if (_surface != null && _pw == pw && _ph == ph) return;
        _surface?.Dispose();
        _pw = pw; _ph = ph;
        _info = new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(_info);
        _target?.Dispose();
        _target = new WriteableBitmap(new PixelSize(pw, ph), new Vector(96 * scaling, 96 * scaling),
            PixelFormat.Bgra8888, AlphaFormat.Premul);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Model == null) return;
        double w = Bounds.Width, h = Bounds.Height;
        if (w < 2 || h < 2) return;

        double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        if (scaling <= 0) scaling = 1.0;
        int pw = Math.Max(2, (int)Math.Round(w * scaling));
        int ph = Math.Max(2, (int)Math.Round(h * scaling));
        EnsureSurface(pw, ph, scaling);

        var canvas = _surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var b = Model.Bounds;
        if (b.Width > 0 && b.Height > 0)
        {
            float fit = Math.Min(pw / b.Width, ph / b.Height) * (float)ZoomFactor;
            float cx = pw / 2f - b.MidX * fit;
            float cy = ph / 2f + b.MidY * fit;
            var sw = _showPerf ? System.Diagnostics.Stopwatch.StartNew() : null;
            DrawSkeleton(canvas, Model, fit, cx, cy);
            if (sw != null)
            {
                sw.Stop();
                if (_perfFrames < 5 || _perfFrames % 120 == 0)
                    Console.Error.WriteLine($"[perf] draw {sw.Elapsed.TotalMilliseconds:F2}ms frame={_perfFrames}");
                _perfFrames++;
            }
        }
        canvas.Flush();

        using (var fb = _target.Lock())
        {
            if (!_surface.ReadPixels(_info, fb.Address, fb.RowBytes, 0, 0))
            {
                using var img = _surface.Snapshot();
                using var pix = img.PeekPixels();
                unsafe
                {
                    byte* src = (byte*)pix.GetPixels().ToPointer();
                    byte* dst = (byte*)fb.Address.ToPointer();
                    int rowBytes = Math.Min(pix.RowBytes, fb.RowBytes);
                    for (int y = 0; y < ph; y++)
                        Buffer.MemoryCopy(src + y * pix.RowBytes, dst + y * fb.RowBytes, fb.RowBytes, rowBytes);
                }
            }
        }

        context.DrawImage(_target, new Rect(0, 0, w, h));
    }

    /// <summary>把当前这一帧存成 PNG（右键菜单「保存截图」与 --shot 自检用）。</summary>
    public bool SaveSnapshot(string path)
    {
        if (_surface == null) return false;
        try
        {
            using var img = _surface.Snapshot();
            using var data = img.Encode(SKEncodedImageFormat.Png, 95);
            using var fs = System.IO.File.Create(path);
            data.SaveTo(fs);
            return true;
        }
        catch { return false; }
    }

    void DrawSkeleton(SKCanvas canvas, SkeletonModel model, float s, float cx, float cy)
    {
        var skel = model.Skeleton;

        foreach (var slot in skel.DrawOrder)
        {
            var att = slot.Attachment;
            if (att == null) continue;
            if (!slot.Bone.Active) continue;

            SKBitmap bmp;
            float[] uvs;
            int[] indices;
            int count;

            if (att is RegionAttachment reg)
            {
                if (reg.Region is not AtlasRegion ar || ar.page?.rendererObject is not SKBitmap b) continue;
                if (_buf.Length < 8) _buf = new float[32768];
                reg.ComputeWorldVertices(slot, _buf, 0, 2);
                bmp = b; uvs = reg.UVs; indices = QuadIndices; count = 4;
            }
            else if (att is MeshAttachment mesh)
            {
                if (mesh.Region is not AtlasRegion ar || ar.page?.rendererObject is not SKBitmap b) continue;
                int wl = mesh.WorldVerticesLength;      // float 长度 = 顶点数 × 2
                if (wl <= 0) continue;
                if (_buf.Length < wl) _buf = new float[Math.Max(wl, _buf.Length * 2)];
                mesh.ComputeWorldVertices(slot, 0, wl, _buf, 0, 2);
                bmp = b; uvs = mesh.UVs; indices = mesh.Triangles; count = wl / 2;
            }
            else continue;

            if (count <= 0 || uvs == null || uvs.Length < count * 2) continue;
            if (indices == null || indices.Length < 3) continue;

            float a = skel.A * slot.A;
            if (a <= 0.004f) continue;
            var color = new SKColor(
                (byte)Math.Clamp(skel.R * slot.R * 255f, 0, 255),
                (byte)Math.Clamp(skel.G * slot.G * 255f, 0, 255),
                (byte)Math.Clamp(skel.B * slot.B * 255f, 0, 255),
                (byte)Math.Clamp(a * 255f, 0, 255));

            _paint.Color = color;
            _paint.BlendMode = MapBlend(slot.Data.BlendMode);

            for (int j = 0; j + 2 < indices.Length; j += 3)
            {
                int i0 = indices[j], i1 = indices[j + 1], i2 = indices[j + 2];
                if (i0 >= count || i1 >= count || i2 >= count) continue;

                float x0 = cx + _buf[i0 * 2] * s, y0 = cy - _buf[i0 * 2 + 1] * s;
                float x1 = cx + _buf[i1 * 2] * s, y1 = cy - _buf[i1 * 2 + 1] * s;
                float x2 = cx + _buf[i2 * 2] * s, y2 = cy - _buf[i2 * 2 + 1] * s;

                // spine 的 UV 是「整张图集」的归一化坐标
                float u0 = uvs[i0 * 2], v0 = uvs[i0 * 2 + 1];
                float u1 = uvs[i1 * 2], v1 = uvs[i1 * 2 + 1];
                float u2 = uvs[i2 * 2], v2 = uvs[i2 * 2 + 1];

                float du1 = u1 - u0, dv1 = v1 - v0, du2 = u2 - u0, dv2 = v2 - v0;
                float det = du1 * dv2 - du2 * dv1;
                if (Math.Abs(det) < 1e-12f) continue;
                det = 1f / det;

                // 纹理空间(0..1) -> 屏幕空间的仿射矩阵
                float ma = (dv2 * (x1 - x0) - dv1 * (x2 - x0)) * det;
                float mb = (dv2 * (y1 - y0) - dv1 * (y2 - y0)) * det;
                float mc = (du1 * (x2 - x0) - du2 * (x1 - x0)) * det;
                float md = (du1 * (y2 - y0) - du2 * (y1 - y0)) * det;
                float me = x0 - ma * u0 - mc * v0;
                float mf = y0 - mb * u0 - md * v0;

                _triPath.Reset();
                _triPath.MoveTo(x0, y0);
                _triPath.LineTo(x1, y1);
                _triPath.LineTo(x2, y2);
                _triPath.Close();

                canvas.Save();
                canvas.ClipPath(_triPath, SKClipOperation.Intersect, true);
                // 纹理 y 轴向下、spine 的 v 也向下，这里把 v 再乘一次即可对齐
                var mat = new SKMatrix(ma, mc, me, mb, md, mf, 0, 0, 1);
                canvas.Concat(ref mat);
                canvas.DrawBitmap(bmp, new SKRect(0, 0, 1, 1), _paint);
                canvas.Restore();
            }
        }
    }

    static SKBlendMode MapBlend(BlendMode mode) => mode switch
    {
        BlendMode.Additive => SKBlendMode.Plus,
        BlendMode.Multiply => SKBlendMode.Multiply,
        BlendMode.Screen => SKBlendMode.Screen,
        _ => SKBlendMode.SrcOver,
    };
}
