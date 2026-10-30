using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
namespace StudioDataverse;
public sealed partial class MainWindow
{
    private readonly StackPanel dashboardPanel=new(){Spacing=20,Visibility=Visibility.Collapsed};
    private readonly Grid dashboardGrid=new(){ColumnSpacing=14,RowSpacing=14};
    private readonly TextBlock dashboardStatus=Label("Données de conversation non chargées.",12,"#787E8C");
    private readonly StackPanel creatorChart=new(){Spacing=10};
    private readonly StackPanel platformChart=new(){Spacing=10};
    private readonly StackPanel usersChart=new(){Spacing=10};
    private readonly StackPanel trendingChart=new(){Spacing=10};
    private OrganizationAnalytics? dashboardData;
    private Button dashboardNav=null!;
    private bool viewingDashboard;
    private int dashboardGeneration;
    private void BuildAnalyticsDashboard()
    {
        dashboardPanel.Children.Add(Label("Analyse de l’agent",22));
        dashboardPanel.Children.Add(Label("Répartition du registre et activité observée dans les transcriptions Dataverse.",14,"#687080"));
        dashboardPanel.Children.Add(ActionButton("Actualiser les analyses",async()=>
        {
            var generation=++dashboardGeneration;
            dashboardStatus.Text="Chargement des données réelles…";
            try
            {
                await LoadAgents(); var result=await client.OrganizationAnalyticsAsync();
                if(generation!=dashboardGeneration)return;
                dashboardData=result;RenderDashboard();notice.IsOpen=false;
            }
            catch(Exception ex){dashboardStatus.Text=DescribeError(ex);throw;}
        }));
        dashboardPanel.Children.Add(dashboardStatus);
        for(var i=0;i<4;i++)dashboardGrid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        dashboardGrid.RowDefinitions.Add(new(){Height=GridLength.Auto});dashboardGrid.RowDefinitions.Add(new(){Height=GridLength.Auto});
        StackPanel[] charts=[creatorChart,platformChart,usersChart,trendingChart];
        foreach(var chart in charts)dashboardGrid.Children.Add(new Border{Child=chart,Padding=new Thickness(16),MinHeight=270,Background=Brush("#FFFFFF"),BorderBrush=Brush("#DADDE5"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)});
        void Layout()
        {
            var columns=dashboardGrid.ActualWidth>=1400?4:2;
            for(var i=0;i<4;i++){Grid.SetColumn((FrameworkElement)dashboardGrid.Children[i],i%columns);Grid.SetRow((FrameworkElement)dashboardGrid.Children[i],i/columns);Grid.SetColumnSpan((FrameworkElement)dashboardGrid.Children[i],columns==2?2:1);if(columns==2)Grid.SetColumn((FrameworkElement)dashboardGrid.Children[i],(i%2)*2);}
        }
        dashboardGrid.SizeChanged+=(_,_)=>Layout();Layout();
        dashboardPanel.Children.Add(dashboardGrid);
        dashboardPanel.Children.Add(Label("Périmètre : créateurs et plateformes sur les agents chargés ; activité sur les 100 derniers enregistrements de transcription des 30 derniers jours. Ce n’est pas l’inventaire Agent 365. Les créateurs sont les noms retournés par Dataverse, sans classification organisation/partenaire/Microsoft.",11,"#858895"));
        content.Children.Add(dashboardPanel);RenderDashboard();
    }
    private void HideDashboard(){viewingDashboard=false;dashboardPanel.Visibility=Visibility.Collapsed;if(dashboardNav!=null)dashboardNav.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);}
    private void ShowDashboard()
    {
        HideOverview();SetPageChrome("Analyse de l’agent");
        HideAgentMap();viewingTranscripts=false;viewingDashboard=true;
        homePanel.Visibility=agentPanel.Visibility=transcriptPanel.Visibility=Visibility.Collapsed;dashboardPanel.Visibility=Visibility.Visible;
        welcome.Visibility=Visibility.Collapsed;content.MaxWidth=double.PositiveInfinity;content.HorizontalAlignment=HorizontalAlignment.Stretch;content.Margin=new Thickness(8,10,8,24);
        homeNav.Background=agentsNav.Background=transcriptsNav.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);dashboardNav.Background=Brush("#E7E7E7");notice.IsOpen=false;RenderDashboard();
    }
    private void ClearDashboard(){dashboardGeneration++;dashboardData=null;RenderDashboard();RenderOverview();}
    private void RenderDashboard()
    {
        RenderChartSet(creatorChart,platformChart,usersChart,trendingChart);
        RenderChartSet(overviewCreator,overviewPlatform,overviewUsers,overviewTrending);
    }
    private void RenderChartSet(StackPanel creatorChart,StackPanel platformChart,StackPanel usersChart,StackPanel trendingChart)
    {
        foreach(var panel in new[]{creatorChart,platformChart,usersChart,trendingChart})panel.Children.Clear();
        creatorChart.Children.Add(Label("Assistants par créateurs",14));
        var creators=allAgents.GroupBy(x=>string.IsNullOrWhiteSpace(x.Creator)?"Créateur non renseigné":x.Creator!).OrderByDescending(x=>x.Count()).Take(5).Select(x=>(x.Key,x.Count())).ToList();
        CreatorBars(creatorChart,creators);
        creatorChart.Children.Add(Label("5 premiers créateurs identifiés · agents chargés",11,"#858895"));
        platformChart.Children.Add(Label("Plateformes utilisées pour créer des assistants",14));
        Bars(platformChart,allAgents.Count>0?new List<(string,int)>{("Copilot Studio",allAgents.Count)}:[],["#00B5A6"]);
        platformChart.Children.Add(Label("Cette connexion ne fournit que les agents Copilot Studio de cet environnement.",11,"#858895"));
        usersChart.Children.Add(Label("Utilisateurs actifs au fil du temps",14));
        trendingChart.Children.Add(Label("Agents tendance selon les utilisateurs actifs",14));
        if(dashboardData==null)
        {
            usersChart.Children.Add(Label("Actualisez les analyses pour récupérer l’activité.",13,"#787E8C"));trendingChart.Children.Add(Label("Données non chargées.",13,"#787E8C"));
            dashboardStatus.Text="Registre : "+allAgents.Count+" agents chargés · activité non chargée.";return;
        }
        dashboardStatus.Text=$"Mis à jour : {DateTime.Now:dd/MM/yyyy HH:mm} · {dashboardData.Records} enregistrement(s) analysé(s), limite 100 · {dashboardData.Unreadable} conversation(s) illisible(s)";
        DrawUsersLine(usersChart,dashboardData.DailyUsers);
        Bars(trendingChart,dashboardData.Trends.Select(x=>(x.Name,x.Users)).ToList(),["#E5008D","#0078D4","#168A20","#6640AA","#666666"]);
        usersChart.Children.Add(Label("Utilisateurs distincts (from.id) par jour observé · fuseau Europe/Paris. Les jours sans observation ne sont pas assimilés à zéro.",11,"#858895"));
        trendingChart.Children.Add(Label("Utilisateurs identifiés distincts par agent dans l’échantillon.",11,"#858895"));
    }
    private void Bars(StackPanel panel,List<(string Name,int Count)> values,string[] colors)
    {
        if(values.Count==0){panel.Children.Add(Label("Aucune donnée disponible.",13,"#787E8C"));return;}
        var max=Math.Max(1,values.Max(x=>x.Count));
        for(var i=0;i<values.Count;i++)
        {
            var value=values[i];var label=Label(value.Name,11,"#536277");label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;ToolTipService.SetToolTip(label,value.Name);panel.Children.Add(label);
            var grid=new Grid{ColumnSpacing=10};grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(32)});
            var track=new Grid{Height=10,Background=Brush("#F3F3F6"),VerticalAlignment=VerticalAlignment.Center};
            track.ColumnDefinitions.Add(new(){Width=new GridLength(value.Count,GridUnitType.Star)});track.ColumnDefinitions.Add(new(){Width=new GridLength(max-value.Count,GridUnitType.Star)});
            track.Children.Add(new Border{Background=Brush(colors[i%colors.Length]),CornerRadius=new CornerRadius(2)});grid.Children.Add(track);
            Place(grid,Label(value.Count.ToString(),11),0,1);panel.Children.Add(grid);
        }
    }
    private void CreatorBars(StackPanel creatorChart,List<(string Name,int Count)> values)
    {
        if(values.Count==0){creatorChart.Children.Add(Label("Aucun agent chargé.",13,"#787E8C"));return;}
        var canvas=new Canvas{Width=320,Height=190};var max=Math.Max(1,values.Max(x=>x.Count));
        string[] colors=["#6C64B1","#E5008D","#00B9C5","#9966EE","#006FD3"];
        for(var i=0;i<4;i++){var y=15+i*43;canvas.Children.Add(new Line{X1=30,Y1=y,X2=310,Y2=y,Stroke=Brush("#E0E2E7"),StrokeThickness=1});var label=Label(Math.Round(max*(1-i/3d)).ToString(),9,"#787E8C");Canvas.SetTop(label,y-6);canvas.Children.Add(label);}
        var step=270d/values.Count;
        for(var i=0;i<values.Count;i++)
        {
            var value=values[i];var height=129d*value.Count/max;var x=35+i*step+step/2;
            var bar=new Rectangle{Width=Math.Min(30,step*.55),Height=height,Fill=Brush(colors[i%colors.Length])};Canvas.SetLeft(bar,x-bar.Width/2);Canvas.SetTop(bar,144-height);ToolTipService.SetToolTip(bar,value.Name+" : "+value.Count);canvas.Children.Add(bar);
            var number=Label(value.Count.ToString(),10);Canvas.SetLeft(number,x-8);Canvas.SetTop(number,128-height);canvas.Children.Add(number);
            var name=Label(value.Name,9,"#536277");name.Width=step-3;name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.TextAlignment=TextAlignment.Center;ToolTipService.SetToolTip(name,value.Name);Canvas.SetLeft(name,x-step/2);Canvas.SetTop(name,152);canvas.Children.Add(name);
        }
        creatorChart.Children.Add(new Viewbox{Child=canvas,Stretch=Stretch.Uniform,MaxHeight=190});
    }
    private void DrawUsersLine(StackPanel usersChart,SortedDictionary<DateOnly,int> points)
    {
        if(points.Count==0){usersChart.Children.Add(Label("Aucun utilisateur horodaté et identifiable dans l’échantillon.",13,"#787E8C"));return;}
        var canvas=new Canvas{Width=320,Height=180};var max=Math.Max(1,points.Values.Max());
        for(var i=0;i<4;i++){var y=15+i*43;canvas.Children.Add(new Line{X1=30,Y1=y,X2=310,Y2=y,Stroke=Brush("#E0E2E7"),StrokeThickness=1});var label=Label(Math.Round(max*(1-i/3d)).ToString(),9,"#787E8C");Canvas.SetLeft(label,0);Canvas.SetTop(label,y-6);canvas.Children.Add(label);}
        var first=points.Keys.Min();var last=points.Keys.Max();var days=Math.Max(1,last.DayNumber-first.DayNumber);
        foreach(var point in points)
        {
            var x=30+280d*(point.Key.DayNumber-first.DayNumber)/days;var y=144-129d*point.Value/max;
            var dot=new Ellipse{Width=6,Height=6,Fill=Brush("#0873CF")};Canvas.SetLeft(dot,x-3);Canvas.SetTop(dot,y-3);ToolTipService.SetToolTip(dot,$"{point.Key:dd/MM} : {point.Value}");canvas.Children.Add(dot);
            var previous=points.LastOrDefault(p=>p.Key<point.Key);
            if(previous.Key!=default&&point.Key.DayNumber-previous.Key.DayNumber==1)canvas.Children.Add(new Line{X1=30+280*(previous.Key.DayNumber-first.DayNumber)/days,Y1=144-129*previous.Value/max,X2=x,Y2=y,Stroke=Brush("#0873CF"),StrokeThickness=2});
        }
        var start=Label(first.ToString("dd MMM"),9,"#787E8C");Canvas.SetLeft(start,30);Canvas.SetTop(start,156);canvas.Children.Add(start);
        var end=Label(last.ToString("dd MMM"),9,"#787E8C");Canvas.SetLeft(end,265);Canvas.SetTop(end,156);canvas.Children.Add(end);
        usersChart.Children.Add(new Viewbox{Child=canvas,Stretch=Stretch.Uniform,MaxHeight=180});
    }
}
