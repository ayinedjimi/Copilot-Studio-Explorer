using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace StudioDataverse;

public partial class App : Application
{
    private Window? window;
    private bool errorDialogOpen;
    public App()
    {
        UnhandledException += OnUnhandledException;
        InitializeComponent();
    }
    private async void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        var detail = RecordError(e.Exception);
        // A native process termination or stack overflow cannot be intercepted here.
        e.Handled = true;
        if (window == null) return;
        await ShowErrorAsync(window, detail);
    }
    public static async Task ShowErrorAsync(Window owner, string detail)
    {
        if (Current is not App app || app.errorDialogOpen || owner.Content is not FrameworkElement root || root.XamlRoot == null) return;
        app.errorDialogOpen = true;
        try
        {
            var text = new TextBox { Text = detail, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Height = 240 };
            var body = new StackPanel { Spacing = 12 };
            body.Children.Add(new TextBlock { Text = "Une erreur est survenue. Vous pouvez copier les détails ci-dessous.", TextWrapping = TextWrapping.Wrap });
            body.Children.Add(text);
            var dialog = new ContentDialog { Title = "Erreur de Copilot Studio Explorer", Content = body, PrimaryButtonText = "Copier les détails", CloseButtonText = "Fermer", XamlRoot = root.XamlRoot };
            dialog.PrimaryButtonClick += (_, _) => { var data = new DataPackage(); data.SetText(detail); Clipboard.SetContent(data); };
            await dialog.ShowAsync();
        }
        catch { /* The journal is retained if the dialog itself cannot be shown. */ }
        finally { app.errorDialogOpen = false; }
    }
    public static string RecordError(Exception exception)
    {
        var detail = ExplorerRedaction.Safe(exception.ToString());
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "erreurs.log");
        if(Current is App app && app.window is MainWindow main && !main.WriteErrorLog)return detail+Environment.NewLine+Environment.NewLine+"Journal désactivé dans les paramètres.";
        try
        {
            System.IO.File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + Environment.NewLine + detail + Environment.NewLine + Environment.NewLine);
            return detail + Environment.NewLine + Environment.NewLine + "Journal : " + path;
        }
        catch
        {
            return detail + Environment.NewLine + Environment.NewLine + "Le journal n’a pas pu être écrit dans le dossier de l’application.";
        }
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
        catch (Exception ex) { Log("Startup failed: " + ExplorerRedaction.Safe(ex.ToString())); RecordError(ex); throw; }
    }
    private static void Log(string text)
    {
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "startup.log"), DateTimeOffset.Now + " " + text + Environment.NewLine); }
        catch { }
    }
}
