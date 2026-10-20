using System;
using System.Collections.Generic;
using System.Drawing;
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
    private DateTime _lastWheel = DateTime.MinValue;
    private bool _mouseLeft,_mouseRight,_locked,_visible=true,_paused;
    private int _lastVk,_typingCounter;
    private bool _mouseMoving;
    public PetWindow()
    {
        Title="一二和布布 · V3.0 动态桌宠";
        Width=850;Height=440;
        Left=Math.Max(SystemParameters.WorkArea.Left,SystemParameters.WorkArea.Right-900);
        Top=Math.Max(SystemParameters.WorkArea.Top,SystemParameters.WorkArea.Bottom-480);
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
        ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;
        Content=_scene;
        MouseLeftButtonDown+=(_,e)=>{if(!_locked&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)&&e.ButtonState==MouseButtonState.Pressed)DragMove();};
        ContextMenu=CreateMenu();_scene.ContextMenu=ContextMenu;
        _timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(33)};
        _timer.Tick+=(_,_)=>Tick();
        Loaded+=(_,_)=>{StartListener();CreateTray();_timer.Start();};
        Closed+=(_,_)=>{_timer.Stop();_listener?.Dispose();_tray?.Dispose();};
    }
    private ContextMenu CreateMenu()
    {
        var menu=new ContextMenu();
        Add(menu,"缩放 75%",()=>SetScale(.75));
        Add(menu,"缩放 100%",()=>SetScale(1));
        Add(menu,"缩放 125%",()=>SetScale(1.25));
        menu.Items.Add(new Separator());
        Add(menu,"锁定 / 解锁",()=>_locked=!_locked);
        Add(menu,"暂停 / 恢复",TogglePause);
        menu.Items.Add(new Separator());
        Add(menu,"退出",Close);
        return menu;
    }
    private static void Add(ContextMenu menu,string label,Action action)
    {var item=new MenuItem{Header=label};item.Click+=(_,_)=>action();menu.Items.Add(item);}
    private void SetScale(double scale){Width=850*scale;Height=440*scale;}
    private void TogglePause()
    {
        _paused=!_paused;
        if(_paused){_scene.HeldKeys.Clear();_mouseLeft=_mouseRight=_mouseMoving=false;_scene.BubuPose="idle";_scene.YierPose="idle";_scene.InvalidateVisual();}
    }
    private void StartListener()
    {
        _listener?.Dispose();_listener=new InputListener();
        _listener.KeyboardChanged+=key=>
        {
            if(_paused)return;
            if(key.Down)
            {
                if(_scene.HeldKeys.Add(key.VkCode))_typingCounter++;
                _lastVk=key.VkCode;_lastKeyboard=DateTime.UtcNow;
            }
            else _scene.HeldKeys.Remove(key.VkCode);
        };
        _listener.MouseChanged+=action=>
        {
            if(_paused)return;
            if(action==MouseAction.LeftDown)_mouseLeft=true;
            if(action==MouseAction.LeftUp)_mouseLeft=false;
            if(action==MouseAction.RightDown)_mouseRight=true;
            if(action==MouseAction.RightUp)_mouseRight=false;
            if(action==MouseAction.Wheel)_lastWheel=DateTime.UtcNow;
            if(action==MouseAction.Move){_mouseMoving=true;_lastMouse=DateTime.UtcNow;}
        };
        if(!_listener.IsActive)MessageBox.Show("键鼠监听未启用，请检查系统权限。","一二和布布");
    }
    private void Tick()
    {
        var now=DateTime.UtcNow;
        if(_paused)return;
        string bubu="idle";
        if(_mouseLeft)bubu="left";
        else if(_mouseRight)bubu="right";
        else if((now-_lastWheel).TotalMilliseconds<240)bubu="scroll";
        else if(_mouseMoving&&(now-_lastMouse).TotalMilliseconds<165)
            bubu=((now-_lastMouse).TotalMilliseconds/80)%2<1?"move_1":"move_2";
        else _mouseMoving=false;
        string yier="idle";
        if((now-_lastKeyboard).TotalMilliseconds<190||_scene.HeldKeys.Count>0)
        {
            if(_scene.HeldKeys.Count>=3)yier="burst";
            else if(_lastVk==32)yier="space";
            else if(IsRight(_lastVk))yier="right";
            else if(IsMiddle(_lastVk))yier="mid";
            else yier="left";
            if(_typingCounter>3 && (now-_lastKeyboard).TotalMilliseconds<130 && _typingCounter%2==0)yier="burst";
        }
        else _typingCounter=0;
        if(bubu!=_scene.BubuPose||yier!=_scene.YierPose)
        {_scene.BubuPose=bubu;_scene.YierPose=yier;_scene.InvalidateVisual();}
    }
    private static bool IsRight(int vk)=> "UIOPJKLNM".Contains((char)vk)||vk==13||vk==8;
    private static bool IsMiddle(int vk)=> "RTYFGHVB".Contains((char)vk);
    private void CreateTray()
    {
        var menu=new Forms.ContextMenuStrip();
        menu.Items.Add("显示 / 隐藏",null,(_,_)=>Dispatcher.Invoke(Toggle));
        menu.Items.Add("暂停 / 恢复",null,(_,_)=>Dispatcher.Invoke(TogglePause));
        menu.Items.Add("缩放 75%",null,(_,_)=>Dispatcher.Invoke(()=>SetScale(.75)));
        menu.Items.Add("缩放 100%",null,(_,_)=>Dispatcher.Invoke(()=>SetScale(1)));
        menu.Items.Add("退出",null,(_,_)=>Dispatcher.Invoke(Close));
        _tray=new Forms.NotifyIcon{Icon=SystemIcons.Application,Text="一二和布布 V3.0",Visible=true,ContextMenuStrip=menu};
        _tray.DoubleClick+=(_,_)=>Dispatcher.Invoke(Toggle);
    }
    private void Toggle(){_visible=!_visible;if(_visible)Show();else Hide();}
}
