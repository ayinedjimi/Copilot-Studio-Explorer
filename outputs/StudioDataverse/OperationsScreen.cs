using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private readonly StackPanel operationsPanel=new(){Spacing=18,Visibility=Visibility.Collapsed};
    private readonly StackPanel operationsBody=new(){Spacing=18};
    private readonly StackPanel operationsToolbar=new(){Orientation=Orientation.Horizontal,Spacing=10};
    private readonly ComboBox operationsAgent=new(){Header="Agent",MinWidth=300,MaxWidth=520,HorizontalAlignment=HorizontalAlignment.Left};
    private readonly TextBox operationsSearch=new(){PlaceholderText="Filtrer les résultats",MaxWidth=480,HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly TextBlock operationsStatus=Label("Connectez-vous puis chargez l’inventaire.",12,"#687080");
    private readonly CheckBox operationsAll=new(){Content="Tous les agents analysés",IsChecked=false};
    private readonly Dictionary<string,Button> operationsNavigation=[];
    private readonly Dictionary<Guid,AgentInspection> inspections=[];
    private readonly Dictionary<Guid,List<ExplorerIncident>> incidentsByAgent=[];
    private readonly Dictionary<Guid,List<string>> incidentWarnings=[];
    private readonly List<SnapshotDifference> currentDifferences=[];
    private ExplorerLocalStore? operationsStore;
    private CancellationTokenSource? operationsCancellation;
    private string operationsRoute="Audit",reportHtml="";
    private Guid? operationsAgentId;
    private bool operationsBusy,operationsRefreshing,openingOperations;
    private int auditStateFilter,auditSeverityFilter,auditDisplayLimit=150,governanceDisplayLimit=100;
    private string? snapshotBeforeId,snapshotAfterId;
    private readonly ProgressRing operationsProgress=new(){Width=20,Height=20,IsActive=false};

    private void InstallOperationsNavigation(StackPanel nav)
    {
        nav.Children.Add(new Border{Height=1,Background=Brush("#DEDEDE"),Margin=new Thickness(8,10,16,6)});
        var explorerButton=Nav("\uE8B7","Explorateur d’agents");explorerButton.Click+=(_,_)=>ShowOperations("Explorateur");operationsNavigation["Explorateur"]=explorerButton;nav.Children.Add(explorerButton);
        var questionsButton=Nav("\uE8F2","Jeux de questions");questionsButton.Click+=(_,_)=>ShowOperations("Questions");operationsNavigation["Questions"]=questionsButton;nav.Children.Add(questionsButton);
        var chatButton=Nav("\uE8BD","Chat local");chatButton.Click+=(_,_)=>ShowOperations("Chat local");operationsNavigation["Chat local"]=chatButton;nav.Children.Add(chatButton);
        var settingsButton=Nav("\uE713","Paramètres");settingsButton.Click+=(_,_)=>ShowOperations("Paramètres");operationsNavigation["Paramètres"]=settingsButton;nav.Children.Add(settingsButton);
        foreach(var module in new[]{("Analyse IA","#7855D5"),("Sécurité IA","#008C99")}){var item=Nav("\uE734",module.Item1);if(item.Content is StackPanel row&&row.Children[0] is FontIcon icon)icon.Foreground=Brush(module.Item2);var route=module.Item1;item.Click+=(_,_)=>ShowOperations(route);operationsNavigation[route]=item;nav.Children.Add(item);}
        foreach(var item in new[]{("Audit","\uE72E","Audit de sécurité"),("Dépendances","\uE8F1","Dépendances"),("Historique","\uE81C","Historique et versions"),("Incidents","\uE7BA","Centre d’incidents"),("Gouvernance","\uE716","Gouvernance"),("Rapports","\uE9D5","Rapports"),("Solutions","\uE8B7","Solutions"),("Environnements","\uE774","Environnements"),("Recherche","\uE721","Recherche globale"),("Conversations avancées","\uE8F2","Conversations avancées"),("Règles","\uE8D0","Règles personnalisées"),("Évolution des risques","\uE9D2","Évolution des risques"),("Dossiers","\uE716","Dossiers de gouvernance"),("Diagnostic","\uE9CE","Diagnostic connexion"),("Santé","\uE9D9","Santé des agents"),("Scénarios","\uE768","Scénarios de test")})
        {
            var key=item.Item1;var button=Nav(item.Item2,item.Item3);
            button.Click+=(_,_)=>ShowOperations(key);operationsNavigation[key]=button;nav.Children.Add(button);
        }
    }
    private void BuildOperations()
    {
        dependencyWeb.Loaded+=async (_,_)=>await EnsureDependencyWeb();reportWeb.Loaded+=async (_,_)=>await EnsureReportPreview();chatWeb.Loaded+=async (_,_)=>await EnsureLocalChatAsync();
        pageSurface.SizeChanged+=(_,_)=>{if(operationsRoute=="Chat local")chatWeb.Height=Math.Clamp(pageSurface.ActualHeight-330,260,560);};
        operationsPanel.Children.Add(Label("Périmètre Dataverse accessible · nouvelles fonctions en lecture seule · annotations et instantanés locaux.",13,"#536277"));
        var scope=new Grid{ColumnSpacing=18};scope.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});scope.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        scope.Children.Add(operationsAgent);Place(scope,operationsAll,0,1);operationsPanel.Children.Add(scope);
        operationsToolbar.Children.Add(OpButton("Charger tout l’inventaire",LoadOperationsInventory));
        operationsToolbar.Children.Add(OpButton("Analyser cet agent",async token=>await InspectOperationsAgent(OperationsAgent(),token)));
        operationsToolbar.Children.Add(OpButton("Analyser les agents chargés",async token=>{
            var session=client.SessionGeneration;var selected=allAgents.ToArray();var i=0;
            foreach(var agent in selected) {token.ThrowIfCancellationRequested();operationsStatus.Text=$"{++i}/{selected.Length} · {agent.Name}";await InspectOperationsAgent(agent,token);if(session!=client.SessionGeneration)return;}
        }));
        var cancel=new Button{Content="Annuler"};cancel.Click+=(_,_)=>operationsCancellation?.Cancel();operationsToolbar.Children.Add(cancel);operationsToolbar.Children.Add(operationsProgress);
        operationsPanel.Children.Add(operationsToolbar);operationsPanel.Children.Add(operationsStatus);operationsPanel.Children.Add(operationsSearch);operationsPanel.Children.Add(operationsBody);content.Children.Add(operationsPanel);
        operationsAgent.SelectionChanged+=(_,_)=>{if(operationsRefreshing)return;operationsAgentId=(operationsAgent.SelectedItem as ComboBoxItem)?.Tag as Guid?;generatedQuestions="";localAnalysis=localSecurity="";scenarioSelectedId=null;scenarioNew=false;scenarioHistoryVisible=false;currentDifferences.Clear();snapshotBeforeId=snapshotAfterId=null;conversationSamples.Clear();conversationWarnings.Clear();scenarioEndpoint="";targetEnvironmentAgentId=null;manualEnvironmentDiff.Clear();RenderOperations();};
        operationsAll.Checked+=(_,_)=>RenderOperations();operationsAll.Unchecked+=(_,_)=>RenderOperations();
        operationsSearch.TextChanged+=(_,_)=>RenderOperations();
    }
    private Button OpButton(string text,Func<CancellationToken,Task> action)
    {
        var primary=text is "Charger les solutions" or "Rechercher" or "Ajouter la règle" or "Charger les conversations" or "Enregistrer le dossier local" or "Lancer le diagnostic" or "Enregistrer le scénario" or "Connecter l’environnement cible" or "Analyser cet agent";
        var button=new Button{Content=text,IsEnabled=!operationsBusy,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(15,9,15,9),CornerRadius=new CornerRadius(6)};
        if(primary){button.Background=Brush("#6357D5");button.Foreground=Brush("#FFFFFF");}
        button.Click+=async (_,_)=>await RunOperations(action);return button;
    }
    private async Task RunOperations(Func<CancellationToken,Task> action)
    {
        if(operationsBusy)return;
        operationsBusy=true;operationsProgress.IsActive=true;operationsAgent.IsEnabled=false;operationsAll.IsEnabled=false;operationsSearch.IsEnabled=false;
        operationsCancellation?.Dispose();var source=operationsCancellation=new CancellationTokenSource();
        var session=client.SessionGeneration;
        try {await action(source.Token);}
        catch(OperationCanceledException){operationsStatus.Text="Opération annulée ; les résultats déjà collectés sont conservés.";}
        catch(Exception ex){var detail=App.RecordError(ex);operationsStatus.Text=ExplorerRedaction.Safe(DescribeError(ex));await App.ShowErrorAsync(this,detail);}
        finally{operationsBusy=false;operationsProgress.IsActive=false;operationsAgent.IsEnabled=true;operationsAll.IsEnabled=true;operationsSearch.IsEnabled=true;if(session==client.SessionGeneration&&operationsPanel.Visibility==Visibility.Visible)try{RenderOperations();}catch(Exception ex){var detail=App.RecordError(ex);operationsStatus.Text="Impossible de mettre à jour cet écran : "+ExplorerRedaction.Safe(DescribeError(ex));await App.ShowErrorAsync(this,detail);}}
    }
    private Agent OperationsAgent()=>allAgents.FirstOrDefault(x=>x.Id==operationsAgentId)??throw new InvalidOperationException("Sélectionnez un agent dans le périmètre chargé.");
    private List<AgentInspection> OperationsScope()=>operationsAll.IsChecked==true?inspections.Values.OrderBy(x=>x.Name).ToList():operationsAgentId.HasValue&&inspections.TryGetValue(operationsAgentId.Value,out var inspection)?[inspection]:[];
    private bool Match(string text)=>text.Contains(operationsSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase);
    private async Task LoadOperationsInventory(CancellationToken token)
    {
        if(!authenticated)throw new InvalidOperationException("Connectez-vous depuis le menu du compte en bas à gauche.");
        var session=client.SessionGeneration;var loaded=await client.ListAgentsAsync();
        while(client.NextAgentsPath!=null){token.ThrowIfCancellationRequested();operationsStatus.Text=$"Inventaire : {loaded.Count} agents…";loaded.AddRange(await client.MoreAgentsAsync());}
        token.ThrowIfCancellationRequested();if(session!=client.SessionGeneration)return;
        allAgents=loaded.DistinctBy(x=>x.Id).ToList();RefreshLanguages();FilterAgents();RefreshOperationsAgents();operationsStatus.Text=$"{allAgents.Count} agents chargés ; l’audit détaillé reste à lancer.";
    }
    private async Task InspectOperationsAgent(Agent agent,CancellationToken token)
    {
        if(!authenticated)throw new InvalidOperationException("Connexion requise.");
        var session=client.SessionGeneration;
        var result=await client.InspectAgentAsync(agent,message=>operationsStatus.Text=agent.Name+" · "+message,token);
        token.ThrowIfCancellationRequested();if(session!=client.SessionGeneration)return;
        inspections[agent.Id]=result;
        operationsStatus.Text=$"{inspections.Count} agent(s) analysé(s) sur {allAgents.Count} chargé(s). Dernière collecte : {DateTime.Now:HH:mm:ss}.";
    }
    private void SetOperationsSession(string username){ClearOperationsState();operationsStore=new ExplorerLocalStore(username,organization:client.OrganizationUrl);}
    private void ClearOperationsState()
    {
        operationsCancellation?.Cancel();modelDownloadCancellation?.Cancel();ClearLocalChat();inspections.Clear();incidentsByAgent.Clear();incidentWarnings.Clear();currentDifferences.Clear();operationsStore=null;operationsAgentId=null;generatedQuestions="";localAnalysis=localSecurity="";questionGenerationEnabled=false;scenarioSelectedId=null;scenarioNew=false;scenarioHistoryVisible=false;scenarioFilter="";explorerAgentId=explorerExpandedAgent=null;explorerTranscripts.Clear();explorerGroups.Clear();explorerGeneration++;
        targetEnvironmentClient?.Dispose();targetEnvironmentClient=null;targetEnvironmentAgents.Clear();targetEnvironmentUrl="";targetEnvironmentAgentId=null;solutions.Clear();solutionComponents.Clear();searchHits.Clear();customResults.Clear();conversationSamples.Clear();conversationWarnings.Clear();manualEnvironmentDiff.Clear();advancedQuery="";conversationQuery="";conversationDays=appSettings.TranscriptDays.ToString();scenarioEndpoint="";scenarioRegion=appSettings.ScenarioRegion;diagnosticResult="Diagnostic non lancé.";operationsBody.Children.Clear();reportHtml="";loadedReport="";reportNavigation?.TrySetCanceled();reportNavigation=null;dependencyReady=false;dependencyDetail.Children.Clear();
        if(dependencyWeb.CoreWebView2!=null)dependencyWeb.CoreWebView2.PostWebMessageAsJson("{\"type\":\"dependencies\",\"nodes\":[],\"edges\":[]}");
        if(reportWeb.CoreWebView2!=null)reportWeb.CoreWebView2.NavigateToString("<html><body>Session fermée</body></html>");
        operationsStatus.Text="Session fermée. Les résultats précédents ont été retirés de l’affichage.";
    }
    private void RefreshOperationsAgents()
    {
        operationsRefreshing=true;var id=operationsAgentId??SelectedAgentOrNull()?.Id;
        operationsAgent.Items.Clear();
        foreach(var agent in allAgents.OrderBy(x=>x.Name))operationsAgent.Items.Add(new ComboBoxItem{Content=agent.Name,Tag=agent.Id});
        operationsAgent.SelectedIndex=operationsAgent.Items.Count>0?0:-1;
        foreach(ComboBoxItem item in operationsAgent.Items)if(item.Tag is Guid guid&&guid==id)operationsAgent.SelectedItem=item;
        operationsAgentId=(operationsAgent.SelectedItem as ComboBoxItem)?.Tag as Guid?;operationsRefreshing=false;
    }
    private void HideOperations()
    {
        operationsPanel.Visibility=Visibility.Collapsed;operationsCancellation?.Cancel();
        foreach(var button in operationsNavigation.Values)button.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
    private void ShowOperations(string route)
    {
        operationsRoute=route;openingOperations=true;
        try{HideOverview();HideDashboard();HideAgentMap();SetPageChrome(route);viewingTranscripts=false;homePanel.Visibility=agentPanel.Visibility=transcriptPanel.Visibility=Visibility.Collapsed;}
        finally{openingOperations=false;}
        operationsPanel.Visibility=Visibility.Visible;welcome.Text=route=="Audit"?"Audit de sécurité des agents":route;welcome.Visibility=Visibility.Visible;
        content.MaxWidth=double.PositiveInfinity;content.HorizontalAlignment=HorizontalAlignment.Stretch;content.Margin=new Thickness(16);
        homeNav.Background=agentsNav.Background=transcriptsNav.Background=new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        foreach(var pair in operationsNavigation)pair.Value.Background=pair.Key==route?Brush("#E7E7E7"):new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        notice.IsOpen=false;RefreshOperationsAgents();RenderOperations();
    }
    private void RenderOperations()
    {
        if(operationsPanel.Visibility!=Visibility.Visible)return;
        operationsToolbar.Visibility=operationsRoute is "Explorateur" or "Chat local" or "Paramètres"?Visibility.Collapsed:Visibility.Visible;
        operationsAgent.Visibility=operationsRoute is "Explorateur" or "Paramètres"?Visibility.Collapsed:Visibility.Visible;
        if(operationsRoute=="Explorateur"&&operationsBody.Children.Contains(explorerWorkspace)){RefreshExplorerTree();RenderExplorerDetail();return;}
        if(operationsRoute=="Chat local"&&operationsBody.Children.Contains(chatHost))return;
        if(operationsRoute=="Dépendances"&&operationsBody.Children.Contains(dependencyHost)){RefreshDependencies();return;}
        if(operationsRoute=="Rapports"&&operationsBody.Children.Contains(reportHost)){RefreshReports();return;}
        operationsBody.Children.Clear();
        operationsSearch.Visibility=operationsRoute is "Paramètres" or "Explorateur" or "Questions" or "Chat local" or "Analyse IA" or "Sécurité IA" or "Dépendances" or "Rapports" or "Recherche" or "Conversations avancées" or "Diagnostic" or "Santé" or "Scénarios" or "Environnements" or "Règles" or "Dossiers" or "Évolution des risques"?Visibility.Collapsed:Visibility.Visible;
        operationsAll.Visibility=operationsRoute is "Paramètres" or "Explorateur" or "Questions" or "Chat local" or "Analyse IA" or "Sécurité IA" or "Historique" or "Gouvernance" or "Solutions" or "Recherche" or "Conversations avancées" or "Dossiers" or "Diagnostic" or "Scénarios" or "Environnements" or "Évolution des risques"?Visibility.Collapsed:Visibility.Visible;
        switch(operationsRoute){case "Paramètres":RenderSettings();break;case "Explorateur":RenderExplorer();break;case "Questions":RenderQuestionGenerator();break;case "Chat local":RenderLocalChat();break;case "Analyse IA":RenderLocalReview(false);break;case "Sécurité IA":RenderLocalReview(true);break;case "Audit":RenderSecurityAudit();break;case "Dépendances":RenderDependencies();break;case "Historique":RenderSnapshots();break;case "Incidents":RenderIncidents();break;case "Gouvernance":RenderGovernance();break;case "Rapports":RenderReports();break;default:RenderAdvanced(operationsRoute);break;}
    }
    private static string FindingLabel(FindingState state)=>state switch{FindingState.Conforme=>"Conforme",FindingState.AExaminer=>"À examiner",FindingState.Risque=>"Risque",_=>"Non vérifiable"};
    private static string FindingColor(FindingState state)=>state switch{FindingState.Conforme=>"#168A45",FindingState.AExaminer=>"#B76A06",FindingState.Risque=>"#C4314B",_=>"#85838B"};
    private void Metrics(params (string Label,string Value,string Color)[] values)
    {
        var grid=new Grid{ColumnSpacing=12};
        for(int i=0;i<values.Length;i++){grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});var stack=new StackPanel{Spacing=8};stack.Children.Add(Label(values[i].Label,12,"#536277"));var number=Label(values[i].Value,28,values[i].Color);number.FontWeight=FontWeights.SemiBold;stack.Children.Add(number);Place(grid,Card(stack),0,i);}
        operationsBody.Children.Add(grid);
    }
    private void RenderSecurityAudit()
    {
        var scope=OperationsScope();var findings=scope.SelectMany(i=>i.Findings).ToList();
        Metrics(("Risques",findings.Count(f=>f.State==FindingState.Risque).ToString(),"#C4314B"),("À examiner",findings.Count(f=>f.State==FindingState.AExaminer).ToString(),"#B76A06"),("Conformes",findings.Count(f=>f.State==FindingState.Conforme).ToString(),"#168A45"),("Non vérifiables",findings.Count(f=>f.State==FindingState.NonVerifiable).ToString(),"#85838B"));
        operationsBody.Children.Add(Label("Audit statique de configuration · absence de signature ≠ absence de risque · les droits effectifs et protections externes ne sont pas déduits des objets Dataverse.",12,"#687080"));
        AddAuditRuleImport();
        if(scope.Count==0)operationsBody.Children.Add(Label("Sélectionnez un agent et lancez l’analyse. Aucun contrôle n’est simulé.",16));
        var filters=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};
        var state=Options("État","Tous","Risques","À examiner","Non vérifiables","Conformes");state.SelectedIndex=auditStateFilter;
        var severity=Options("Criticité","Toutes","Critique","Élevée","Moyenne","Faible");severity.SelectedIndex=auditSeverityFilter;
        state.SelectionChanged+=(_,_)=>{auditStateFilter=state.SelectedIndex;auditDisplayLimit=150;RenderOperations();};severity.SelectionChanged+=(_,_)=>{auditSeverityFilter=severity.SelectedIndex;auditDisplayLimit=150;RenderOperations();};filters.Children.Add(state);filters.Children.Add(severity);operationsBody.Children.Add(filters);
        bool Allowed(SecurityFinding f)=>(auditStateFilter switch {1=>f.State==FindingState.Risque,2=>f.State==FindingState.AExaminer,3=>f.State==FindingState.NonVerifiable,4=>f.State==FindingState.Conforme,_=>true})&&(auditSeverityFilter==0||f.Severity==new[]{"","Critique","Élevée","Moyenne","Faible"}[auditSeverityFilter]);
        var total=scope.Sum(i=>i.Findings.Count(f=>Allowed(f)&&Match(i.Name+" "+f.Title+" "+f.Severity+" "+FindingLabel(f.State)+" "+f.Evidence+" "+f.Recommendation)));
        operationsBody.Children.Add(Label($"{Math.Min(total,auditDisplayLimit)} / {total} contrôles affichés · les rapports incluent le périmètre analysé complet.",12,"#687080"));
        var remaining=auditDisplayLimit;
        var notes=operationsStore?.Notes()??[];
        foreach(var inspection in scope)
        {
            var relevant=inspection.Findings.Where(f=>Allowed(f)&&Match(inspection.Name+" "+f.Title+" "+f.Severity+" "+FindingLabel(f.State)+" "+f.Evidence+" "+f.Recommendation)).OrderBy(f=>f.State==FindingState.Risque?0:f.State==FindingState.AExaminer?1:f.State==FindingState.NonVerifiable?2:3).Take(remaining).ToArray();
            remaining-=relevant.Length;
            if(relevant.Length==0)continue;
            operationsBody.Children.Add(Label(inspection.Name+" · "+inspection.CapturedAt.ToLocalTime().ToString("g"),21));
            foreach(var finding in relevant)
            {
                var detail=new StackPanel{Spacing=10};
                detail.Children.Add(Label(finding.Evidence,13));detail.Children.Add(Label(finding.Recommendation,14));
                var actionsRow=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
                actionsRow.Children.Add(OpButton("Voir les dépendances",_=>{operationsAgentId=inspection.AgentId;ShowOperations("Dépendances");return Task.CompletedTask;}));
                actionsRow.Children.Add(OpButton("Ouvrir la fiche",_=>{var a=allAgents.FirstOrDefault(x=>x.Id==inspection.AgentId);if(a!=null){ShowAgents();SelectAgent(a);agentWorkspace.IsPaneOpen=true;}return Task.CompletedTask;}));
                detail.Children.Add(actionsRow);
                var note=notes.FirstOrDefault(n=>n.AgentId==inspection.AgentId&&n.FindingId==finding.Id);
                var follow=Options("Suivi local (ne modifie pas le constat)","À traiter","En cours","À recontrôler","Accepté");
                for(int i=0;i<follow.Items.Count;i++)if((follow.Items[i] as ComboBoxItem)?.Content?.ToString()==note?.Status)follow.SelectedIndex=i;
                var comment=new TextBox{PlaceholderText="Commentaire local",Text=note?.Comment??"",AcceptsReturn=true};
                detail.Children.Add(follow);detail.Children.Add(comment);
                detail.Children.Add(OpButton("Enregistrer le suivi local",async _=>{if(operationsStore==null)throw new InvalidOperationException("Reconnectez-vous pour activer le stockage local par compte.");await operationsStore.SaveNote(new(inspection.AgentId,"",(follow.SelectedItem as ComboBoxItem)?.Content?.ToString()??"À traiter",comment.Text,DateTimeOffset.UtcNow,finding.Id));operationsStatus.Text="Suivi local enregistré ; constat Dataverse inchangé.";}));
                var title=Label(finding.Id+" · "+finding.Title+" · "+finding.Severity+" · "+FindingLabel(finding.State),14,FindingColor(finding.State));
                operationsBody.Children.Add(new Expander{Header=title,Content=detail,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch});
            }
        }
        if(total>auditDisplayLimit)operationsBody.Children.Add(OpButton("Afficher 150 contrôles supplémentaires",_=>{auditDisplayLimit+=150;return Task.CompletedTask;}));
    }
    private void RenderSnapshots()
    {
        var agent=allAgents.FirstOrDefault(x=>x.Id==operationsAgentId);
        if(agent==null||operationsStore==null){operationsBody.Children.Add(Label("Connectez-vous et sélectionnez un agent pour consulter ses instantanés locaux.",16));return;}
        var label=new TextBox{Header="Nom de l’instantané",PlaceholderText="Ex. Avant publication",MaxLength=100};
        operationsBody.Children.Add(label);
        operationsBody.Children.Add(OpButton("Relire et capturer un instantané",async token=>{await InspectOperationsAgent(agent,token);token.ThrowIfCancellationRequested();if(operationsStore!=null&&inspections.TryGetValue(agent.Id,out var data)){await operationsStore.SaveSnapshot(data,label.Text);operationsStatus.Text="Instantané local enregistré, configurations sensibles masquées.";}}));
        var snapshots=operationsStore.Snapshots(agent.Id);
        operationsBody.Children.Add(Label($"{snapshots.Count} instantané(s) local(aux) disponible(s). Les secrets détectés ne sont pas conservés.",12,"#687080"));
        if(snapshots.Count==0)return;
        var before=new ComboBox{Header="Avant",HorizontalAlignment=HorizontalAlignment.Stretch};var after=new ComboBox{Header="Après",HorizontalAlignment=HorizontalAlignment.Stretch};
        foreach(var snapshot in snapshots){var title=snapshot.Timestamp.ToLocalTime().ToString("g")+" · "+snapshot.Label;before.Items.Add(new ComboBoxItem{Content=title,Tag=snapshot});after.Items.Add(new ComboBoxItem{Content=title,Tag=snapshot});}
        before.SelectedIndex=snapshots.Count>1?1:0;after.SelectedIndex=0;
        foreach(ComboBoxItem item in before.Items)if((item.Tag as SnapshotEntry)?.Id==snapshotBeforeId)before.SelectedItem=item;
        foreach(ComboBoxItem item in after.Items)if((item.Tag as SnapshotEntry)?.Id==snapshotAfterId)after.SelectedItem=item;
        before.SelectionChanged+=(_,_)=>snapshotBeforeId=((before.SelectedItem as ComboBoxItem)?.Tag as SnapshotEntry)?.Id;
        after.SelectionChanged+=(_,_)=>snapshotAfterId=((after.SelectedItem as ComboBoxItem)?.Tag as SnapshotEntry)?.Id;
        var ignore=new CheckBox{Content="Ignorer les dates de création/modification",IsChecked=true};var diffHost=new StackPanel{Spacing=10};
        operationsBody.Children.Add(before);operationsBody.Children.Add(after);operationsBody.Children.Add(ignore);
        operationsBody.Children.Add(OpButton("Comparer les versions",_=>{
            if((before.SelectedItem as ComboBoxItem)?.Tag is not SnapshotEntry a||(after.SelectedItem as ComboBoxItem)?.Tag is not SnapshotEntry b)return Task.CompletedTask;
            currentDifferences.Clear();currentDifferences.AddRange(SnapshotComparer.Compare(a.Inspection,b.Inspection,ignore.IsChecked==true));operationsStatus.Text=(a.Inspection.Missing.Count>0||b.Inspection.Missing.Count>0?"Comparaison partielle (sources non vérifiables exclues) : ":"Comparaison : ")+currentDifferences.Count+" différence(s).";RenderDiff();
            return Task.CompletedTask;
        }));
        void RenderDiff() {
diffHost.Children.Clear();diffHost.Children.Add(Label(currentDifferences.Count+" différence(s)",20));
            foreach(var difference in currentDifferences.Where(d=>Match(d.Path+" "+d.Before+" "+d.After))) {
                var grid=new Grid{ColumnSpacing=12};grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
                grid.Children.Add(Code(difference.Before));Place(grid,Code(difference.After),0,1);
                diffHost.Children.Add(new Expander{Header=difference.Change+" · "+difference.Path,Content=grid,HorizontalAlignment=HorizontalAlignment.Stretch});
            }
            if(currentDifferences.Count==0)diffHost.Children.Add(Label("Aucune différence dans les champs comparés.",14));
        }
        if(currentDifferences.Count>0)RenderDiff();
        operationsBody.Children.Add(diffHost);
    }
    private void RenderIncidents()
    {
        var sample=Options("Enregistrements de conversation par agent","50","100","250","500");sample.SelectedIndex=appSettings.IncidentSample switch{50=>0,250=>2,500=>3,_=>1};
        operationsBody.Children.Add(sample);
        operationsBody.Children.Add(OpButton("Charger les incidents du périmètre",async token=>{
            var scope=OperationsScope();if(scope.Count==0)throw new InvalidOperationException("Analysez au moins un agent.");
            var session=client.SessionGeneration;
            foreach(var inspection in scope){operationsStatus.Text="Incidents · "+inspection.Name;var result=await client.ExplorerIncidentsAsync(inspection,new[]{50,100,250,500}[Math.Clamp(sample.SelectedIndex,0,3)],token,appSettings.IncidentDays);token.ThrowIfCancellationRequested();if(session!=client.SessionGeneration)return;incidentsByAgent[inspection.AgentId]=result.Items;incidentWarnings[inspection.AgentId]=result.Warnings;}
        }));
        var ids=OperationsScope().Select(x=>x.AgentId).ToHashSet();var incidents=incidentsByAgent.Where(x=>ids.Contains(x.Key)).SelectMany(x=>x.Value).ToList();
        Metrics(("Erreurs observées",incidents.Count.ToString(),"#C4314B"),("Agents concernés",incidents.Select(x=>x.AgentId).Distinct().Count().ToString(),"#6357D5"),("Sources",incidents.Select(x=>x.Source).Distinct().Count().ToString(),"#0078D4"));
        foreach(var warning in incidentWarnings.Where(x=>ids.Contains(x.Key)).SelectMany(x=>x.Value))operationsBody.Children.Add(Label(warning,11,"#85838B"));
        foreach(var day in incidents.Where(x=>x.Time.HasValue).GroupBy(x=>x.Time!.Value.ToLocalTime().Date).OrderByDescending(x=>x.Key))operationsBody.Children.Add(Label(day.Key.ToString("dd/MM/yyyy")+" · "+day.Count()+" erreur(s) observée(s)",13));
        var groups=incidents.Where(i=>Match(i.AgentName+" "+i.Type+" "+i.Summary+" "+i.Correlation)).GroupBy(i=>ExplorerRedaction.Hash(i.Source+"|"+i.Type+"|"+System.Text.RegularExpressions.Regex.Replace(i.Summary,@"\b[0-9a-fA-F-]{36}\b","[ID]")));
        foreach(var group in groups.Take(200)) {
            var detail=new StackPanel{Spacing=12};var first=group.First();
            foreach(var item in group.OrderByDescending(x=>x.Time).Take(25)) {
                detail.Children.Add(Label(item.AgentName+" · "+item.Time?.ToLocalTime().ToString("g")+" · Corrélation : "+item.Correlation,12));
                detail.Children.Add(Code(item.Summary));
            }
            if(group.Count()>25)detail.Children.Add(Label("Les 25 occurrences les plus récentes sont affichées ; les exports conservent les occurrences chargées.",12,"#687080"));
            detail.Children.Add(OpButton("Voir les transcriptions",async _=>{ShowTranscripts(first.AgentId);await LoadTranscriptsAsync(false);}));
            detail.Children.Add(OpButton("Examiner les dépendances",_=>{operationsAgentId=first.AgentId;ShowOperations("Dépendances");return Task.CompletedTask;}));
            operationsBody.Children.Add(new Expander{Header=first.Source+" · "+first.Type+" · "+group.Count()+" occurrence(s)",Content=detail,HorizontalAlignment=HorizontalAlignment.Stretch});
        }
    }
    private void RenderGovernance()
    {
        var notes=operationsStore?.Notes()??[];
        Metrics(("Inventaire chargé",allAgents.Count.ToString(),"#6357D5"),("Propriétaires désactivés",inspections.Values.Count(x=>x.OwnerState=="Désactivé").ToString(),"#C4314B"),("Références introuvables",inspections.Values.Sum(x=>x.Missing.Count(m=>m.StartsWith("connection:")&&m.EndsWith(":introuvable"))).ToString(),"#B76A06"));
        operationsBody.Children.Add(Label("Les agents anciens ne sont pas assimilés à des agents inutilisés : l’absence d’activité ne peut être conclue qu’à partir d’un historique complet. Les statuts de suivi ci-dessous sont locaux.",12,"#687080"));
        foreach(var agent in allAgents.Where(a=>Match(a.Name+" "+string.Join(" ",notes.Where(n=>n.AgentId==a.Id).Select(n=>n.Tags)))).Take(governanceDisplayLimit)) {
            inspections.TryGetValue(agent.Id,out var inspection);
            var note=notes.FirstOrDefault(n=>n.AgentId==agent.Id&&n.FindingId=="");
            var detail=new StackPanel{Spacing=10};
            var owner=inspection==null?"À charger":ExplorerInspection.Text(inspection.Agent,"_ownerid_value@OData.Community.Display.V1.FormattedValue");
            detail.Children.Add(Label("Propriétaire : "+(owner.Length>0?owner:"Non renseigné")+" · État : "+(inspection?.OwnerState??"Non vérifiable"),13));
            detail.Children.Add(Label(agent.StatusLabel+" · "+(agent.PublishedOn.HasValue?"Déjà publié":"Jamais publié")+" · Solution : "+(agent.Managed==true?"managée":agent.Managed==false?"non managée":"inconnue")+" · Modification : "+agent.ModifiedLabel,12));
            if(agent.ModifiedOn<DateTimeOffset.UtcNow.AddDays(-90))detail.Children.Add(Label("Agent non modifié depuis plus de 90 jours : à examiner, sans conclure sur son utilisation.",12,"#B76A06"));
            if(inspection!=null) {
                detail.Children.Add(Label("Dernière transcription accessible : "+(inspection.LastConversation?.ToLocalTime().ToString("g")??inspection.ActivityState),12));
                foreach(var connection in inspection.Connections)detail.Children.Add(Label("Connexion : "+ExplorerInspection.Text(connection,"connectionreferencedisplayname")+" · référence "+ExplorerInspection.Text(connection,"connectionreferencelogicalname")+" · "+(ExplorerInspection.Text(connection,"connectionid").Length>0?"identifiant renseigné, validité en exécution non vérifiée":"sans identifiant de connexion : à examiner"),12));
                foreach(var missing in inspection.Missing.Where(x=>x.StartsWith("connection:")))detail.Children.Add(Label("Connexion à vérifier : "+missing,12,"#B76A06"));
            }
            var tags=new TextBox{Header="Étiquettes locales",Text=note?.Tags??"",PlaceholderText="Production, Finance, À revoir",MaxLength=300};
            var comment=new TextBox{Header="Commentaire local",Text=note?.Comment??"",AcceptsReturn=true,MaxLength=3000};
            detail.Children.Add(tags);detail.Children.Add(comment);
            detail.Children.Add(OpButton("Enregistrer les étiquettes",async _=>{if(operationsStore==null)throw new InvalidOperationException("Connexion requise pour isoler le stockage par compte.");await operationsStore.SaveNote(new(agent.Id,tags.Text,"Inventaire",comment.Text,DateTimeOffset.UtcNow));operationsStatus.Text="Étiquettes locales enregistrées.";}));
            detail.Children.Add(OpButton("Auditer cet agent",async token=>{operationsAgentId=agent.Id;await InspectOperationsAgent(agent,token);ShowOperations("Audit");}));
            foreach(var follow in notes.Where(n=>n.AgentId==agent.Id&&n.FindingId!=""))detail.Children.Add(Label(follow.FindingId+" · "+follow.Status+" · "+follow.Comment,12));
            operationsBody.Children.Add(new Expander{Header=agent.Name+" · "+(note?.Tags??""),Content=detail,HorizontalAlignment=HorizontalAlignment.Stretch});
        }
        if(allAgents.Count>governanceDisplayLimit)operationsBody.Children.Add(OpButton("Afficher 100 agents supplémentaires",_=>{governanceDisplayLimit+=100;return Task.CompletedTask;}));
    }
}

