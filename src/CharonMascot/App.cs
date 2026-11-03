using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace CharonMascot;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                AppHost.Init();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[fatal] 启动失败: " + ex);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
