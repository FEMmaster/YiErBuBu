using Microsoft.Win32;
using System.Diagnostics;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
namespace YiErBuBu;
internal sealed class PetWindow : Window
{
    private readonly SceneView _scene = new();
    private readonly DispatcherTimer _timer;
    private InputListener? _listener;
    private Forms.NotifyIcon? _tray;
    private DateTime _lastKeyboard = DateTime.MinValue;
    private DateTime _lastMouse = DateTime.MinValue;
    private bool _locked, _visible = true, _paused;
    private double _scale = 1;
    public PetWindow()
    {
        Title = "一二和布布 · 视觉还原版 V2.3";
        Width = 540; Height = 275;
        Left = SystemParameters.WorkArea.Right - 585;
        Top = SystemParameters.WorkArea.Bottom - 320;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Content = _scene;
        MouseLeftButtonDown += (_, e) => { if (!_locked && e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        ContextMenu = CreateMenu();
        _scene.ContextMenu = ContextMenu;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Tick();
        Loaded += (_, _) => { StartListener(); _timer.Start(); };
        Closed += (_, _) => { _timer.Stop(); _listener?.Dispose(); _tray?.Dispose(); };
    }
    private ContextMenu CreateMenu()
    {
        var menu = new ContextMenu();
        Add(menu, "大小 75%", () => SetScale(.75));
        Add(menu, "大小 100%", () => SetScale(1));
        Add(menu, "大小 125%", () => SetScale(1.25));
        Add(menu, "大小 150%", () => SetScale(1.5));
        menu.Items.Add(new Separator());
        Add(menu, "锁定 / 解锁位置", () => _locked = !_locked);
        Add(menu, "暂停 / 恢复输入动画", TogglePause);
        Add(menu, "重新启动键鼠监听", StartListener);
        menu.Items.Add(new Separator());
        var autoStartItem = new MenuItem
        {
            Header = "开机自启",
            IsCheckable = true,
            IsChecked = IsAutoStartEnabled()
        };
        
        autoStartItem.Click += (_, _) =>
        {
            try
            {
                SetAutoStart(autoStartItem.IsChecked);
            }
            catch (Exception ex)
            {
                autoStartItem.IsChecked = IsAutoStartEnabled();
        
                MessageBox.Show(
                    "修改开机自启失败：" + ex.Message,
                    "一二和布布",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        };
        
        menu.Items.Add(autoStartItem);
        menu.Items.Add(new Separator());
        Add(menu, "退出", Close);
        return menu;
    }
    
private const string AutoStartName = "YiErBuBu";

private const string AutoStartRegistryPath =
    @"Software\Microsoft\Windows\CurrentVersion\Run";

// 判断是否已经启用开机自启
private static bool IsAutoStartEnabled()
{
    using var key = Registry.CurrentUser.OpenSubKey(
        AutoStartRegistryPath, false);

    return key?.GetValue(AutoStartName) is string;
}

// 开启或关闭开机自启
private static void SetAutoStart(bool enabled)
{
    using var key = Registry.CurrentUser.OpenSubKey(
        AutoStartRegistryPath, true);

    if (key == null)
        throw new InvalidOperationException(
            "无法打开 Windows 启动项注册表。");

    if (enabled)
    {
        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "无法获取程序路径。");

        // 避免注册到 dotnet.exe 或其他启动宿主
        if (!string.Equals(
            System.IO.Path.GetFileName(exePath),
            "YiErBuBu.exe",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "请使用发布后的 YiErBuBu.exe 启动程序，" +
                "再开启开机自启。");
        }

        key.SetValue(
            AutoStartName,
            $"\"{exePath}\"",
            RegistryValueKind.String);
    }
    else
    {
        key.DeleteValue(AutoStartName, false);
    }
}

    private static void Add(ContextMenu menu, string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }
    private void SetScale(double s) { _scale = s; Width = 540 * s; Height = 275 * s; }
    private void TogglePause() { _paused = !_paused; if (_paused) _scene.HeldKeys.Clear(); }
    private void StartListener()
    {
        _listener?.Dispose();
        _listener = new InputListener();
        _listener.KeyboardChanged += key =>
        {
            if (_paused) return;
            if (key.Down)
            {
                _scene.HeldKeys.Add(key.VkCode);
                _scene.LastKey = key.VkCode;
                _lastKeyboard = DateTime.UtcNow;
            }
            else _scene.HeldKeys.Remove(key.VkCode);
        };
        _listener.MouseChanged += action =>
        {
            if (_paused) return;
            if (action == MouseAction.LeftDown) _scene.MouseLeft = true;
            if (action == MouseAction.LeftUp) _scene.MouseLeft = false;
            if (action == MouseAction.RightDown) _scene.MouseRight = true;
            if (action == MouseAction.RightUp) _scene.MouseRight = false;
            if (action == MouseAction.Wheel || action == MouseAction.Move || action == MouseAction.LeftDown || action == MouseAction.RightDown)
                _lastMouse = DateTime.UtcNow;
        };
        if (!_listener.IsActive)
            MessageBox.Show("键盘或鼠标全局监听未能启用；请检查安全软件限制。", "一二和布布");
    }
    private void Tick()
    {
        var now = DateTime.UtcNow;
        double elapsedKey = (now - _lastKeyboard).TotalMilliseconds;
        _scene.KeyboardPulse = elapsedKey < 95 ? 1 : elapsedKey < 220 ? 1 - (elapsedKey - 95) / 125 : 0;
        _scene.MousePulse = Math.Clamp(1 - (now - _lastMouse).TotalMilliseconds / 170, 0, 1);
        var p = Forms.Cursor.Position;
        var centerX = Left + Width * .25;
        var centerY = Top + Height * .68;
        _scene.MouseX = Math.Clamp((p.X - centerX) / 280, -1, 1);
        _scene.MouseY = Math.Clamp((p.Y - centerY) / 240, -1, 1);
        _scene.Step(now);
        _scene.InvalidateVisual();
    }
    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏", null, (_, _) => Dispatcher.Invoke(Toggle));
        menu.Items.Add("暂停 / 恢复", null, (_, _) => Dispatcher.Invoke(TogglePause));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Close));
        _tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "一二和布布 V2.3",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(Toggle);
    }
    private void Toggle() { _visible = !_visible; if (_visible) Show(); else Hide(); }
}
