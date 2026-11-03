using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CharonMascot;

/// <summary>应用配置（自动持久化到 %APPDATA%/CharonMascot/config.json）。</summary>
public class AppConfig
{
    public double Scale { get; set; } = 1.0;              // 50% ~ 400%
    public string GarmentId { get; set; } = "312801";     // 当前装扮
    public bool Topmost { get; set; } = true;
    public bool LockPosition { get; set; } = false;
    public bool IdleRandomAction { get; set; } = true;    // 空闲时随机做动作
    public int IdleActionMaxSeconds { get; set; } = 45;   // 空闲多久后随机动作
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public bool PlayEnterOnStart { get; set; } = true;

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CharonMascot");

    public string FilePath => Path.Combine(Dir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            var cfg = new AppConfig();
            if (File.Exists(cfg.FilePath))
            {
                var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(cfg.FilePath), JsonOpts);
                if (loaded != null) return loaded;
            }
        }
        catch { /* 配置损坏时回退默认值 */ }
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { /* 忽略写入失败 */ }
    }
}
