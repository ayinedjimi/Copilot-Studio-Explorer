using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace StudioDataverse;

public sealed class AgentAvatar : UserControl
{
    public static readonly DependencyProperty IconBase64Property = DependencyProperty.Register(nameof(IconBase64), typeof(string), typeof(AgentAvatar), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty InitialProperty = DependencyProperty.Register(nameof(Initial), typeof(string), typeof(AgentAvatar), new PropertyMetadata("A", InitialChanged));
    public string? IconBase64 { get => (string?)GetValue(IconBase64Property); set => SetValue(IconBase64Property, value); }
    public string Initial { get => (string)GetValue(InitialProperty); set => SetValue(InitialProperty, value); }
    private readonly Image picture = new() { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
    private readonly Image defaultPicture = new() { Source = new SvgImageSource(new Uri("ms-appx:///Assets/agent-default.svg")), Stretch = Stretch.Uniform };
    private readonly TextBlock initial = new() { Text = "A", FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 104, 83, 184)) };
    private int generation;
    public AgentAvatar()
    {
        Width = Height = 34;
        var grid = new Grid(); grid.Children.Add(defaultPicture); grid.Children.Add(picture);
        Content = new Border { Child = grid, CornerRadius = new CornerRadius(9), Padding = new Thickness(2), Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 238, 235, 252)) };
        picture.ImageFailed += (_, _) => { picture.Visibility = Visibility.Collapsed; defaultPicture.Visibility = Visibility.Visible; };
    }
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args) => _ = ((AgentAvatar)target).LoadAsync();
    private static void InitialChanged(DependencyObject target, DependencyPropertyChangedEventArgs args) => ((AgentAvatar)target).initial.Text = args.NewValue as string ?? "A";
    private async Task LoadAsync()
    {
        var current = ++generation;
        picture.Visibility = Visibility.Collapsed; picture.Source = null; defaultPicture.Visibility = Visibility.Visible;
        var bytes = AgentIconPayload.Decode(IconBase64);
        if (bytes == null) return;
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes); await writer.StoreAsync(); writer.DetachStream();
            }
            stream.Seek(0);
            var bitmap = new BitmapImage { DecodePixelWidth = 68, DecodePixelHeight = 68 };
            await bitmap.SetSourceAsync(stream);
            if (current != generation) return;
            picture.Source = bitmap; picture.Visibility = Visibility.Visible; defaultPicture.Visibility = Visibility.Collapsed;
        }
        catch (Exception) { /* Missing or unreadable icons retain the agent's initial. */ }
    }
}
