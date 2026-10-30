using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;

namespace StudioDataverse;

public sealed record ExplorerTreeItem(string Display, string Kind, Guid AgentId, string Group, int Tab);

public sealed partial class MainWindow
{
    private readonly Grid explorerWorkspace=new(){ColumnSpacing=0,Height=720};
    private readonly ListView explorerTree=new(){SelectionMode=ListViewSelectionMode.Single,HorizontalContentAlignment=HorizontalAlignment.Stretch};
    private readonly TextBox explorerSearch=new(){PlaceholderText="Rechercher un agent"};
    private readonly ComboBox explorerFilter=Options("Filtre rapide","Tous","Actifs","Publiés","À risque");
    private readonly StackPanel explorerDetail=new(){Spacing=12,Margin=new Thickness(20,0,8,8)};
    private readonly HashSet<string> explorerGroups=[];
    private Guid? explorerAgentId,explorerExpandedAgent;
    private int explorerTab,explorerGeneration,explorerConversationGeneration;
    private string explorerSelectedKind="agent";
    private bool explorerTreeRefreshing,explorerBuilt;
    private readonly Dictionary<Guid,List<TranscriptRecord>> explorerTranscripts=[];

    private void RenderExplorer()
    {
        if(!explorerBuilt)BuildExplorer();
        operationsBody.Children.Add(explorerWorkspace);RefreshExplorerTree();RenderExplorerDetail();
    }
    private void BuildExplorer()
    {
        explorerBuilt=true;explorerWorkspace.ColumnDefinitions.Add(new(){Width=new GridLength(280),MinWidth=220,MaxWidth=450});explorerWorkspace.ColumnDefinitions.Add(new(){Width=new GridLength(8)});explorerWorkspace.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var left=new Grid{RowSpacing=10,Padding=new Thickness(12)};left.RowDefinitions.Add(new(){Height=GridLength.Auto});left.RowDefinitions.Add(new(){Height=GridLength.Auto});left.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        explorerSearch.TextChanged+=(_,_)=>RefreshExplorerTree();explorerFilter.SelectionChanged+=(_,_)=>RefreshExplorerTree();left.Children.Add(explorerSearch);Place(left,explorerFilter,1,0);
        explorerTree.ItemTemplate=(DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><TextBlock Text="{Binding Display}" TextTrimming="CharacterEllipsis" Padding="4,8" /></DataTemplate>
            """);
        explorerTree.SelectionChanged+=async (_,_)=>{
            if(explorerTreeRefreshing||explorerTree.SelectedItem is not ExplorerTreeItem item)return;
            if(item.Kind=="group"){if(!explorerGroups.Add(item.Group))explorerGroups.Remove(item.Group);RefreshExplorerTree();return;}
            if(item.Kind=="agent"){explorerExpandedAgent=explorerExpandedAgent==item.AgentId?null:item.AgentId;explorerTab=0;explorerSelectedKind="agent";}
            else {explorerTab=item.Tab;explorerSelectedKind="tab";}
            if(operationsAgentId!=item.AgentId){generatedQuestions="";localAnalysis=localSecurity="";scenarioSelectedId=null;scenarioNew=false;scenarioEndpoint="";}explorerAgentId=item.AgentId;operationsAgentId=item.AgentId;RefreshExplorerTree();RenderExplorerDetail();
            if(!inspections.ContainsKey(item.AgentId))await LoadExplorerAgentAsync(item.AgentId);
        };
        Place(left,explorerTree,2,0);explorerWorkspace.Children.Add(new Border{Child=left,Background=Brush("#FAFAFC"),BorderBrush=Brush("#E3E3EA"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)});
        var splitter=new Thumb{Width=8,HorizontalAlignment=HorizontalAlignment.Stretch,Background=Brush("#F0EFF5")};splitter.DragDelta+=(_,e)=>{var column=explorerWorkspace.ColumnDefinitions[0];column.Width=new GridLength(Math.Clamp(column.ActualWidth+e.HorizontalChange,220,450));};Place(explorerWorkspace,splitter,0,1);
        Place(explorerWorkspace,new ScrollViewer{Content=explorerDetail,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},0,2);
    }
    private void RefreshExplorerTree()
    {
        if(!explorerBuilt)return;
        var filter=explorerFilter.SelectedIndex;var query=explorerSearch.Text.Trim();
        var agents=allAgents.Where(a=>a.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||a.Schema.Contains(query,StringComparison.OrdinalIgnoreCase));
        agents=filter switch {1=>agents.Where(a=>a.State==0),2=>agents.Where(a=>a.PublishedOn.HasValue),3=>agents.Where(a=>inspections.TryGetValue(a.Id,out var inspection)&&inspection.Findings.Any(f=>f.State==FindingState.Risque)),_=>agents};
        var rows=new List<ExplorerTreeItem>();
        foreach(var group in agents.OrderBy(a=>a.Name).GroupBy(a=>a.PublishedOn.HasValue?"Publiés":"Non publiés").OrderBy(g=>g.Key)){
            var opened=explorerGroups.Contains(group.Key);rows.Add(new((opened?"▾ ":"▸ ")+group.Key+"  "+group.Count(),"group",Guid.Empty,group.Key,0));
            if(!opened)continue;
            foreach(var agent in group){rows.Add(new("   "+(explorerExpandedAgent==agent.Id?"▾ ":"▸ ")+agent.Name,"agent",agent.Id,group.Key,0));
                if(explorerExpandedAgent==agent.Id){string[] tabs=["Composants","Dépendances","Transcriptions","Versions","Incidents","Sécurité"];for(int i=0;i<tabs.Length;i++)rows.Add(new("        "+tabs[i],"tab",agent.Id,group.Key,i));}}
        }
        explorerTreeRefreshing=true;explorerTree.ItemsSource=rows;explorerTree.SelectedItem=rows.FirstOrDefault(x=>x.AgentId==explorerAgentId&&x.Kind==explorerSelectedKind&&(x.Kind!="tab"||x.Tab==explorerTab));explorerTreeRefreshing=false;
    }
    private async Task LoadExplorerAgentAsync(Guid id)
    {
        var agent=allAgents.FirstOrDefault(a=>a.Id==id);if(agent==null||!authenticated)return;
        var generation=++explorerGeneration;var session=client.SessionGeneration;operationsStatus.Text="Analyse de l’agent en cours…";
        try{var data=await client.InspectAgentAsync(agent,null,CancellationToken.None);if(session!=client.SessionGeneration)return;inspections[id]=data;if(generation==explorerGeneration&&explorerAgentId==id){operationsStatus.Text="Agent analysé · données accessibles";RefreshExplorerTree();RenderExplorerDetail();}}
        catch(Exception ex){operationsStatus.Text="Analyse impossible : "+ExplorerRedaction.Safe(DescribeError(ex));await App.ShowErrorAsync(this,App.RecordError(ex));}
    }
    private void RenderExplorerDetail()
    {
        explorerDetail.Children.Clear();var agent=allAgents.FirstOrDefault(a=>a.Id==explorerAgentId);
        if(agent==null){explorerDetail.Children.Add(Label("Sélectionnez un agent",24));explorerDetail.Children.Add(Label("Ouvrez un groupe pour parcourir les agents. Le filtre À risque concerne uniquement les agents déjà analysés.",13,"#687080"));return;}
        explorerDetail.Children.Add(Label((agent.PublishedOn.HasValue?"Publiés":"Non publiés")+"  ›  "+agent.Name,12,"#687080"));
        var title=Label(agent.Name,26);title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;explorerDetail.Children.Add(title);
        explorerDetail.Children.Add(Label(agent.StatusLabel+" · "+(agent.PublishedOn.HasValue?"Publié":"Non publié")+" · Modifié : "+agent.ModifiedLabel,13,"#687080"));
        if(!inspections.TryGetValue(agent.Id,out var data)){explorerDetail.Children.Add(Label("Analyse en cours ou non disponible. Sélectionnez à nouveau l’agent pour réessayer.",14,"#687080"));return;}
        var pivot=new Pivot();string[] names=["Composants","Dépendances","Transcriptions","Versions","Incidents","Sécurité"];
        for(int i=0;i<names.Length;i++){var body=new StackPanel{Spacing=10,Margin=new Thickness(0,12,0,12)};FillExplorerTab(body,i,agent,data);pivot.Items.Add(new PivotItem{Header=names[i],Content=body});}
        pivot.SelectedIndex=Math.Clamp(explorerTab,0,5);pivot.SelectionChanged+=(_,_)=>explorerTab=pivot.SelectedIndex;explorerDetail.Children.Add(pivot);
    }
    private void FillExplorerTab(StackPanel body,int tab,Agent agent,AgentInspection data)
    {
        if(tab==0){body.Children.Add(Label(data.Components.Count+" composants accessibles",14));foreach(var c in data.Components.Take(200))body.Children.Add(Card(Label(ExplorerInspection.Text(c,"name")+" · type "+ExplorerInspection.Text(c,"componenttype"),13)));if(data.Missing.Contains("components"))body.Children.Add(Label("Composants non vérifiables : accès incomplet.",13,"#B76A06"));}
        else if(tab==1){body.Children.Add(Label(data.Nodes.Count+" objets · "+data.Edges.Count+" liens déclarés",14));foreach(var node in data.Nodes.Take(100))body.Children.Add(Label(node.Kind+" · "+node.Label+" · "+node.State,13));body.Children.Add(OpButton("Ouvrir la carte des dépendances",_=>{operationsAgentId=agent.Id;ShowOperations("Dépendances");return Task.CompletedTask;}));}
        else if(tab==2){
            body.Children.Add(OpButton("Charger les transcriptions",async token=>{var page=await client.ListTranscriptsAsync(DataverseClient.TranscriptListPath(agent.Id,DateTimeOffset.UtcNow.AddDays(-appSettings.TranscriptDays),null));token.ThrowIfCancellationRequested();explorerTranscripts[agent.Id]=page.Items;operationsStatus.Text=page.Items.Count+" transcription(s) chargée(s) sur "+appSettings.TranscriptDays+" jours.";}));
            if(explorerTranscripts.TryGetValue(agent.Id,out var records)){
                body.Children.Add(Label(appSettings.TranscriptDays+" derniers jours · 50 premiers enregistrements affichés",12,"#687080"));
                var layout=new Grid{ColumnSpacing=12,MinHeight=300};layout.ColumnDefinitions.Add(new(){Width=new GridLength(240)});layout.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
                var list=new StackPanel{Spacing=5};var reader=new StackPanel{Spacing=10};reader.Children.Add(Label("Sélectionnez une conversation",15));
                foreach(var record in records.Take(50)){var button=new Button{Content=record.StartLabel,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left};button.Click+=async (_,_)=>{
                    reader.Children.Clear();reader.Children.Add(Label("Chargement…",13));var session=client.SessionGeneration;var generation=++explorerConversationGeneration;
                    try {var batch=await client.TranscriptAsync(record.Id);if(session!=client.SessionGeneration||explorerAgentId!=agent.Id||generation!=explorerConversationGeneration)return;var activities=TranscriptParser.Parse([batch]);reader.Children.Clear();
                        var messages=activities.Where(a=>a.Type.Equals("message",StringComparison.OrdinalIgnoreCase)).ToList();var times=messages.Where(a=>a.Timestamp.HasValue).Select(a=>a.Timestamp!.Value).OrderBy(x=>x).ToArray();
                        var duration=times.Length>=2?(times[^1]-times[0]).ToString(@"hh\:mm\:ss"):"durée non disponible";reader.Children.Add(Label(messages.Count+" messages · "+duration,13,"#687080"));
                        var contentTabs=new Pivot();var messagePanel=new StackPanel{Spacing=8};foreach(var message in messages.Take(200))messagePanel.Children.Add(Card(Label(ExplorerRedaction.Safe(message.Role+" · "+message.Text),13)));
                        contentTabs.Items.Add(new PivotItem{Header="Messages",Content=messagePanel});contentTabs.Items.Add(new PivotItem{Header="JSON",Content=Code(ExplorerRedaction.Safe(batch.Content))});contentTabs.Items.Add(new PivotItem{Header="Métadonnées",Content=Code("Identifiant : "+record.Id+"\n"+ExplorerRedaction.Safe(batch.Metadata))});reader.Children.Add(contentTabs);
                    }catch(Exception ex){reader.Children.Clear();reader.Children.Add(Label("Lecture impossible : "+ExplorerRedaction.Safe(DescribeError(ex)),13));await App.ShowErrorAsync(this,App.RecordError(ex));}
                };list.Children.Add(button);}
                layout.Children.Add(new ScrollViewer{Content=list,MaxHeight=500});Place(layout,new ScrollViewer{Content=reader,MaxHeight=500,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled},0,1);body.Children.Add(layout);
            }else body.Children.Add(Label("Les transcriptions ne sont pas encore chargées.",13,"#687080"));
            body.Children.Add(OpButton("Ouvrir le lecteur complet",_=>{ShowTranscripts(agent.Id);return Task.CompletedTask;}));}
        else if(tab==3){var items=operationsStore?.Snapshots(agent.Id)??[];body.Children.Add(Label(items.Count+" instantané(s) locaux",14));foreach(var item in items.OrderByDescending(x=>x.Timestamp).Take(30))body.Children.Add(Label(item.Timestamp.ToLocalTime().ToString("g")+" · "+item.Label,13));body.Children.Add(OpButton("Ouvrir les versions",_=>{operationsAgentId=agent.Id;ShowOperations("Historique");return Task.CompletedTask;}));}
        else if(tab==4){body.Children.Add(OpButton("Charger les incidents",async token=>{var result=await client.ExplorerIncidentsAsync(data,appSettings.IncidentSample,token,appSettings.IncidentDays);incidentsByAgent[agent.Id]=result.Items;incidentWarnings[agent.Id]=result.Warnings;operationsStatus.Text=result.Items.Count+" incident(s) observé(s) dans l’échantillon.";}));if(incidentsByAgent.TryGetValue(agent.Id,out var incidents)){body.Children.Add(Label(incidents.Count+" incident(s) observé(s)",14));foreach(var incident in incidents.Take(30))body.Children.Add(Label(incident.Type+" · "+incident.Summary,13));}else body.Children.Add(Label("Incidents non chargés.",13,"#687080"));}
        else {foreach(var finding in data.Findings)body.Children.Add(Card(Label(FindingLabel(finding.State)+" · "+finding.Title+"\n"+finding.Evidence,13,FindingColor(finding.State))));}
    }
}
