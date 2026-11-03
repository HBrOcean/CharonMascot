using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CharonMascot.SpineRuntime;

namespace CharonMascot;

public class PetWindow : Window
{
    readonly AppHost _host;
    readonly PetControl _pet;
    readonly DispatcherTimer _timer;
    readonly Stopwatch _clock = Stopwatch.StartNew();

    double _lastTime;
    double _actionUntil = -1;
    double _nextIdleAction;
    bool _dragging;
    Point _pressPoint;
    bool _moved;

    public bool AllowClose { get; set; }

    public PetWindow(AppHost host)
    {
        _host = host;

        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        Topmost = host.Config.Topmost;
        ShowInTaskbar = false;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        ExtendClientAreaToDecorationsHint = false;
        Title = "卡戎桌宠 · CharonMascot";
        try
        {
            var icon = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icons", "app.png");
            if (System.IO.File.Exists(icon)) Icon = new WindowIcon(icon);
        }
        catch { }

        _pet = new PetControl { IsHitTestVisible = false, Model = host.Model };
        Content = _pet;

        ApplyScale();

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnTick);
        _timer.Start();

        _nextIdleAction = _clock.Elapsed.TotalSeconds + Random.Shared.Next(20, Math.Max(25, host.Config.IdleActionMaxSeconds));

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnWheel;
        Closed += OnClosed;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        PlaceWindow();
        if (_host.Model != null && _host.Model.HasAnimation("idle"))
            _host.Model.Play("idle", true);
    }

    void PlaceWindow()
    {
        var cfg = _host.Config;
        if (cfg.WindowX.HasValue && cfg.WindowY.HasValue)
        {
            Position = new PixelPoint((int)cfg.WindowX.Value, (int)cfg.WindowY.Value);
            return;
        }
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen == null) return;
        var wa = screen.WorkingArea;
        double sc = screen.Scaling <= 0 ? 1 : screen.Scaling;
        int x = wa.X + (int)Math.Max(0, wa.Width - Width * sc - 80 * sc);
        int y = wa.Y + (int)Math.Max(0, wa.Height - Height * sc - 80 * sc);
        Position = new PixelPoint(x, y);
    }

    /// <summary>根据模型包围盒与缩放比重新计算窗口尺寸。</summary>
    public void ApplyScale()
    {
        var model = _host.Model;
        if (model == null) return;
        var b = model.Bounds;
        double targetHeight = 240.0 * _host.Config.Scale;   // scale=1.0 时角色高约 240px
        double pixelScale = targetHeight / Math.Max(b.Height, 1);
        double w = b.Width * pixelScale + 16;
        double h = b.Height * pixelScale + 16;
        Width = Math.Max(40, Math.Min(w, 2000));
        Height = Math.Max(40, Math.Min(h, 2000));
    }

    public void OnModelChanged()
    {
        ApplyScale();
        _pet.Model = _host.Model;
        _pet.ZoomFactor = 1.0;
        _actionUntil = -1;
        if (_host.Model != null && _host.Model.HasAnimation("idle"))
            _host.Model.Play("idle", true);
    }

    void OnTick(object sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = _lastTime == 0 ? 0.016 : Math.Min(0.05, now - _lastTime);
        _lastTime = now;

        var model = _host.Model;
        if (model == null) return;

        model.Update((float)dt);

        if (_actionUntil > 0 && now >= _actionUntil)
        {
            _actionUntil = -1;
            model.Play("idle", true);
        }

        if (_host.Config.IdleRandomAction && _actionUntil < 0 && now >= _nextIdleAction)
        {
            var candidates = model.AnimationNames
                .Where(n => n is "giddy" or "posture" or "unique" or "sleep" or "skill1")
                .ToArray();
            if (candidates.Length > 0)
                PlayOnce(candidates[Random.Shared.Next(candidates.Length)]);
            _nextIdleAction = now + Random.Shared.Next(30, Math.Max(40, _host.Config.IdleActionMaxSeconds * 2));
        }

        _pet.InvalidateVisual();
    }

    /// <summary>播放一次性动作，之后自动回到待机。</summary>
    public void PlayOnce(string name, float fallbackDuration = 0)
    {
        var model = _host.Model;
        if (model == null || !model.HasAnimation(name)) return;
        var anim = model.Data.FindAnimation(name);
        float dur = anim != null && anim.Duration > 0.05f ? anim.Duration : (fallbackDuration > 0 ? fallbackDuration : 1.2f);
        model.Play(name, false, 0.15f);
        _actionUntil = _clock.Elapsed.TotalSeconds + Math.Max(0.4, dur) + 0.05;
        _nextIdleAction = _actionUntil + Random.Shared.Next(20, Math.Max(30, _host.Config.IdleActionMaxSeconds));
    }

    void OnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        _pressPoint = pt.Position;
        _moved = false;
        if (pt.Properties.IsRightButtonPressed)
        {
            ShowMenu();
            e.Handled = true;
            return;
        }
        if (pt.Properties.IsLeftButtonPressed)
        {
            if (!_host.Config.LockPosition)
            {
                _dragging = true;
                BeginMoveDrag(e);
            }
            e.Handled = true;
        }
    }

    void OnPointerMoved(object sender, PointerEventArgs e)
    {
        if (_dragging)
        {
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _pressPoint.X) > 4 || Math.Abs(p.Y - _pressPoint.Y) > 4) _moved = true;
        }
    }

    void OnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        bool wasDragging = _dragging;
        _dragging = false;
        if (wasDragging)
        {
            _host.SaveWindowPosition();
            if (!_moved) Click();
        }
        else if (!_moved && e.InitialPressMouseButton == MouseButton.Left)
        {
            Click();
        }
        e.Handled = true;
    }

    void Click()
    {
        var model = _host.Model;
        if (model == null) return;
        var candidates = new[] { "giddy", "hit", "skill1", "posture", "unique", "sleep", "skill2", "skill3" }
            .Where(model.HasAnimation).ToArray();
        if (candidates.Length == 0) return;
        PlayOnce(candidates[Random.Shared.Next(candidates.Length)]);
    }

    void OnWheel(object sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0) return;
        double step = e.Delta.Y > 0 ? 0.1 : -0.1;
        _host.SetScale(Math.Round(_host.Config.Scale + step, 2));
        e.Handled = true;
    }

    void ShowMenu()
    {
        var model = _host.Model;
        var flyout = new MenuFlyout();
        var items = flyout.Items;

        var actionMenu = new MenuItem { Header = "播放动作" };
        if (model != null)
        {
            foreach (var name in model.AnimationNames)
            {
                var n = name;
                var mi = new MenuItem { Header = DisplayName(n) };
                mi.Click += (_, _) => PlayOnce(n);
                actionMenu.Items.Add(mi);
            }
        }
        items.Add(actionMenu);

        var garmentMenu = new MenuItem { Header = "切换装扮" };
        foreach (var (id, gname) in AppHost.Garments)
        {
            var gid = id;
            var mi = new MenuItem { Header = gname, ToggleType = MenuItemToggleType.Radio, IsChecked = _host.Config.GarmentId == gid };
            mi.Click += (_, _) => _host.SwitchGarment(gid);
            garmentMenu.Items.Add(mi);
        }
        items.Add(garmentMenu);

        var scaleMenu = new MenuItem { Header = "缩放" };
        foreach (var s in new[] { 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0 })
        {
            var sv = s;
            var mi = new MenuItem
            {
                Header = $"{(int)(s * 100)}%",
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = Math.Abs(_host.Config.Scale - s) < 0.01
            };
            mi.Click += (_, _) => _host.SetScale(sv);
            scaleMenu.Items.Add(mi);
        }
        items.Add(scaleMenu);

        items.Add(new Separator());

        var topMost = new MenuItem { Header = "窗口置顶", ToggleType = MenuItemToggleType.CheckBox, IsChecked = _host.Config.Topmost };
        topMost.Click += (_, _) =>
        {
            _host.Config.Topmost = !_host.Config.Topmost;
            Topmost = _host.Config.Topmost;
            _host.Config.Save();
        };
        items.Add(topMost);

        var lockPos = new MenuItem { Header = "锁定位置", ToggleType = MenuItemToggleType.CheckBox, IsChecked = _host.Config.LockPosition };
        lockPos.Click += (_, _) =>
        {
            _host.Config.LockPosition = !_host.Config.LockPosition;
            _host.Config.Save();
        };
        items.Add(lockPos);

        var idle = new MenuItem { Header = "空闲时自动做动作", ToggleType = MenuItemToggleType.CheckBox, IsChecked = _host.Config.IdleRandomAction };
        idle.Click += (_, _) =>
        {
            _host.Config.IdleRandomAction = !_host.Config.IdleRandomAction;
            _host.Config.Save();
        };
        items.Add(idle);

        items.Add(new Separator());

        var shot = new MenuItem { Header = "保存截图…" };
        shot.Click += (_, _) => SaveShot();
        items.Add(shot);

        var settings = new MenuItem { Header = "设置..." };
        settings.Click += (_, _) => _host.OpenSettings();
        items.Add(settings);

        var hide = new MenuItem { Header = "隐藏（Ctrl+Alt+H 唤回）" };
        hide.Click += (_, _) => _host.ToggleVisible();
        items.Add(hide);

        var quit = new MenuItem { Header = "退出" };
        quit.Click += (_, _) => _host.Quit();
        items.Add(quit);

        flyout.ShowAt(this);
    }

    public void SaveShotTo(string path)
    {
        _pet.SaveSnapshot(path);
    }

    public void SaveShot()
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CharonMascot");
        try { System.IO.Directory.CreateDirectory(dir); } catch { }
        var path = System.IO.Path.Combine(dir, $"charon_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        if (_pet.SaveSnapshot(path)) Console.WriteLine("[shot] " + path);
    }

    public static string DisplayName(string anim) => anim switch
    {
        "idle" => "待机",
        "born" => "登场",
        "change" => "换装",
        "die" => "退场",
        "freeze" => "定格",
        "giddy" => "开心",
        "hit" => "受击",
        "posture" => "姿势",
        "skill1" => "技能一",
        "skill2" => "技能二",
        "skill3" => "技能三",
        "sleep" => "睡觉",
        "unique" => "专属",
        _ => anim,
    };

    void OnClosed(object sender, EventArgs e)
    {
        _timer.Stop();
        _pet.Model = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && !IsVisible && !AllowClose)
        {
            // 被系统隐藏时不做额外处理
        }
    }
}
