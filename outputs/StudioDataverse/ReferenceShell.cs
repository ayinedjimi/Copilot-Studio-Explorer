using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
namespace StudioDataverse;
public sealed partial class MainWindow
{
    private Grid pageSurface=null!;
    private FrameworkElement homeBanner=null!;
    private readonly TextBlock breadcrumbs=Label("Accueil",11,"#536277");
    private readonly Border pageAccent=new(){Height=130,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    private void InstallReferenceChrome(Grid main,FrameworkElement banner)
    {
        pageSurface=main;homeBanner=banner;
        pageAccent.Background=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,0),GradientStops=new GradientStopCollection{new(){Color=Windows.UI.Color.FromArgb(35,228,119,192),Offset=0},new(){Color=Windows.UI.Color.FromArgb(45,98,142,248),Offset=1}}};
        Grid.SetRowSpan(pageAccent,2);main.Children.Insert(0,pageAccent);
        breadcrumbs.Margin=new Thickness(8,6,8,0);breadcrumbs.Visibility=Visibility.Collapsed;main.Children.Add(breadcrumbs);
        welcome.FontSize=26;welcome.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;
    }
    private void SetPageChrome(string title,bool home=false)
    {
        if(!openingOperations)HideOperations();
        homeBanner.Visibility=home?Visibility.Visible:Visibility.Collapsed;
        breadcrumbs.Visibility=home?Visibility.Collapsed:Visibility.Visible;
        breadcrumbs.Text="Accueil  ›  "+title;
        pageAccent.Visibility=home?Visibility.Collapsed:Visibility.Visible;
        pageSurface.RowDefinitions[0].Height=new GridLength(home?48:25);
    }
}
