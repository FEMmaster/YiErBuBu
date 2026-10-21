using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YiErBuBu;

internal sealed class SceneView : FrameworkElement
{
    public HashSet<int> HeldKeys { get; } = new();
    public int LastKey { get; set; } = -1;
    public bool MouseLeft { get; set; }
    public bool MouseRight { get; set; }
    public double MouseX { get; set; }
    public double MouseY { get; set; }
    public double MousePulse { get; set; }
    public double KeyboardPulse { get; set; }

    private const double BaseW = 540, BaseH = 275;
    private static readonly Typeface UiFace = new("Segoe UI");
    private static readonly BitmapImage Bubu = Load("bubu_body.png");
    private static readonly BitmapImage BubuBlink = Load("bubu_blink.png");
    private static readonly BitmapImage BubuLeft = Load("bubu_paw_left.png");
    private static readonly BitmapImage BubuRight = Load("bubu_paw_right.png");
    private static readonly BitmapImage Yier = Load("yier_body.png");
    private static readonly BitmapImage YierBlink = Load("yier_blink.png");
    private static readonly BitmapImage YierLeft = Load("yier_paw_left.png");
    private static readonly BitmapImage YierRight = Load("yier_paw_right.png");
    private readonly Random _random = new();
    private DateTime _nextBubuBlink = DateTime.UtcNow.AddSeconds(3);
    private DateTime _nextYierBlink = DateTime.UtcNow.AddSeconds(4);
    private DateTime _bubuBlinkUntil, _yierBlinkUntil;
    private Point _bubuPaw;
    private bool _pawsInitialized;
    // 人物位置和尺寸
    private static readonly Rect BubuBox = new(182, 55, 122, 125);
    private static readonly Rect YierBox = new(280, 65, 136, 138);
    
    // 肩膀相对位置，取值通常为 0~1
    private static readonly Point BubuShoulderUV = new(0.32, 0.85);
    private static readonly Point BubuOtherShoulderUV = new(0.77, 0.87);
    
    private static readonly Point YierLeftShoulderUV = new(0.25, 0.87);
    private static readonly Point YierRightShoulderUV = new(0.76, 0.87);
    private Point _yierLeftPaw;
    private Point _yierRightPaw;
    private DateTime _lastStep = DateTime.UtcNow;
    private const double DeviceAngle = 10;
    private const double RotateX = BaseW / 2, RotateY = BaseH / 2;
    // 鼠标垫的唯一几何定义。鼠标中心从这四个顶点自动计算。
    private static readonly Point MousePadA = new(151, 175);
    private static readonly Point MousePadB = new(227, 188);
    private static readonly Point MousePadC = new(195, 235);
    private static readonly Point MousePadD = new(110, 212);

    // 保持鼠标垫已有的尺寸、位置和角度不变。
    private const double PadPivotX = 127, PadPivotY = 203;
    private const double PadScale = .88;
    private const double PadTranslateX = 44, PadTranslateY = -18;

    private static BitmapImage Load(string name)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri($"pack://application:,,,/assets/characters/{name}");
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    public void Step(DateTime now)
    {
        double dt = Math.Clamp((now - _lastStep).TotalSeconds, 0.0, 0.1);
        _lastStep = now;
        if (now >= _nextBubuBlink)
        {
            _bubuBlinkUntil = now.AddMilliseconds(130);
            _nextBubuBlink = now.AddSeconds(2.3 + _random.NextDouble() * 5);
        }
        if (now >= _nextYierBlink)
        {
            _yierBlinkUntil = now.AddMilliseconds(135);
            _nextYierBlink = now.AddSeconds(2.5 + _random.NextDouble() * 5);
        }
        // 待机爪子靠近肩膀；肩膀与人物使用完全相同的旋转坐标。
        Point bubuRest = RestPaw(ShoulderWorld(BubuBox, BubuShoulderUV), -12, 13);
        Point yierLeftRest = RestPaw(ShoulderWorld(YierBox, YierLeftShoulderUV), -11, 14);
        Point yierRightRest = RestPaw(ShoulderWorld(YierBox, YierRightShoulderUV), 11, 14);

        if (!_pawsInitialized)
        {
            _bubuPaw = bubuRest;
            _yierLeftPaw = yierLeftRest;
            _yierRightPaw = yierRightRest;
            _pawsInitialized = true;
        }

        // 鼠标中心由鼠标垫四个顶点得出。与绘制阶段共享同一套变换。
        Point mouse = RotateDevice(MousePadLocalToScene(GetMouseLocalPosition()));
        bool mouseActive = MouseLeft || MouseRight || MousePulse > .04;
        Point bubuTarget = mouseActive
            ? new Point(mouse.X, mouse.Y - (MouseLeft || MouseRight ? 1 : 6))
            : bubuRest;
        _bubuPaw = Approach(_bubuPaw, bubuTarget, dt, 17);

        Point leftTarget = yierLeftRest, rightTarget = yierRightRest;
        bool leftDown = false, rightDown = false;
        foreach (int vk in HeldKeys)
        {
            if (!KeyboardLayout.TryGetKey(vk, out var key)) continue;
            Point p = KeyboardTarget(key);
            // Each hand moves independently to the actual pressed key center.
            if (p.X <= 354 || (vk == 32 && !leftDown))
            {
                leftTarget = p;
                leftDown = true;
            }
            else
            {
                rightTarget = p;
                rightDown = true;
            }
        }
        // For short taps whose KeyUp preceded the next frame, use LastKey + pulse.
        if (!leftDown && !rightDown && KeyboardPulse > .08 &&
            KeyboardLayout.TryGetKey(LastKey, out var recent))
        {
            var p = KeyboardTarget(recent);
            if (p.X <= 354) { leftTarget = p; leftDown = true; }
            else { rightTarget = p; rightDown = true; }
        }
        _yierLeftPaw = Approach(_yierLeftPaw,
            leftDown ? leftTarget : yierLeftRest, dt, 20);
        _yierRightPaw = Approach(_yierRightPaw,
            rightDown ? rightTarget : yierRightRest, dt, 20);
    }

    // 四边形顶点平均值：改变顶点后鼠标中心自动随之更新。
    private static Point MousePadCenter() => new(
        (MousePadA.X + MousePadB.X + MousePadC.X + MousePadD.X) / 4,
        (MousePadA.Y + MousePadB.Y + MousePadC.Y + MousePadD.Y) / 4);

    private Point GetMouseLocalPosition()
    {
        Point center = MousePadCenter();
        return new Point(center.X + Math.Clamp(MouseX, -1, 1) * 5,
                         center.Y + Math.Clamp(MouseY, -1, 1) * 2);
    }

    private static Point MousePadLocalToScene(Point local) => new(
        PadPivotX + (local.X - PadPivotX) * PadScale + PadTranslateX,
        PadPivotY + (local.Y - PadPivotY) * PadScale + PadTranslateY);

    private static Point ShoulderWorld(Rect box, Point uv)
    {
        Point p = GetShoulder(box, uv);
        p.Offset(-5, 0); // 保留你原先向左调整肩膀的设定
        return p;
    }

    private static Point RestPaw(Point shoulder, double dx, double dy) =>
        new(shoulder.X + dx, shoulder.Y + dy);

    private static Point LimitReach(Point target, Point shoulder, double reach)
    {
        double dx = target.X - shoulder.X, dy = target.Y - shoulder.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= reach || distance < 0.001) return target;
        return new Point(shoulder.X + dx * reach / distance,
                         shoulder.Y + dy * reach / distance);
    }

    private static Point Approach(Point current, Point target, double dt, double speed)
    {
        double t = 1 - Math.Exp(-speed * dt);
        return new Point(current.X + (target.X - current.X) * t,
                         current.Y + (target.Y - current.Y) * t);
    }
    private static Point RotateDevice(Point p)
    {
        double a = DeviceAngle * Math.PI / 180;
        double x = p.X - RotateX, y = p.Y - RotateY;
        return new Point(RotateX + x * Math.Cos(a) - y * Math.Sin(a),
                         RotateY + x * Math.Sin(a) + y * Math.Cos(a));
    }
    private static Point KeyboardTarget(KeyShape k)
    {
        double u = Math.Clamp((445 - k.CenterX) / 207.0, 0, 1);
        double v = Math.Clamp((247 - k.CenterY) / 77.0, 0, 1);
        return RotateDevice(ProjectKey(u, v, new Point(260, 169),
            new Point(430, 185), new Point(456, 239), new Point(228, 218)));
    }

    protected override void OnRender(DrawingContext d)
    {
        base.OnRender(d);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        d.PushTransform(new ScaleTransform(ActualWidth / BaseW, ActualHeight / BaseH));
        DrawShadows(d);

        // Anatomical order: arms emerge FROM BEHIND the bodies.
        // Neither a forearm nor its shoulder line is ever drawn over a face.


        DrawCharacter(d,
            DateTime.UtcNow < _bubuBlinkUntil ? BubuBlink : Bubu,
            BubuBox);
        
        DrawCharacter(d,
            DateTime.UtcNow < _yierBlinkUntil ? YierBlink : Yier,
            YierBox);

        // The equipment occludes the forearms where they pass behind it.
        
        // 1. 绘制键盘和鼠标
        d.PushTransform(new RotateTransform(DeviceAngle, RotateX, RotateY));
        DrawMousePadAndMouse(d);
        DrawKeyboard(d);
        d.Pop();
                
        // 2. 绘制可以自由伸缩的手臂
        Point restingBubuPaw = RestPaw(
            ShoulderWorld(BubuBox, BubuOtherShoulderUV), 11, 13);
        

        // 所有肩膀使用相同的角色旋转+偏移计算。
        Point bubuShoulder = ShoulderWorld(BubuBox, BubuShoulderUV);
        Point bubuOtherShoulder = ShoulderWorld(BubuBox, BubuOtherShoulderUV);
        Point yierLeftShoulder = ShoulderWorld(YierBox, YierLeftShoulderUV);
        Point yierRightShoulder = ShoulderWorld(YierBox, YierRightShoulderUV);

        // 动态手臂：起点跟随人物，终点跟随爪子
        DrawArm(d, bubuShoulder, _bubuPaw,
            "#D5A172", "#3F221C", 10);
        
        DrawArm(d, bubuOtherShoulder, restingBubuPaw,
            "#D5A172", "#3F221C", 9.5);
        
        DrawArm(d, yierLeftShoulder, _yierLeftPaw,
            "#FFFDF8", "#351716", 11);
        
        DrawArm(d, yierRightShoulder, _yierRightPaw,
            "#FFFDF8", "#351716", 11);

        
        // 3. 最后绘制四只爪子
        DrawPaw(d, BubuLeft, _bubuPaw, 42, 31);
        DrawPaw(d, BubuRight, restingBubuPaw, 42, 31);
        
        DrawPaw(d, YierLeft, _yierLeftPaw, 42, 32);
        DrawPaw(d, YierRight, _yierRightPaw, 42, 32);

        d.Pop();
    }

    private static void DrawCharacter(DrawingContext d, ImageSource bmp, Rect box)
    {
        d.PushTransform(new RotateTransform(10, box.X + box.Width / 2, box.Y + box.Height / 2));
        d.DrawImage(bmp, box);
        d.Pop();
    }
    
    private static Point GetShoulder(Rect box, Point uv)
    {
        // 计算肩膀在未旋转人物图片中的位置
        double x = box.X + box.Width * uv.X;
        double y = box.Y + box.Height * uv.Y;
    
        // 人物中心
        double cx = box.X + box.Width / 2;
        double cy = box.Y + box.Height / 2;
    
        // 与 DrawCharacter 使用相同的旋转角度
        double angle = 10 * Math.PI / 180;
    
        double dx = x - cx;
        double dy = y - cy;
    
        return new Point(
            cx + dx * Math.Cos(angle) - dy * Math.Sin(angle),
            cy + dx * Math.Sin(angle) + dy * Math.Cos(angle)
        );
    }

    private static void DrawArm(DrawingContext d, Point shoulder, Point hand,
        string furColor, string outlineColor, double width)
    {
        // Draw a softly bowed tapered limb BEHIND the original body art.
        // There is no visible joint cut; the body masks the shoulder root.
        double dx = hand.X - shoulder.X;
        double dy = hand.Y - shoulder.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < 1) return;
        double bend = Math.Min(7, distance * .12);
        double side = hand.X < shoulder.X ? -1 : 1;
        Point ctrl = new(shoulder.X + dx * .52 + bend * side,
                         shoulder.Y + dy * .50 + bend * .30);
        var path = new StreamGeometry();
        using (var c = path.Open())
        {
            c.BeginFigure(shoulder, false, false);
            c.QuadraticBezierTo(ctrl, hand, true, false);
        }
        path.Freeze();
        var edge = new Pen(Ink(outlineColor), width + 2.8)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        var fill = new Pen(Ink(furColor), width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        d.DrawGeometry(null, edge, path);
        d.DrawGeometry(null, fill, path);
    }
    private static void DrawPaw(DrawingContext d, ImageSource bmp, Point p, double w, double h)
    {
        d.DrawImage(bmp, new Rect(p.X - w / 2, p.Y - h / 2, w, h));
    }
    private void DrawShadows(DrawingContext d)
    {
        Ellipse(d, "#14000000", null, 165, 181, 62, 9);
        Ellipse(d, "#16000000", null, 353, 236, 116, 13);
    }
    private void DrawMousePadAndMouse(DrawingContext d)
    {
        var t = new TransformGroup();
        t.Children.Add(new ScaleTransform(PadScale, PadScale, PadPivotX, PadPivotY));
        t.Children.Add(new TranslateTransform(PadTranslateX, PadTranslateY));
        d.PushTransform(t);
        DrawQuad(d, "#F7D6CF", "#8A6A64",
            MousePadA, MousePadB, MousePadC, MousePadD);
        Point localMouse = GetMouseLocalPosition();
        double mx = localMouse.X;
        double my = localMouse.Y;
        d.PushTransform(new RotateTransform(35,mx,my));
        Ellipse(d, "#FFF9F4", "#866B64", mx, my, 12, 19, 1.6);
        Line(d,"#8A6F68",1.4,mx,my+19,mx,my-1);
        if(MouseLeft) Ellipse(d,"#F1BBB2",null,mx-6,my+8,6,8);
        if(MouseRight) Ellipse(d,"#F1BBB2",null,mx+6,my+8,6,8);
        d.Pop();
        if(!MouseLeft&&!MouseRight&&MousePulse>.05)
            Ellipse(d,"#18D9A8A0",null,mx,my+12,12,5);
        d.Pop();
    }
    private void DrawKeyboard(DrawingContext d)
    {
        Point a=new(260,169), b=new(430,185), c=new(456,239), e=new(228,218);
        DrawQuad(d,"#DCCABB","#8D8177",e,c,new Point(456,247),new Point(228,226));
        DrawQuad(d,"#F8F1E7","#8D8177",a,b,c,e);
        foreach(var k in KeyboardLayout.Keys)
        {
            bool held=HeldKeys.Contains(k.Vk)||(k.Vk==160&&HeldKeys.Contains(16))||
                (k.Vk==161&&HeldKeys.Contains(16));
            double u0=Math.Clamp((445-k.X-k.W)/207.0,0,1);
            double u1=Math.Clamp((445-k.X)/207.0,0,1);
            double v0=Math.Clamp((247-k.Y-k.H)/77.0,0,1);
            double v1=Math.Clamp((247-k.Y)/77.0,0,1);
            Point p0=ProjectKey(u0,v0,a,b,c,e),p1=ProjectKey(u1,v0,a,b,c,e);
            Point p2=ProjectKey(u1,v1,a,b,c,e),p3=ProjectKey(u0,v1,a,b,c,e);
            if(!held)DrawQuad(d,"#C7B09C",null,new Point(p0.X,p0.Y+2),
                new Point(p1.X,p1.Y+2),new Point(p2.X,p2.Y+2),new Point(p3.X,p3.Y+2));
            DrawQuad(d,held?"#F0B89C":"#FFF9EF",held?"#B57258":"#C6B4A5",p0,p1,p2,p3);
            Point center=ProjectKey((u0+u1)/2,(v0+v1)/2,a,b,c,e);
            
            d.PushTransform(new RotateTransform(
                180, center.X, center.Y));
            
            Label(d, k.Caption, center.X, center.Y - 4, 6.2,
                held ? "#7D4C3A" : "#6F615A");
            
            d.Pop();

        }
    }
    private static Point ProjectKey(double u,double v,Point a,Point b,Point c,Point e)=>
        new((1-u)*(1-v)*a.X+u*(1-v)*b.X+u*v*c.X+(1-u)*v*e.X,
            (1-u)*(1-v)*a.Y+u*(1-v)*b.Y+u*v*c.Y+(1-u)*v*e.Y);
    private static void DrawQuad(DrawingContext d,string fill,string? stroke,
        Point a,Point b,Point c,Point e)
    {
        var geo=new StreamGeometry();
        using(var ctx=geo.Open())
        {
            ctx.BeginFigure(a,true,true);ctx.LineTo(b,true,false);
            ctx.LineTo(c,true,false);ctx.LineTo(e,true,false);
        }
        geo.Freeze(); d.DrawGeometry(Ink(fill),stroke==null?null:Stroke(stroke,1.2),geo);
    }
    private static Brush Ink(string hex)=>(Brush)new BrushConverter().ConvertFromString(hex)!;
    private static Pen? Stroke(string? hex,double width)=>hex is null?null:new Pen(Ink(hex),width);
    private static void Ellipse(DrawingContext d,string fill,string? stroke,
        double x,double y,double rx,double ry,double width=0)=>
        d.DrawEllipse(Ink(fill),Stroke(stroke,width),new Point(x,y),rx,ry);
    private static void Line(DrawingContext d,string color,double width,
        double x1,double y1,double x2,double y2)=>
        d.DrawLine(new Pen(Ink(color),width),new Point(x1,y1),new Point(x2,y2));
    private static void Label(DrawingContext d,string text,double x,double y,double size,string color)
    {
        var ft=new FormattedText(text,System.Globalization.CultureInfo.CurrentUICulture,
            global::System.Windows.FlowDirection.LeftToRight,UiFace,size,Ink(color),1);
        d.DrawText(ft,new Point(x-ft.Width/2,y));
    }
}
