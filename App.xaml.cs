using System.Windows;
namespace YiErBuBu;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var window = new PetWindow();
        MainWindow = window;
        window.Show();
    }
}
