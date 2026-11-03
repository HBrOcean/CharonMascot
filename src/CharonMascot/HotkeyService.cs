using System;
using SharpHook;
using SharpHook.Data;

namespace CharonMascot;

/// <summary>全局热键（SharpHook）。修饰键状态自己维护，避免各平台 EventMask 差异。</summary>
public sealed class HotkeyService : IDisposable
{
    readonly TaskPoolGlobalHook _hook;
    readonly object _lock = new();
    bool _ctrl, _alt, _shift;

    public event Action<string> HotkeyPressed;

    public HotkeyService()
    {
        _hook = new TaskPoolGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    public void Start()
    {
        _hook.RunAsync();
    }

    void OnKeyPressed(object sender, KeyboardHookEventArgs e)
    {
        lock (_lock)
        {
            switch (e.Data.KeyCode)
            {
                case KeyCode.VcLeftControl:
                case KeyCode.VcRightControl:
                    _ctrl = true; return;
                case KeyCode.VcLeftAlt:
                case KeyCode.VcRightAlt:
                    _alt = true; return;
                case KeyCode.VcLeftShift:
                case KeyCode.VcRightShift:
                    _shift = true; return;
            }

            bool ctrl = _ctrl, alt = _alt, shift = _shift;

            if (ctrl && alt && !shift)
            {
                switch (e.Data.KeyCode)
                {
                    case KeyCode.VcH: HotkeyPressed?.Invoke("toggle"); e.SuppressEvent = true; break;
                    case KeyCode.VcS: HotkeyPressed?.Invoke("settings"); e.SuppressEvent = true; break;
                    case KeyCode.VcQ: HotkeyPressed?.Invoke("quit"); e.SuppressEvent = true; break;
                }
            }
        }
    }

    void OnKeyReleased(object sender, KeyboardHookEventArgs e)
    {
        lock (_lock)
        {
            switch (e.Data.KeyCode)
            {
                case KeyCode.VcLeftControl:
                case KeyCode.VcRightControl:
                    _ctrl = false; break;
                case KeyCode.VcLeftAlt:
                case KeyCode.VcRightAlt:
                    _alt = false; break;
                case KeyCode.VcLeftShift:
                case KeyCode.VcRightShift:
                    _shift = false; break;
            }
        }
    }

    public void Dispose()
    {
        try { _hook.Dispose(); } catch { }
    }
}
