using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SkiaSharp;
using Spine;

namespace CharonMascot.SpineRuntime;

/// <summary>把 atlas 中的贴图页加载为 SkiaSharp 位图。</summary>
public sealed class SkiaTextureLoader : TextureLoader
{
    readonly string _dir;
    readonly List<SKBitmap> _owned;

    public SkiaTextureLoader(string dir, List<SKBitmap> owned)
    {
        _dir = dir;
        _owned = owned;
    }

    public void Load(AtlasPage page, string path)
    {
        var candidates = new List<string>
        {
            Path.Combine(_dir, page.name ?? ""),
            Path.Combine(_dir, Path.GetFileName(path ?? "")),
        };
        string file = candidates.FirstOrDefault(File.Exists);
        if (file == null)
            throw new FileNotFoundException("贴图不存在: " + string.Join(" | ", candidates));

        using var stream = File.OpenRead(file);
        using var codec = SKCodec.Create(stream) ?? throw new InvalidOperationException("无法解码贴图: " + file);
        // 明确按 BGRA8888 + Unpremul(直通 alpha) 解码，确保贴图的 alpha 通道完整保留
        var srcInfo = codec.Info;
        var info = new SKImageInfo(srcInfo.Width, srcInfo.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var bmp = new SKBitmap(info);
        var result = codec.GetPixels(info, bmp.GetPixels());
        if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
            throw new InvalidOperationException($"贴图解码失败: {file} ({result})");
        bmp.SetImmutable();

        if (Environment.GetEnvironmentVariable("PET_TEXDIAG") == "1")
        {
            int transparent = 0, semi = 0, total = 0;
            for (int y = 0; y < bmp.Height; y += 4)
                for (int x = 0; x < bmp.Width; x += 4)
                {
                    byte a = bmp.GetPixel(x, y).Alpha;
                    total++;
                    if (a == 0) transparent++;
                    else if (a < 250) semi++;
                }
            Console.Error.WriteLine($"[tex] {Path.GetFileName(file)} {bmp.Width}x{bmp.Height} type={bmp.ColorType} alphaType={bmp.AlphaType} transparent={transparent * 100.0 / total:F1}% semi={semi * 100.0 / total:F1}%");
        }
        page.rendererObject = bmp;
        page.width = bmp.Width;
        page.height = bmp.Height;
        lock (_owned) _owned.Add(bmp);
    }

    public void Unload(object texture)
    {
        if (texture is SKBitmap bmp)
            lock (_owned) _owned.Remove(bmp);
    }
}

/// <summary>一个角色的 Spine 模型：数据、动画状态、静态包围盒。</summary>
public sealed class SkeletonModel : IDisposable
{
    public string Id { get; private set; }
    public SkeletonData Data { get; private set; }
    public Skeleton Skeleton { get; private set; }
    public AnimationState State { get; private set; }
    public AnimationStateData StateData { get; private set; }
    public Atlas Atlas { get; private set; }
    public SKRect Bounds { get; private set; } = new SKRect(-100, -100, 100, 100);

    readonly List<SKBitmap> _bitmaps = new();
    readonly float[] _scratch = new float[8192];

    public IReadOnlyList<SKBitmap> Textures => _bitmaps;

    public IEnumerable<string> AnimationNames => Data.Animations.Select(a => a.Name);

    public bool HasAnimation(string name) => Data.FindAnimation(name) != null;

    public static SkeletonModel Load(string dir, string id)
    {
        var model = new SkeletonModel { Id = id };
        var loader = new SkiaTextureLoader(dir, model._bitmaps);
        model.Atlas = new Atlas(Path.Combine(dir, id + ".atlas"), loader);
        using (var fs = File.OpenRead(Path.Combine(dir, id + ".skel")))
        {
            var bin = new SkeletonBinary(model.Atlas);
            model.Data = bin.ReadSkeletonData(fs);
        }
        model.Skeleton = new Skeleton(model.Data);
        model.Skeleton.SetToSetupPose();
        model.StateData = new AnimationStateData(model.Data) { DefaultMix = 0.18f };
        model.State = new AnimationState(model.StateData);
        model.Bounds = model.ComputeBounds();
        return model;
    }

    /// <summary>用 idle/posture/hit/giddy 等「身体动画」的顶点并集估算静态包围盒，
    /// 其余动画最多再外扩 40%（避免大招位移把窗口撑得过大）。</summary>
    SKRect ComputeBounds()
    {
        var bodyNames = new[] { "idle", "posture", "hit", "giddy", "freeze", "change" };
        var bodyPoints = SamplePoints(bodyNames);
        var allPoints = SamplePoints(Data.Animations.Select(a => a.Name).ToArray());
        if (bodyPoints.Count == 0) bodyPoints = allPoints;
        if (bodyPoints.Count == 0) return new SKRect(-100, -100, 100, 100);

        var rect = PercentileRect(bodyPoints, 0.01f, 0.99f);
        if (allPoints.Count > 0)
        {
            var all = PercentileRect(allPoints, 0.01f, 0.99f);
            // 允许在身体基准上外扩 40%
            float padX = rect.Width * 0.2f, padY = rect.Height * 0.2f;
            var expanded = new SKRect(rect.Left - padX, rect.Top - padY, rect.Right + padX, rect.Bottom + padY);
            rect = SKRect.Intersect(expanded, all) is var inter && !inter.IsEmpty ? inter : rect;
            // 交集不小于身体基准的 60%，避免因个别动画没顶点导致框过小
            if (rect.Width < bodyPointsRect(bodyPoints).Width * 0.6f) rect = bodyPointsRect(bodyPoints);
        }
        // 加一点边距
        float extraX = Math.Max(rect.Width * 0.04f, 1f), extraY = Math.Max(rect.Height * 0.02f, 1f);
        return new SKRect(rect.Left - extraX, rect.Top - extraY, rect.Right + extraX, rect.Bottom + extraY);
    }

    static SKRect bodyPointsRect(List<(float x, float y)> pts)
    {
        var r = PercentileRect(pts, 0.01f, 0.99f);
        return new SKRect(r.Left, r.Top, r.Right, r.Bottom);
    }

    static SKRect PercentileRect(List<(float x, float y)> pts, float lo, float hi)
    {
        var xs = pts.Select(p => p.x).OrderBy(v => v).ToArray();
        var ys = pts.Select(p => p.y).OrderBy(v => v).ToArray();
        float pick(float[] arr, float p) => arr[Math.Clamp((int)(arr.Length * p), 0, arr.Length - 1)];
        return new SKRect(pick(xs, lo), pick(ys, lo), pick(xs, hi), pick(ys, hi));
    }

    List<(float x, float y)> SamplePoints(string[] animNames)
    {
        var result = new List<(float, float)>();
        var skel = new Skeleton(Data);
        var buf = new float[8192];
        foreach (var name in animNames)
        {
            var anim = Data.FindAnimation(name);
            if (anim == null) continue;
            float dur = anim.Duration;
            int steps = Math.Clamp((int)(dur * 12), 1, 60);
            for (int s = 0; s <= steps; s++)
            {
                float t = dur * s / steps;
                skel.SetToSetupPose();
                try { anim.Apply(skel, 0, t, true, null, 1f, MixBlend.Setup, MixDirection.In); }
                catch { continue; }
                skel.UpdateWorldTransform(Skeleton.Physics.None);
                CollectVertices(skel, buf, result);
            }
        }
        return result;
    }

    void CollectVertices(Skeleton skel, float[] buf, List<(float, float)> sink)
    {
        foreach (var slot in skel.DrawOrder)
        {
            var att = slot.Attachment;
            if (att == null || !slot.Bone.Active) continue;
            int n = 0;
            if (att is RegionAttachment reg)
            {
                n = 4;
                if (buf.Length < 8) continue;
                reg.ComputeWorldVertices(slot, buf, 0, 2);
            }
            else if (att is MeshAttachment mesh)
            {
                n = mesh.WorldVerticesLength / 2;
                if (buf.Length < mesh.WorldVerticesLength) continue;
                mesh.ComputeWorldVertices(slot, 0, mesh.WorldVerticesLength, buf, 0, 2);
            }
            else continue;

            for (int i = 0; i < n; i++)
            {
                float x = buf[i * 2], y = buf[i * 2 + 1];
                if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) continue;
                if (Math.Abs(x) > 5000 || Math.Abs(y) > 5000) continue;
                sink.Add((x, y));
            }
        }
    }

    public void Play(string name, bool loop = true, float mix = 0.18f)
    {
        var anim = Data.FindAnimation(name);
        if (anim == null) return;
        StateData.DefaultMix = mix;
        var track = State.SetAnimation(0, name, loop);
        if (track != null) track.MixDuration = mix;
    }

    public void Update(float dt)
    {
        State.Update(dt);
        State.Apply(Skeleton);
        Skeleton.UpdateWorldTransform(Skeleton.Physics.None);
    }

    public void Dispose()
    {
        foreach (var b in _bitmaps) b.Dispose();
        _bitmaps.Clear();
    }
}
