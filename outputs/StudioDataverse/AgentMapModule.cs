using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private readonly StackPanel mapPanel = new() { Spacing = 14, Visibility = Visibility.Collapsed };
    private readonly Grid registrySummary = new() { ColumnSpacing = 14 };
    private readonly WebView2 mapWeb = new() { Height = 760, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock mapLoadStatus = Label("Carte React Flow · chargement à l’ouverture",12,"#787E8C");
    private Button mapNav = null!;
    private bool viewingMap;
    private bool mapWebReady;
    private bool mapWebLoading;
    private const string MapOrigin = "https://agentmap.local";

    private void BuildAgentMap()
    {
        mapPanel.Children.Add(Label("Visualisez la répartition des agents. Explorez les groupes, déplacez les icônes et filtrez votre environnement.",14,"#536277"));
        mapPanel.Children.Add(mapLoadStatus);
        mapPanel.Children.Add(new Border { Child=mapWeb,CornerRadius=new CornerRadius(10),BorderBrush=Brush("#DFE2EA"),BorderThickness=new Thickness(1) });
        var retry = new Button { Content="Recharger la carte",HorizontalAlignment=HorizontalAlignment.Left };
        retry.Click += async (_,_) => { if(mapWeb.CoreWebView2!=null)mapWeb.CoreWebView2.Reload();else await EnsureMapWebAsync(); };
        mapPanel.Children.Add(retry);
        content.Children.Add(mapPanel);
        for(var i=0;i<3;i++)registrySummary.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        UpdateRegistrySummary();
    }
    private async Task EnsureMapWebAsync()
    {
        if(mapWebReady||mapWebLoading)return;
        mapWebLoading=true;
        try
        {
            _=CoreWebView2Environment.GetAvailableBrowserVersionString();
            var cache=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StudioDataverse","MapWebView2");
            var environment=await CoreWebView2Environment.CreateWithOptionsAsync(null,cache,new CoreWebView2EnvironmentOptions());
            await mapWeb.EnsureCoreWebView2Async(environment);
            var core=mapWeb.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled=false; core.Settings.IsStatusBarEnabled=false;
            core.Settings.AreDevToolsEnabled=false; core.Settings.AreDefaultScriptDialogsEnabled=false;
            core.SetVirtualHostNameToFolderMapping("agentmap.local",System.IO.Path.Combine(AppContext.BaseDirectory,"Assets","AgentMap"),CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting+=(_,args)=>{if(!IsMapOrigin(args.Uri)){args.Cancel=true;return;}mapWebReady=false;};
            core.NewWindowRequested+=(_,args)=>args.Handled=true;
            core.WebMessageReceived+=ReceiveMapMessage;
            core.NavigationCompleted+=(_,args)=>{if(!args.IsSuccess)mapLoadStatus.Text="La carte locale n’a pas pu être chargée : "+args.WebErrorStatus;};
            core.Navigate(MapOrigin+"/index.html");
        }
        catch(Exception ex){mapLoadStatus.Text="La carte nécessite Microsoft Edge WebView2 Runtime. Détail : "+ex.Message;}
        finally{mapWebLoading=false;}
    }
    private static bool IsMapOrigin(string source)=>Uri.TryCreate(source,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="agentmap.local"&&uri.IsDefaultPort;
    private async void ReceiveMapMessage(object? sender,CoreWebView2WebMessageReceivedEventArgs args)
    {
        if(!IsMapOrigin(args.Source))return;
        try
        {
            using var doc=JsonDocument.Parse(args.WebMessageAsJson);
            if(!doc.RootElement.TryGetProperty("action",out var action)||action.ValueKind!=JsonValueKind.String)return;
            if(action.GetString()=="ready"){mapWebReady=true;mapLoadStatus.Text="React Flow · données locales Dataverse · zoom, déplacement et sélection natifs";RenderAgentMap();return;}
            if(!authenticated||busy||!doc.RootElement.TryGetProperty("id",out var id)||!Guid.TryParse(id.GetString(),out var guid))return;
            var agent=allAgents.FirstOrDefault(a=>a.Id==guid);if(agent==null)return;
            switch(action.GetString())
            {
                case "open":ShowAgents();SelectAgent(agent);agentWorkspace.IsPaneOpen=true;break;
                case "transcripts":await Run(async()=>{ShowTranscripts(agent.Id);await LoadTranscriptsAsync(false);});break;
                case "components":ShowAgents();SelectAgent(agent);await Run(async()=>await LoadComponentsAsync(agent));break;
                case "favorite":preferences.Toggle(agent.Id);await SavePreferencesAsync();RenderAgentMap();break;
                case "copy":CopyText(agent.Id.ToString());break;
                case "reference":comparisonAnchor=agent;comparisonLabel.Text="Référence : "+agent.Name;break;
            }
        }
        catch(Exception ex){mapLoadStatus.Text=DescribeError(ex);}
    }
    private void RenderAgentMap()
    {
        if(!mapWebReady||mapWeb.CoreWebView2==null)return;
        var agentsPayload=allAgents.Select(a=>new{ id=a.Id.ToString(),name=a.Name,schema=a.Schema,state=a.State,status=a.StatusLabel,language=LanguageName(a.Language),managed=a.Managed,published=a.PublishedOn.HasValue,
            icon=AgentIconPayload.Decode(a.IconBase64) is { } bytes?Convert.ToBase64String(bytes):null,favorite=preferences.Favorites.Contains(a.Id)});
        mapWeb.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="agents",agents=agentsPayload,total=client.TotalAgents,connected=authenticated}));
    }
    private void UpdateRegistrySummary()
    {
        registrySummary.Children.Clear();
        var counts=new[]{("Registre des agents",client.TotalAgents?.ToString()??allAgents.Count.ToString(),client.TotalAgents.HasValue?$"{allAgents.Count} chargés depuis Dataverse":"Agents actuellement chargés"),("Agents actifs",allAgents.Count(x=>x.State==0).ToString(),"Dans les agents chargés"),("Déjà publiés",allAgents.Count(x=>x.PublishedOn.HasValue).ToString(),"Date de publication renseignée")};
        for(var i=0;i<counts.Length;i++){var text=new StackPanel{Spacing=8};text.Children.Add(Label(counts[i].Item1,12,"#536277"));var number=Label(counts[i].Item2,26);number.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;text.Children.Add(number);text.Children.Add(Label(counts[i].Item3,11,"#858895"));Place(registrySummary,Card(text),0,i);}
        RenderAgentMap();RenderOverview();if(viewingDashboard||viewingOverview)RenderDashboard();
    }
    private void HideAgentMap(){viewingMap=false;mapPanel.Visibility=Visibility.Collapsed;if(mapNav!=null)mapNav.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);}
    private void ShowAgentMap()
    {
        HideOverview();SetPageChrome("Carte d’agent");
        HideDashboard();viewingMap=true;viewingTranscripts=false;
        homePanel.Visibility=agentPanel.Visibility=transcriptPanel.Visibility=Visibility.Collapsed;mapPanel.Visibility=Visibility.Visible;
        welcome.Text="Carte d’agent";welcome.Visibility=Visibility.Visible;content.MaxWidth=double.PositiveInfinity;content.HorizontalAlignment=HorizontalAlignment.Stretch;content.Margin=new Thickness(16);
        homeNav.Background=agentsNav.Background=transcriptsNav.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);mapNav.Background=Brush("#E7E7E7");notice.IsOpen=false;
        _=EnsureMapWebAsync();RenderAgentMap();
    }
}
