using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;

namespace StudioDataverse;

public partial class App : Application
{
    private Window? window;
    public App()
    {
        UnhandledException += (_, e) => Log("Unhandled: " + e.Exception);
        InitializeComponent();
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            Log("Creating window");
            window = new MainWindow();
            Log("Activating window");
            window.Activate();
            var area = DisplayArea.GetFromWindowId(window.AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(area.X + 30, area.Y + 30,
                Math.Min(1520, area.Width - 60), Math.Min(960, area.Height - 60)));
            window.AppWindow.Show(true);
            if (window.AppWindow.Presenter is OverlappedPresenter presenter) presenter.Restore();
            Log("Window visible: " + window.AppWindow.IsVisible);
        }
        catch (Exception ex) { Log("Startup failed: " + ex); throw; }
    }
    private static void Log(string text)
    {
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log"), DateTimeOffset.Now + " " + text + Environment.NewLine); }
        catch { }
    }
}
