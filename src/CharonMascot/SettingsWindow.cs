using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace CharonMascot;

public class SettingsWindow : Window
{
    readonly AppHost _host;
    bool _loading = true;

    readonly Slider _scaleSlider;
    readonly TextBlock _scaleLabel;
    readonly ComboBox _garmentBox;
    readonly CheckBox _topmost, _lockPos, _idle, _enter;
    readonly Slider _idleSeconds;
    readonly TextBlock _idleLabel;

    public SettingsWindow(AppHost host)
    {
        _host = host;

        Title = "卡戎桌宠 · 设置";
        Width = 420;
        Height = 640;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        try
        {
            var icon = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "icons", "app.png");
            if (System.IO.File.Exists(icon)) Icon = new WindowIcon(icon);
        }
        catch { }

        var root = new StackPanel
        {
            Margin = new Thickness(22),
            Spacing = 14
        };

        root.Children.Add(new TextBlock
        {
            Text = "卡戎 · Charon",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold
        });
        root.Children.Add(new TextBlock
        {
            Text = "Reverse: 1999 桌宠 · 素材取自 UTTU Archive",
            FontSize = 12,
            Foreground = Brushes.Gray
        });

        root.Children.Add(new Separator());

        // 缩放
        _scaleLabel = new TextBlock { Text = "尺寸 100%", FontWeight = FontWeight.Medium };
        root.Children.Add(_scaleLabel);
        _scaleSlider = new Slider
        {
            Minimum = 50, Maximum = 400, TickFrequency = 5, IsSnapToTickEnabled = true,
            Value = host.Config.Scale * 100
        };
        _scaleSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty && !_loading)
            {
                _scaleLabel.Text = $"尺寸 {(int)_scaleSlider.Value}%";
                _host.SetScale(_scaleSlider.Value / 100.0);
            }
        };
        root.Children.Add(_scaleSlider);

        // 装扮
        root.Children.Add(new TextBlock { Text = "装扮", FontWeight = FontWeight.Medium, Margin = new Thickness(0, 6, 0, 0) });
        _garmentBox = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (_, name) in AppHost.Garments) _garmentBox.Items.Add(name);
        _garmentBox.SelectedIndex = Array.FindIndex(AppHost.Garments, g => g.Id == host.Config.GarmentId);
        if (_garmentBox.SelectedIndex < 0) _garmentBox.SelectedIndex = 0;
        _garmentBox.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            int i = _garmentBox.SelectedIndex;
            if (i >= 0 && i < AppHost.Garments.Length)
            {
                try { _host.SwitchGarment(AppHost.Garments[i].Id); }
                catch (Exception ex) { Console.Error.WriteLine("[garment] " + ex.Message); }
            }
        };
        root.Children.Add(_garmentBox);

        root.Children.Add(new Separator());

        _topmost = new CheckBox { Content = "窗口置顶", IsChecked = host.Config.Topmost };
        _topmost.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            host.Config.Topmost = _topmost.IsChecked == true;
            if (host.PetWindow != null) host.PetWindow.Topmost = host.Config.Topmost;
            host.Config.Save();
        };
        root.Children.Add(_topmost);

        _lockPos = new CheckBox { Content = "锁定位置（禁止拖动）", IsChecked = host.Config.LockPosition };
        _lockPos.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            host.Config.LockPosition = _lockPos.IsChecked == true;
            host.Config.Save();
        };
        root.Children.Add(_lockPos);

        _idle = new CheckBox { Content = "空闲时自动做动作", IsChecked = host.Config.IdleRandomAction };
        _idle.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            host.Config.IdleRandomAction = _idle.IsChecked == true;
            host.Config.Save();
        };
        root.Children.Add(_idle);

        _enter = new CheckBox { Content = "启动时播放登场动画", IsChecked = host.Config.PlayEnterOnStart };
        _enter.IsCheckedChanged += (_, _) =>
        {
            if (_loading) return;
            host.Config.PlayEnterOnStart = _enter.IsChecked == true;
            host.Config.Save();
        };
        root.Children.Add(_enter);

        _idleLabel = new TextBlock { Text = $"空闲动作间隔 {host.Config.IdleActionMaxSeconds} 秒", FontSize = 12 };
        root.Children.Add(_idleLabel);
        _idleSeconds = new Slider
        {
            Minimum = 10, Maximum = 180, TickFrequency = 5, IsSnapToTickEnabled = true,
            Value = host.Config.IdleActionMaxSeconds
        };
        _idleSeconds.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty && !_loading)
            {
                host.Config.IdleActionMaxSeconds = (int)_idleSeconds.Value;
                _idleLabel.Text = $"空闲动作间隔 {(int)_idleSeconds.Value} 秒";
                host.Config.Save();
            }
        };
        root.Children.Add(_idleSeconds);

        root.Children.Add(new Separator());

        root.Children.Add(new TextBlock
        {
            Text = "快捷键：Ctrl+Alt+H 显示/隐藏 · Ctrl+Alt+S 设置 · Ctrl+Alt+Q 退出\n鼠标：左键拖动 · 左键单击做动作 · 右键菜单 · Ctrl+滚轮缩放",
            FontSize = 12,
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var openCfg = new Button { Content = "配置目录" };
        openCfg.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(_host.Config.Dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _host.Config.Dir,
                    UseShellExecute = true
                });
            }
            catch { }
        };
        buttons.Children.Add(openCfg);

        var quit = new Button { Content = "退出程序" };
        quit.Click += (_, _) => _host.Quit();
        buttons.Children.Add(quit);

        var close = new Button { Content = "关闭" };
        close.Click += (_, _) => Close();
        buttons.Children.Add(close);

        root.Children.Add(buttons);

        Content = new ScrollViewer { Content = root };
        _loading = false;
    }
}
