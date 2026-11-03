using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using CharonMascot.SpineRuntime;

namespace CharonMascot;

/// <summary>把窗口、模型、配置、热键串起来的中枢。</summary>
public sealed class AppHost
{
    public static AppHost Instance { get; private set; }

    public AppConfig Config { get; private set; } = AppConfig.Load();
    public SkeletonModel Model { get; private set; }
    public PetWindow PetWindow { get; private set; }
    public SettingsWindow SettingsWindow { get; private set; }
    HotkeyService _hotkeys;

    public event Action ModelReloaded;

    /// <summary>卡戎（Charon）在 UTTU Archive 上的三套装扮。</summary>
    public static readonly (string Id, string Name)[] Garments =
    {
        ("312801", "默认"),
        ("312802", "沉默与悲鸣"),
        ("312803", "亡者的恩赐"),
    };

    public static string CharacterAssetsRoot
    {
        get
        {
            var primary = Path.Combine(AppContext.BaseDirectory, "Assets", "characters");
            if (Directory.Exists(primary)) return primary;
            var alt = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "characters"));
            if (Directory.Exists(alt)) return alt;
            return primary;
        }
    }

    public static string GarmentDir(string id) => Path.Combine(CharacterAssetsRoot, id);

    public static void Init()
    {
        Instance = new AppHost();
        Instance.Start();
    }

    void Start()
    {
        if (!Garments.Any(g => g.Id == Config.GarmentId) || !Directory.Exists(GarmentDir(Config.GarmentId)))
            Config.GarmentId = Garments[0].Id;

        LoadModel(Config.GarmentId);

        PetWindow = new PetWindow(this);
        PetWindow.Show();

        try
        {
            _hotkeys = new HotkeyService();
            _hotkeys.HotkeyPressed += OnHotkey;
            _hotkeys.Start();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[hotkey] 全局热键不可用: " + ex.Message);
        }

        var shotPath = Environment.GetEnvironmentVariable("PET_SHOT");
        if (!string.IsNullOrEmpty(shotPath))
        {
            double delay = 4;
            double.TryParse(Environment.GetEnvironmentVariable("PET_SHOT_DELAY"), out delay);
            if (delay <= 0.2) delay = 4;
            var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                PetWindow.SaveShotTo(shotPath);
                Quit();
            };
            timer.Start();
        }

        if (Config.PlayEnterOnStart && Model != null && Model.HasAnimation("born"))
            PetWindow.PlayOnce("born", 3.2f);
        else if (Model != null && Model.HasAnimation("idle"))
            Model.Play("idle", true);
    }

    void OnHotkey(string id)
    {
        Dispatcher.UIThread.Post(() =>
        {
            switch (id)
            {
                case "toggle": ToggleVisible(); break;
                case "settings": OpenSettings(); break;
                case "quit": Quit(); break;
            }
        });
    }

    public void LoadModel(string garmentId)
    {
        var dir = GarmentDir(garmentId);
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException("素材目录不存在: " + dir);
        var old = Model;
        var model = SkeletonModel.Load(dir, garmentId);
        Model = model;
        Config.GarmentId = garmentId;
        old?.Dispose();
        if (PetWindow != null)
        {
            PetWindow.OnModelChanged();
            ModelReloaded?.Invoke();
        }
    }

    public void SwitchGarment(string garmentId)
    {
        if (garmentId == Config.GarmentId) return;
        LoadModel(garmentId);
        Config.Save();
    }

    public void SetScale(double scale)
    {
        Config.Scale = Math.Clamp(scale, 0.5, 4.0);
        PetWindow?.ApplyScale();
        Config.Save();
    }

    public void ToggleVisible()
    {
        if (PetWindow == null) return;
        if (PetWindow.IsVisible) PetWindow.Hide();
        else { PetWindow.Show(); PetWindow.Activate(); }
    }

    public void OpenSettings()
    {
        if (SettingsWindow == null || !SettingsWindow.IsVisible)
        {
            SettingsWindow = new SettingsWindow(this);
            SettingsWindow.Closed += (_, _) => SettingsWindow = null;
            SettingsWindow.Show();
        }
        else SettingsWindow.Activate();
    }

    public void SaveWindowPosition()
    {
        if (PetWindow == null) return;
        Config.WindowX = PetWindow.Position.X;
        Config.WindowY = PetWindow.Position.Y;
        Config.Save();
    }

    public void Quit()
    {
        try { SaveWindowPosition(); } catch { }
        _hotkeys?.Dispose();
        Config.Save();
        if (PetWindow != null) { PetWindow.AllowClose = true; PetWindow.Close(); }
        if (SettingsWindow != null) SettingsWindow.Close();
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life)
            life.Shutdown();
    }
}
