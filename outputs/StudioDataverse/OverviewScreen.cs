using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace StudioDataverse;
public sealed partial class MainWindow
{
    private readonly StackPanel overviewPanel=new(){Spacing=24,Visibility=Visibility.Collapsed};
    private readonly Grid overviewMetrics=new(){ColumnSpacing=14,RowSpacing=14};
    private readonly StackPanel overviewCreator=new(){Spacing=10};
    private readonly StackPanel overviewPlatform=new(){Spacing=10};
    private readonly StackPanel overviewUsers=new(){Spacing=10};
    private readonly StackPanel overviewTrending=new(){Spacing=10};
    private Button overviewNav=null!;
    private bool viewingOverview;
    private bool overviewBuilt;
    private void BuildOverview()
    {
        overviewPanel.Children.Add(Label("Suivez l’utilisation des agents au sein de votre organisation et prenez des mesures pour améliorer leur impact.",13));
        var hero=new Grid{MinHeight=232,Background=Brush("#EAF3FF")};hero.ColumnDefinitions.Add(new(){Width=new GridLength(.45,GridUnitType.Star)});hero.ColumnDefinitions.Add(new(){Width=new GridLength(.55,GridUnitType.Star)});
        var heroCopy=new StackPanel{Spacing=16,Margin=new Thickness(42,38,24,24)};
        var heroTitle=Label("Découvrez comment tout fonctionne ensemble",20);heroTitle.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;heroCopy.Children.Add(heroTitle);
        heroCopy.Children.Add(Label("Obtenez une vue centralisée et interactive du fonctionnement des agents dans votre environnement. Explorez leur propriété, leur état et leur utilisation à partir des données Dataverse.",13));
        heroCopy.Children.Add(Label("Environnement Formation · Copilot Studio",11,"#536277"));
        var explore=new Button{Content="Explorer votre carte d’agent",Background=Brush("#0F6CBD"),Foreground=Brush("#FFFFFF"),Padding=new Thickness(14,7,14,7)};explore.Click+=(_,_)=>ShowAgentMap();heroCopy.Children.Add(explore);hero.Children.Add(heroCopy);
        Place(hero,Artwork("overview-hero",double.NaN,232),0,1);
        overviewPanel.Children.Add(new Border{Child=hero,BorderBrush=Brush("#D9DFE8"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3)});
        for(var i=0;i<4;i++)overviewMetrics.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        overviewMetrics.RowDefinitions.Add(new(){Height=GridLength.Auto});overviewMetrics.RowDefinitions.Add(new(){Height=GridLength.Auto});overviewPanel.Children.Add(overviewMetrics);
        overviewPanel.Children.Add(Label("Les meilleures actions pour vous",19));
        var actionGrid=new Grid{ColumnSpacing=14};for(var i=0;i<4;i++)actionGrid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        string[] titles=["Demandes d’assistants en attente","Assistants à risque","Assistants sans propriétaire","Agents avec des exceptions"];
        for(var i=0;i<4;i++)
        {
            var action=new StackPanel{Spacing=18};action.Children.Add(Label(titles[i],12,"#536277"));action.Children.Add(Label("—",28));action.Children.Add(Label("Indicateur Agent 365 non fourni par cette connexion.",11,"#787E8C"));action.Children.Add(new Button{Content="Afficher les détails",IsEnabled=false});Place(actionGrid,ReferenceCard(action),0,i);
        }
        overviewPanel.Children.Add(actionGrid);
        var analysisHead=new Grid();analysisHead.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});analysisHead.ColumnDefinitions.Add(new(){Width=GridLength.Auto});analysisHead.Children.Add(Label("Analyse de l’agent",19));
        var refresh=ActionButton("Actualiser les analyses",async()=>{await LoadAgents();dashboardData=await client.OrganizationAnalyticsAsync();RenderDashboard();RenderOverview();notice.IsOpen=false;});Place(analysisHead,refresh,0,1);overviewPanel.Children.Add(analysisHead);
        var charts=new Grid{ColumnSpacing=14,RowSpacing=14};for(var i=0;i<4;i++)charts.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});charts.RowDefinitions.Add(new(){Height=GridLength.Auto});charts.RowDefinitions.Add(new(){Height=GridLength.Auto});
        StackPanel[] previews=[overviewCreator,overviewPlatform,overviewUsers,overviewTrending];for(var i=0;i<4;i++)Place(charts,ReferenceCard(previews[i]),0,i);
        charts.SizeChanged+=(_,_)=>{var count=charts.ActualWidth>=1350?4:2;for(var i=0;i<4;i++){var child=(FrameworkElement)charts.Children[i];Grid.SetRow(child,i/count);Grid.SetColumn(child,count==4?i:(i%2)*2);Grid.SetColumnSpan(child,count==4?1:2);}};
        overviewPanel.Children.Add(charts);content.Children.Add(overviewPanel);overviewBuilt=true;RenderOverview();
    }
    private static Border ReferenceCard(UIElement content)=>new(){Child=content,Padding=new Thickness(16),Background=Brush("#FFFFFF"),CornerRadius=new CornerRadius(7),BorderBrush=Brush("#D2D5DB"),BorderThickness=new Thickness(1),MinHeight=134};
    private void RenderOverview()
    {
        if(!overviewBuilt)return;overviewMetrics.Children.Clear();
        var data=new[]{("Registre des agents",client.TotalAgents?.ToString()??allAgents.Count.ToString(),"Agents accessibles dans cet environnement","Explorer tous les agents →"),("Utilisateurs actifs identifiés",dashboardData?.IdentifiedUsers.ToString()??"—","Échantillon de transcriptions sur 30 jours","Afficher les analyses →"),("Exécution de l’agent","—","Temps d’exécution interne non fourni","Non disponible"),("Plateformes observées",allAgents.Count>0?"1":"0","Copilot Studio dans cet environnement","Explorer la carte →"),("Outils","—","Inventaire global des outils non chargé","Explorer les agents →")};
        for(var i=0;i<data.Length;i++)
        {
            var metric=data[i];var box=new StackPanel{Spacing=10};box.Children.Add(Label(metric.Item1,12,"#536277"));var number=Label(metric.Item2,28);number.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;box.Children.Add(number);box.Children.Add(Label(metric.Item3,11,"#787E8C"));
            var link=new Button{Content=metric.Item4,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent),Foreground=Brush("#0078D4"),IsEnabled=i!=2};
            var index=i;link.Click+=(_,_)=>{if(index==1)ShowDashboard();else if(index==3)ShowAgentMap();else ShowAgents();};box.Children.Add(link);Place(overviewMetrics,ReferenceCard(box),i/4,i%4);
        }
    }
    private void HideOverview(){viewingOverview=false;overviewPanel.Visibility=Visibility.Collapsed;if(overviewNav!=null)overviewNav.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);}
    private void ShowOverview()
    {
        HideDashboard();HideAgentMap();viewingTranscripts=false;viewingOverview=true;SetPageChrome("Vue d’ensemble des agents");homePanel.Visibility=agentPanel.Visibility=transcriptPanel.Visibility=Visibility.Collapsed;overviewPanel.Visibility=Visibility.Visible;
        welcome.Text="Vue d’ensemble des agents";welcome.Visibility=Visibility.Visible;content.MaxWidth=double.PositiveInfinity;content.HorizontalAlignment=HorizontalAlignment.Stretch;content.Margin=new Thickness(8,8,8,24);
        homeNav.Background=agentsNav.Background=transcriptsNav.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);overviewNav.Background=Brush("#E7E7E7");notice.IsOpen=false;RenderOverview();RenderDashboard();
    }
}
