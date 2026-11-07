using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CharonMascot.Services;

/// <summary>一套皮肤的元信息。</summary>
public sealed class SkinEntry
{
    /// <summary>目录名，同时也是 .atlas / .skel 的文件名前缀。</summary>
    public string Id { get; init; } = "";

    /// <summary>界面上显示的名字（一定有值，不会为 null）。</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>皮肤目录；资源缺失时为 null。</summary>
    public string? Dir { get; init; }

    /// <summary>.atlas 与 .skel 齐全，真的能加载。</summary>
    public bool IsUsable { get; init; }
}

/// <summary>
/// 皮肤目录扫描。
///
/// 约定：Assets/characters/&lt;id&gt;/ 下放同名的 &lt;id&gt;.atlas、&lt;id&gt;.skel 和图集，
/// 该目录即为一套皮肤。放一个 display.txt（UTF-8，单行）可以自定义显示名。
///
/// 这里**没有任何硬编码的皮肤列表**：列表完全等于目录内容。
/// 显示名走三级兜底（见 <see cref="DisplayNameFrom"/>）：
///   1. config.json 里的 SkinNames[id]（用户可改，优先级最高）
///   2. 目录里的 display.txt
///   3. 目录名（id）
///
/// 排序按目录名升序，因此 312801 / 312802 / 312803 天然按顺序排列；
/// 删除某套目录，它就从列表里消失，不需要改任何代码。
/// </summary>
public static class SkinCatalog
{
    private static List<SkinEntry>? _cache;

    /// <summary>config.json 的 SkinNames 注入点（由 ConfigStore.Load 在最早期调用一次）。</summary>
    private static IReadOnlyDictionary<string, string>? _configuredNames;

    /// <summary>
    /// 注入配置名。必须在任何人访问 <see cref="All"/> 之前调用 ——
    /// 否则缓存里已经是旧名字，直到下次 Invalidate 才更新。
    /// </summary>
    public static void SetConfiguredNames(IReadOnlyDictionary<string, string>? names)
    {
        _configuredNames = names;
        _cache = null;
    }

    /// <summary>当前可用的全部皮肤（按目录名排序）。</summary>
    public static IReadOnlyList<SkinEntry> All => _cache ??= Scan();

    /// <summary>丢弃缓存，下次访问 All 时重新扫描。</summary>
    public static void Invalidate() => _cache = null;

    /// <summary>皮肤显示名：配置名 → display.txt → 目录名。</summary>
    public static string DisplayNameOf(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";

        var hit = All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
        if (hit != null) return hit.DisplayName;

        // 不在缓存里（比如配置里存了已删掉的皮肤），现场算一个
        return DisplayNameFrom(id, DirOf(id));
    }

    /// <summary>某套皮肤的目录；找不到返回 null。</summary>
    public static string? DirOf(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        foreach (string root in RootCandidates())
        {
            string dir = Path.Combine(root, id);
            if (Directory.Exists(dir)) return dir;
        }
        return null;
    }

    /// <summary>characters 根目录的候选路径（程序目录优先，其次是开发时的源码目录）。</summary>
    public static IEnumerable<string> RootCandidates()
    {
        string baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "Assets", "characters");
        yield return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Assets", "characters"));
    }

    private static List<SkinEntry> Scan()
    {
        // id → 目录。同名时以先找到的为准（程序目录优先于源码目录）
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in RootCandidates())
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string id = Path.GetFileName(dir);
                    if (!found.ContainsKey(id)) found[id] = dir;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"[Skin] 扫描目录失败 {root}", ex);
            }
        }

        var list = new List<SkinEntry>();
        foreach (var kv in found.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            list.Add(Make(kv.Key, kv.Value));

        Logger.Log($"[Skin] 扫描到 {list.Count} 套皮肤（其中可用 {list.Count(e => e.IsUsable)} 套）");
        return list;
    }

    private static SkinEntry Make(string id, string dir)
    {
        bool usable = false;
        try
        {
            usable = File.Exists(Path.Combine(dir, id + ".atlas"))
                  && File.Exists(Path.Combine(dir, id + ".skel"));
        }
        catch (Exception ex) { Logger.Error($"[Skin] 检查 {id} 资源失败", ex); }

        return new SkinEntry
        {
            Id = id,
            DisplayName = DisplayNameFrom(id, dir),
            Dir = dir,
            IsUsable = usable,
        };
    }

    /// <summary>
    /// 显示名三级兜底：配置名 → display.txt → 目录名。
    /// 任何一级读不到都继续往下一级走，保证返回值非空。
    /// </summary>
    private static string DisplayNameFrom(string id, string? dir)
    {
        // 1) config.json 的 SkinNames
        if (_configuredNames != null &&
            _configuredNames.TryGetValue(id, out string? cfgName) &&
            !string.IsNullOrWhiteSpace(cfgName))
            return cfgName.Trim();

        // 2) 目录里的 display.txt
        if (dir != null)
        {
            try
            {
                string txt = Path.Combine(dir, "display.txt");
                if (File.Exists(txt))
                {
                    string line = File.ReadLines(txt, System.Text.Encoding.UTF8)
                                      .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))
                                  ?? "";
                    line = line.Trim();
                    if (line.Length > 0) return line;
                }
            }
            catch (Exception ex) { Logger.Error($"[Skin] 读取 display.txt 失败 {id}", ex); }
        }

        // 3) 目录名
        return id;
    }
}
