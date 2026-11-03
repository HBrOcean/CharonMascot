using System;
using Avalonia;
using Avalonia.Logging;
using Avalonia.X11;

namespace CharonMascot;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Console.Error.WriteLine("[fatal] " + e.ExceptionObject);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new X11PlatformOptions
            {
                // 无 GPU（虚拟机 / 远程桌面 / 沙箱）时优先软件渲染
                RenderingMode = new[] { X11RenderingMode.Software, X11RenderingMode.Egl, X11RenderingMode.Glx }
            })
            .LogToTrace(LogEventLevel.Warning);
}
