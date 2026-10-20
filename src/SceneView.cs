using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace YiErBuBu;
internal sealed class SceneView : FrameworkElement
{
    public readonly HashSet<int> HeldKeys = new();
    public string BubuPose { get; set; } = "idle";
    public string YierPose { get; set; } = "idle";
    private readonly Dictionary<string, ImageSource> _sprites = new();
    public SceneView()
    {
        foreach (string pose in new[] {"idle","move_1","move_2","left","right","scroll"}) Load("bubu_" + pose);
        foreach (string pose in new[] {"idle","left","mid","right","space","burst"}) Load("yier_" + pose);
        SnapsToDevicePixels = true;
    }
    private void Load(string name)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/assets/sprites/"+name+".png",UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        _sprites[name]=image;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double scale = Math.Min(ActualWidth / 850.0, ActualHeight / 440.0);
        dc.PushTransform(new TranslateTransform((ActualWidth-850*scale)*0.5,(ActualHeight-440*scale)*0.5));
        dc.PushTransform(new ScaleTransform(scale,scale));
        // Each character is an individually switchable, transparent sprite.
        // No base image is drawn underneath the alternate poses.
        if (_sprites.TryGetValue("bubu_"+BubuPose,out var b)) dc.DrawImage(b,new Rect(0,65,410,310));
        if (_sprites.TryGetValue("yier_"+YierPose,out var y)) dc.DrawImage(y,new Rect(390,45,448,337));
        dc.Pop(); dc.Pop();
    }
}
