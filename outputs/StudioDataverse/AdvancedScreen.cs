using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
namespace StudioDataverse;
public sealed partial class MainWindow
{
    private List<JsonElement> solutions=[];
    private List<JsonElement> solutionComponents=[];
    private Guid? solutionId;
    private List<SearchHit> searchHits=[];
    private List<CustomRuleResult> customResults=[];
    private List<(TranscriptRecord Record,List<TranscriptActivity> Activities)> conversationSamples=[];
    private List<string> conversationWarnings=[];
    private readonly List<string> manualEnvironmentDiff=[];
    private DataverseClient? targetEnvironmentClient;
    private readonly List<Agent> targetEnvironmentAgents=[];
    private string targetEnvironmentUrl="";
    private Guid? targetEnvironmentAgentId;
    private void RenderAdvanced(string route)
    {
        switch(route) {
            case "Solutions":RenderSolutions();break;
            case "Recherche":RenderGlobalSearch();break;
            case "Règles":RenderCustomRules();break;
            case "Conversations avancées":RenderAdvancedConversations();break;
            case "Évolution des risques":RenderRiskHistory();break;
            case "Dossiers":RenderGovernanceRecords();break;
            case "Diagnostic":RenderDiagnostic();break;
            case "Santé":RenderHealth();break;
            case "Scénarios":RenderScenarios();break;
            case "Environnements":RenderEnvironments();break;
        }
    }
    private void RenderSolutions()
    {
        operationsBody.Children.Add(Label("Solutions accessibles dans l’environnement connecté ; les correspondances avec les agents et flux utilisent les identifiants des composants réellement retournés.",13));
        operationsBody.Children.Add(OpButton("Charger les solutions",async token=>{solutions=await client.ExplorerSolutionsAsync(token);solutionId=null;solutionComponents.Clear();operationsStatus.Text=solutions.Count+" solution(s) chargée(s).";}));
        foreach(var solution in solutions.Where(s=>Match(ExplorerInspection.Text(s,"friendlyname")+" "+ExplorerInspection.Text(s,"uniquename"))).Take(300)) {
            if(!Guid.TryParse(ExplorerInspection.Text(solution,"solutionid"),out var id))continue;
            var panel=new StackPanel{Spacing=10};
            panel.Children.Add(Label("Nom unique : "+ExplorerInspection.Text(solution,"uniquename")+" · Version : "+ExplorerInspection.Text(solution,"version")+" · Éditeur : "+ExplorerInspection.Text(solution,"_publisherid_value@OData.Community.Display.V1.FormattedValue"),13));
            panel.Children.Add(OpButton("Lire les composants",async token=>{solutionComponents=await client.ExplorerSolutionComponentsAsync(id,token);solutionId=id;operationsStatus.Text=solutionComponents.Count+" composant(s) de solution chargé(s).";}));
            if(solutionId==id)foreach(var component in solutionComponents.Take(500)) {
                var obj=ExplorerInspection.Text(component,"objectid");
                var agent=allAgents.FirstOrDefault(a=>a.Id.ToString().Equals(obj,StringComparison.OrdinalIgnoreCase));
                var flow=inspections.Values.SelectMany(x=>x.Flows).FirstOrDefault(x=>ExplorerInspection.Text(x,"workflowid").Equals(obj,StringComparison.OrdinalIgnoreCase));
                panel.Children.Add(Label((agent?.Name??(flow.ValueKind==JsonValueKind.Object?ExplorerInspection.Text(flow,"name"):"Objet "+obj))+" · type "+ExplorerInspection.Text(component,"componenttype"),12));
            }
            if(solutionId==id) {
                var present=solutionComponents.Select(c=>ExplorerInspection.Text(c,"objectid")).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var associated=inspections.Values.Where(a=>present.Contains(a.AgentId.ToString())).ToArray();
                foreach(var inspected in associated) {
                    foreach(var node in inspected.Nodes.Where(n=>n.Id.StartsWith("component:")||n.Id.StartsWith("flow:"))) {
                        var objectId=node.Id[(node.Id.IndexOf(':')+1)..];
                        if(Guid.TryParse(objectId,out _)&&!present.Contains(objectId))panel.Children.Add(Label("À vérifier dans la solution : "+node.Label+" · une dépendance peut être incluse indirectement ou externe.",12,"#B76A06"));
                    }
                }
                if(associated.Length==0)panel.Children.Add(Label("Analysez les agents de cette solution pour examiner leurs dépendances déclarées.",12,"#687080"));
            }
            var section=new Expander{Header=ExplorerInspection.Text(solution,"friendlyname")+" · "+ExplorerInspection.Text(solution,"version"),Content=panel,IsExpanded=solutionId==id,HorizontalAlignment=HorizontalAlignment.Stretch};operationsBody.Children.Add(section);
        }
        operationsBody.Children.Add(Label("Un objet sans nom résolu reste identifiable par son ID ; sa présence ne garantit pas que toutes ses dépendances sont dans la solution.",12,"#687080"));
    }
    private void RenderGlobalSearch()
    {
        operationsBody.Children.Add(Label("Recherche dans les agents déjà analysés : noms et configurations masquées des composants et flux. Lancez l’analyse des agents souhaités avant de rechercher.",13));
        var query=new TextBox{Header="Texte recherché",PlaceholderText="Nom, URL, opération…",Text=advancedQuery};
        operationsBody.Children.Add(query);
        operationsBody.Children.Add(OpButton("Rechercher",_=>{advancedQuery=query.Text;searchHits=AdvancedFunctions.Search(inspections.Values,advancedQuery);operationsStatus.Text=searchHits.Count+" résultat(s) dans "+inspections.Count+" agent(s) analysé(s).";return Task.CompletedTask;}));
        foreach(var hit in searchHits.Take(300)) {
            var detail=new StackPanel{Spacing=6};detail.Children.Add(Label(hit.Agent+" · "+hit.Object+" · "+hit.Field,13));detail.Children.Add(Code(hit.Excerpt));
            detail.Children.Add(OpButton("Voir les dépendances",_=>{operationsAgentId=hit.AgentId;ShowOperations("Dépendances");return Task.CompletedTask;}));operationsBody.Children.Add(Card(detail));
        }
        if(searchHits.Count>300)operationsBody.Children.Add(Label("300 premiers résultats affichés ; affinez la recherche.",12));
    }
    private string advancedQuery="";
    private void RenderCustomRules()
    {
        if(operationsStore==null){operationsBody.Children.Add(Label("Connectez-vous pour enregistrer les règles par compte.",14));return;}
        var rules=operationsStore.Rules();
        var name=new TextBox{Header="Nom de la règle"};var field=Options("Champ","Nom","Instructions","Composants","Flux");var operation=Options("Opérateur","Contient","Ne contient pas","Commence par");var value=new TextBox{Header="Texte recherché"};
        operationsBody.Children.Add(name);operationsBody.Children.Add(field);operationsBody.Children.Add(operation);operationsBody.Children.Add(value);
        operationsBody.Children.Add(OpButton("Ajouter la règle",async _=>{
            if(string.IsNullOrWhiteSpace(name.Text)||string.IsNullOrWhiteSpace(value.Text))throw new InvalidOperationException("Renseignez le nom et le texte.");
            rules.Add(new(Guid.NewGuid().ToString("N"),name.Text,((ComboBoxItem)field.SelectedItem).Content.ToString()!,((ComboBoxItem)operation.SelectedItem).Content.ToString()!,value.Text));
            await operationsStore.SaveRules(rules);operationsStatus.Text="Règle locale enregistrée.";
        }));
        operationsBody.Children.Add(OpButton("Évaluer sur les agents analysés",_=>{customResults=AdvancedFunctions.EvaluateRules(rules,OperationsScope());operationsStatus.Text=customResults.Count+" résultat(s) de règles ; audit standard inchangé.";return Task.CompletedTask;}));
        foreach(var rule in rules) {
            var box=new StackPanel{Spacing=6};box.Children.Add(Label(rule.Field+" · "+rule.Operation+" · "+rule.Value,13));
            box.Children.Add(OpButton("Supprimer cette règle locale",async _=>{await operationsStore.SaveRules(rules.Where(x=>x.Id!=rule.Id).ToList());customResults.RemoveAll(x=>x.Rule.Id==rule.Id);}));
            foreach(var result in customResults.Where(x=>x.Rule.Id==rule.Id))box.Children.Add(Label(result.Agent+" · "+FindingLabel(result.State)+" · "+result.Evidence,12,FindingColor(result.State)));
            operationsBody.Children.Add(new Expander{Header=rule.Name,Content=box,HorizontalAlignment=HorizontalAlignment.Stretch});
        }
        operationsBody.Children.Add(Label("Règles locales appliquées aux données lisibles et déjà masquées. Elles ne modifient ni Dataverse ni les contrôles standard.",12,"#687080"));
    }
    private void RenderAdvancedConversations()
    {
        var days=new TextBox{Header="Période en jours",Text="30"};var search=new TextBox{Header="Filtrer les messages chargés",Text=conversationQuery};
        operationsBody.Children.Add(days);operationsBody.Children.Add(search);
        operationsBody.Children.Add(OpButton("Charger les conversations",async token=>{
            var agent=OperationsAgent();if(!int.TryParse(days.Text,out var count)||count<1||count>365)throw new InvalidOperationException("Période : 1 à 365 jours.");
            var page=await client.ListTranscriptsAsync(DataverseClient.TranscriptListPath(agent.Id,DateTimeOffset.UtcNow.AddDays(-count),null));
            var data=new List<(TranscriptRecord,List<TranscriptActivity>)>();var warnings=new List<string>();var session=client.SessionGeneration;
            foreach(var record in page.Items.Take(50)) {
                token.ThrowIfCancellationRequested();
                try {data.Add((record,await client.ExplorerConversationAsync(record.Id,token)));}catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}catch{warnings.Add("Conversation inaccessible : "+record.Id);}
            }
            if(session!=client.SessionGeneration)return;conversationSamples=data;conversationWarnings=warnings;
            if(page.Next!=null||page.Items.Count>50)conversationWarnings.Add("Échantillon limité aux 50 premiers enregistrements ; résultats non exhaustifs.");
            conversationQuery=search.Text;operationsStatus.Text=data.Count+" enregistrement(s) chargé(s).";
        }));
        operationsBody.Children.Add(OpButton("Appliquer le filtre",_=>{conversationQuery=search.Text;return Task.CompletedTask;}));
        operationsBody.Children.Add(OpButton("Exporter l’échantillon masqué",async _=>{
            var file=await OperationsSavePicker(".txt","Texte");if(file==null)return;
            var text=string.Join("\r\n",conversationSamples.SelectMany(c=>c.Activities.Where(a=>a.Text.Contains(conversationQuery,StringComparison.OrdinalIgnoreCase)).Select(a=>ExplorerRedaction.Safe(c.Record.Name+" · "+a.Timestamp?.ToString("O")+" · "+a.Role+" : "+a.Text))));
            await File.WriteAllTextAsync(file.Path,text,new UTF8Encoding(true));
        }));
        foreach(var warning in conversationWarnings)operationsBody.Children.Add(Label(warning,12,"#B76A06"));
        foreach(var conversation in conversationSamples.Where(c=>c.Activities.Any(a=>a.Text.Contains(conversationQuery,StringComparison.OrdinalIgnoreCase)))) {
            var panel=new StackPanel{Spacing=8};foreach(var activity in conversation.Activities.Where(a=>a.Text.Contains(conversationQuery,StringComparison.OrdinalIgnoreCase)))panel.Children.Add(Label(ExplorerRedaction.Safe(activity.Role+" · "+activity.Timestamp?.ToLocalTime().ToString("g")+"\n"+activity.Text),13));
            operationsBody.Children.Add(new Expander{Header=ExplorerRedaction.Safe(conversation.Record.Name)+" · "+conversation.Record.StartLabel,Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch});
        }
        operationsBody.Children.Add(Label("Le masquage des e-mails, téléphones et secrets est heuristique ; relisez l’export avant diffusion.",12,"#687080"));
    }
    private string conversationQuery="";
    private string scenarioEndpoint="";
    private void RenderRiskHistory()
    {
        if(operationsStore==null||operationsAgentId==null)return;
        var snapshots=operationsStore.Snapshots(operationsAgentId.Value).OrderBy(x=>x.Timestamp).ToArray();
        operationsBody.Children.Add(Label("Évolution des constats entre instantanés locaux. Une source inaccessible rend la comparaison partielle.",13));
        foreach(var snapshot in snapshots) {
            var findings=snapshot.Inspection.Findings;
            var risk=findings.Count(f=>f.State==FindingState.Risque);var review=findings.Count(f=>f.State==FindingState.AExaminer);var unknown=findings.Count(f=>f.State==FindingState.NonVerifiable);
            var row=new StackPanel{Spacing=5};row.Children.Add(Label(snapshot.Timestamp.ToLocalTime().ToString("g")+" · "+snapshot.Label,13));row.Children.Add(Label($"{risk} risques · {review} à examiner · {unknown} non vérifiables",13));row.Children.Add(new ProgressBar{Minimum=0,Maximum=16,Value=risk,Foreground=Brush("#C4314B"),Height=5});operationsBody.Children.Add(Card(row));
        }
        if(snapshots.Length<2)operationsBody.Children.Add(Label("Capturez au moins deux instantanés dans Historique et versions pour comparer l’évolution.",14));
        else {
            var before=snapshots[^2].Inspection.Findings.ToDictionary(x=>x.Id);
            foreach(var current in snapshots[^1].Inspection.Findings.Where(x=>before.TryGetValue(x.Id,out var old)&&old.State!=x.State))
                operationsBody.Children.Add(Label(current.Id+" · "+current.Title+" : "+FindingLabel(before[current.Id].State)+" → "+FindingLabel(current.State),13));
        }
    }
    private void RenderGovernanceRecords()
    {
        if(operationsStore==null||operationsAgentId==null)return;var id=operationsAgentId.Value;var records=operationsStore.Records();var current=records.FirstOrDefault(x=>x.AgentId==id);
        var owner=new TextBox{Header="Responsable déclaré localement",Text=current?.Owner??""};var criticality=Options("Criticité métier","Non définie","Faible","Moyenne","Élevée","Critique");
        for(int i=0;i<criticality.Items.Count;i++)if(((ComboBoxItem)criticality.Items[i]).Content.ToString()==current?.BusinessCriticality)criticality.SelectedIndex=i;
        var date=new TextBox{Header="Prochaine revue (AAAA-MM-JJ)",Text=current?.ReviewDue?.ToString("yyyy-MM-dd")??""};var comment=new TextBox{Header="Commentaire",Text=current?.Comment??"",AcceptsReturn=true};
        operationsBody.Children.Add(owner);operationsBody.Children.Add(criticality);operationsBody.Children.Add(date);operationsBody.Children.Add(comment);
        operationsBody.Children.Add(OpButton("Enregistrer le dossier local",async _=>{
            DateTimeOffset? due=null;if(date.Text.Trim().Length>0) {if(!DateTimeOffset.TryParseExact(date.Text.Trim(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeLocal,out var parsed))throw new InvalidOperationException("Date attendue : AAAA-MM-JJ.");due=parsed;}
            records.RemoveAll(x=>x.AgentId==id);records.Add(new(id,owner.Text,((ComboBoxItem)criticality.SelectedItem).Content.ToString()!,due,comment.Text));await operationsStore.SaveRecords(records);operationsStatus.Text="Dossier local enregistré.";
        }));
        operationsBody.Children.Add(OpButton("Exporter les dossiers pour l’équipe",async _=>{
            var file=await OperationsSavePicker(".json","Dossiers de gouvernance");if(file!=null)await File.WriteAllTextAsync(file.Path,JsonSerializer.Serialize(operationsStore.Records()),new UTF8Encoding(true));
        }));
        operationsBody.Children.Add(OpButton("Importer des dossiers",async _=>{
            var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".json");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file=await picker.PickSingleFileAsync();if(file==null)return;if(new FileInfo(file.Path).Length>5_000_000)throw new InvalidOperationException("Fichier supérieur à 5 Mo.");
            var incoming=JsonSerializer.Deserialize<List<GovernanceRecord>>(await File.ReadAllTextAsync(file.Path))??throw new InvalidOperationException("Fichier invalide.");
            var existing=operationsStore.Records();var added=0;
            foreach(var record in incoming.Where(x=>x!=null&&x.AgentId!=Guid.Empty))if(existing.All(x=>x.AgentId!=record.AgentId)){existing.Add(record with{Owner=record.Owner??"",BusinessCriticality=record.BusinessCriticality??"Non définie",Comment=record.Comment??""});added++;}
            await operationsStore.SaveRecords(existing);operationsStatus.Text=added+" dossier(s) importé(s) ; les dossiers locaux existants ont été conservés.";
        }));
        operationsBody.Children.Add(Label("Données locales au compte : exportez ou importez un fichier pour les partager. Les dossiers existants ne sont pas remplacés à l’import et les propriétaires Dataverse ne sont pas modifiés.",12,"#687080"));
    }
    private void RenderDiagnostic()
    {
        operationsBody.Children.Add(Label("Vérifie la session et l’accès courant à Dataverse sans afficher le jeton.",13));
        operationsBody.Children.Add(OpButton("Lancer le diagnostic",async token=>{
            if(!authenticated)throw new InvalidOperationException("Connectez-vous d’abord.");
            try{using var result=JsonDocument.Parse(await client.SendAsync(HttpMethod.Get,"WhoAmI",cancellationToken:token,redactTrace:true));diagnosticResult="Session valide · accès à l’environnement vérifié. Les droits sur chaque table restent à contrôler séparément.";}
            catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}catch(Exception ex){diagnosticResult=AdvancedFunctions.Diagnostic(ex);}
        }));
        operationsBody.Children.Add(Label(diagnosticResult,14));
    }
    private string diagnosticResult="Diagnostic non lancé.";
    private void RenderHealth()
    {
        var scope=OperationsScope();var findings=scope.SelectMany(x=>x.Findings).ToArray();var ids=scope.Select(x=>x.AgentId).ToHashSet();
        Metrics(("Agents analysés",scope.Count.ToString(),"#6357D5"),("Risques",findings.Count(x=>x.State==FindingState.Risque).ToString(),"#C4314B"),("Non vérifiables",findings.Count(x=>x.State==FindingState.NonVerifiable).ToString(),"#85838B"),("Incidents chargés",incidentsByAgent.Where(x=>ids.Contains(x.Key)).Sum(x=>x.Value.Count).ToString(),"#B76A06"));
        foreach(var item in scope) {var risk=item.Findings.Count(x=>x.State==FindingState.Risque);var unknown=item.Findings.Count(x=>x.State==FindingState.NonVerifiable);var box=new StackPanel{Spacing=8};box.Children.Add(Label(item.Name+" · "+risk+" risque(s) · "+unknown+" non vérifiable(s)",14));box.Children.Add(new ProgressBar{Minimum=0,Maximum=item.Findings.Count,Value=risk,Foreground=Brush("#C4314B"),Height=6});operationsBody.Children.Add(Card(box));}
        operationsBody.Children.Add(Label("Résultats limités aux agents analysés et incidents chargés ; ils ne représentent pas une mesure exhaustive du tenant.",12));
    }
    private void RenderScenarios()
    {
        if(operationsStore==null)return;var cases=operationsStore.Scenarios();
        var title=new TextBox{Header="Nom du scénario"};var question=new TextBox{Header="Question",AcceptsReturn=true};var expected=new TextBox{Header="Texte attendu"};var observed=new TextBox{Header="Réponse observée",AcceptsReturn=true};
        operationsBody.Children.Add(Label("La réponse peut être saisie ou obtenue par un test manuel via le canal de conversation. Un test réel peut déclencher les actions de l’agent : utilisez un environnement de test.",13));
        var endpoint=new TextBox{Header="Point d’accès de conversation de l’agent sélectionné",PlaceholderText="URL du canal mobile depuis Copilot Studio",Text=scenarioEndpoint};
        var region=Options("Région du canal","Europe","Global","Inde");operationsBody.Children.Add(endpoint);operationsBody.Children.Add(region);
        operationsBody.Children.Add(title);operationsBody.Children.Add(question);operationsBody.Children.Add(expected);operationsBody.Children.Add(observed);
        operationsBody.Children.Add(OpButton("Enregistrer le scénario",async _=>{if(string.IsNullOrWhiteSpace(title.Text)||string.IsNullOrWhiteSpace(question.Text))throw new InvalidOperationException("Nom et question requis.");cases.Add(new(Guid.NewGuid().ToString("N"),title.Text,question.Text,expected.Text,observed.Text,OperationsAgent().Id));await operationsStore.SaveScenarios(cases);}));
        foreach(var item in cases.Where(c=>c.AgentId==Guid.Empty||c.AgentId==operationsAgentId)) {
            var panel=new StackPanel{Spacing=8};panel.Children.Add(Label("Question : "+item.Question,13));panel.Children.Add(Label("Attendu : "+item.Expected,13));
            var response=new TextBox{Header="Réponse observée",Text=item.Observed,AcceptsReturn=true};panel.Children.Add(response);
            panel.Children.Add(OpButton("Lancer ce scénario réel",async token=>{
                if(item.AgentId!=Guid.Empty&&item.AgentId!=OperationsAgent().Id)throw new InvalidOperationException("Ce scénario appartient à un autre agent.");
                scenarioEndpoint=endpoint.Text.Trim();using var runner=new ScenarioRunner();operationsStatus.Text="Conversation en cours · attendez la première réponse textuelle…";
                var answer=await runner.RunAsync(scenarioEndpoint,((ComboBoxItem)region.SelectedItem).Content.ToString()!,item.Question,token);
                var updated=cases.Select(c=>c.Id==item.Id?c with{Observed=answer}:c).ToList();await operationsStore.SaveScenarios(updated);operationsStatus.Text="Première réponse textuelle reçue et enregistrée localement ; vérifiez son contexte.";
            }));
            panel.Children.Add(OpButton("Enregistrer la réponse",async _=>{var updated=cases.Select(c=>c.Id==item.Id?c with{Observed=response.Text}:c).ToList();await operationsStore.SaveScenarios(updated);}));
            panel.Children.Add(OpButton("Supprimer le scénario local",async _=>await operationsStore.SaveScenarios(cases.Where(c=>c.Id!=item.Id).ToList())));
            operationsBody.Children.Add(new Expander{Header=item.Title+" · "+AdvancedFunctions.ScenarioStatus(item),Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch});
        }
    }
    private void RenderEnvironments()
    {
        if(operationsStore==null||operationsAgentId==null)return;
        var snapshots=operationsStore.Snapshots(operationsAgentId.Value);
        operationsBody.Children.Add(Label("Comparez un instantané local avec un agent d’un autre environnement du même tenant, ou avec un instantané exporté. Les deux côtés sont lus séparément.",13));
        var url=new TextBox{Header="URL de l’environnement cible",PlaceholderText="https://organisation.crm17.dynamics.com",Text=targetEnvironmentUrl};operationsBody.Children.Add(url);
        operationsBody.Children.Add(OpButton("Connecter l’environnement cible",async token=> {
            var address=url.Text.Trim();var next=new DataverseClient(address);
            try {
                await next.SignInAsync(token);var agents=await next.ListAgentsAsync();
                while(next.NextAgentsPath!=null){token.ThrowIfCancellationRequested();agents.AddRange(await next.MoreAgentsAsync());}
                token.ThrowIfCancellationRequested();targetEnvironmentClient?.Dispose();targetEnvironmentClient=next;targetEnvironmentUrl=address;targetEnvironmentAgents.Clear();targetEnvironmentAgents.AddRange(agents);targetEnvironmentAgentId=null;manualEnvironmentDiff.Clear();operationsStatus.Text=agents.Count+" agent(s) accessibles dans l’environnement cible.";
            } catch {next.Dispose();throw;}
        }));
        if(targetEnvironmentClient!=null) {
            var target=new ComboBox{Header="Agent cible",HorizontalAlignment=HorizontalAlignment.Stretch};
            foreach(var agent in targetEnvironmentAgents.OrderBy(a=>a.Name))target.Items.Add(new ComboBoxItem{Content=agent.Name+" · "+agent.Schema,Tag=agent.Id});
            var current=allAgents.FirstOrDefault(a=>a.Id==operationsAgentId);
            var candidates=targetEnvironmentAgents.Where(a=>current!=null&&a.Schema.Equals(current.Schema,StringComparison.OrdinalIgnoreCase)).ToArray();
            var desired=targetEnvironmentAgentId??(candidates.Length==1?candidates[0].Id:(Guid?)null);
            foreach(ComboBoxItem item in target.Items)if(item.Tag is Guid id&&id==desired)target.SelectedItem=item;
            target.SelectionChanged+=(_,_)=>{targetEnvironmentAgentId=(target.SelectedItem as ComboBoxItem)?.Tag as Guid?;manualEnvironmentDiff.Clear();if(!operationsBusy)RenderOperations();};
            operationsBody.Children.Add(target);
            operationsBody.Children.Add(OpButton("Comparer avec l’environnement cible",async token=> {
                if((target.SelectedItem as ComboBoxItem)?.Tag is not Guid id)throw new InvalidOperationException("Sélectionnez l’agent cible.");
                if(!inspections.TryGetValue(operationsAgentId!.Value,out var currentInspection))throw new InvalidOperationException("Analysez d’abord l’agent courant.");
                var chosen=targetEnvironmentAgents.First(a=>a.Id==id);var external=await targetEnvironmentClient.InspectAgentAsync(chosen,message=>operationsStatus.Text="Cible · "+message,token);
                token.ThrowIfCancellationRequested();CompareEnvironment(currentInspection,external,"Environnement cible · "+chosen.Name);
                try {
                    if(currentInspection.Missing.Contains("components")||external.Missing.Contains("components")||currentInspection.Missing.Any(x=>x.StartsWith("flow:"))||external.Missing.Any(x=>x.StartsWith("flow:")))throw new InvalidOperationException("Références incomplètes.");
                    var localVariables=await client.ExplorerVariableMetadataAsync(token);var targetVariables=await targetEnvironmentClient.ExplorerVariableMetadataAsync(token);
                    var sourceText=string.Join(" ",currentInspection.Components.Concat(currentInspection.Flows).Select(x=>x.ToString()))+" "+string.Join(" ",external.Components.Concat(external.Flows).Select(x=>x.ToString()));
                    Dictionary<string,string> Referenced(List<JsonElement> rows)=>rows.Where(x=>ExplorerInspection.Text(x,"schemaname").Length>0&&sourceText.Contains(ExplorerInspection.Text(x,"schemaname"),StringComparison.OrdinalIgnoreCase)).GroupBy(x=>ExplorerInspection.Text(x,"schemaname"),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>ExplorerInspection.Text(g.First(),"type"),StringComparer.OrdinalIgnoreCase);
                    var left=Referenced(localVariables);var right=Referenced(targetVariables);
                    foreach(var name in left.Keys.Union(right.Keys,StringComparer.OrdinalIgnoreCase).Where(n=>left.GetValueOrDefault(n)!=right.GetValueOrDefault(n)))manualEnvironmentDiff.Add("Variable / "+name+" · type local : "+left.GetValueOrDefault(name,"(absente)")+" · type cible : "+right.GetValueOrDefault(name,"(absente)"));
                    manualEnvironmentDiff.Add("Variables référencées : comparaison des noms et types uniquement ; valeurs et secrets non lus.");
                } catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}catch{manualEnvironmentDiff.Add("Variables d’environnement non vérifiables : accès incomplet.");}
            }));
        }
        if(snapshots.Count==0){operationsBody.Children.Add(Label("Pour la comparaison par fichier, capturez d’abord un instantané dans Historique et versions.",14));return;}
        var select=new ComboBox{Header="Instantané local",HorizontalAlignment=HorizontalAlignment.Stretch};foreach(var snapshot in snapshots)select.Items.Add(new ComboBoxItem{Content=snapshot.Timestamp.ToLocalTime().ToString("g")+" · "+snapshot.Label,Tag=snapshot});select.SelectedIndex=0;operationsBody.Children.Add(select);
        operationsBody.Children.Add(OpButton("Exporter cet instantané",async _=>{if((select.SelectedItem as ComboBoxItem)?.Tag is not SnapshotEntry snapshot)return;var file=await OperationsSavePicker(".json","Instantané JSON");if(file!=null)await File.WriteAllTextAsync(file.Path,JsonSerializer.Serialize(snapshot),new UTF8Encoding(true));}));
        operationsBody.Children.Add(OpButton("Importer et comparer",async _=>{
            if((select.SelectedItem as ComboBoxItem)?.Tag is not SnapshotEntry local)return;
            var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".json");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file=await picker.PickSingleFileAsync();if(file==null)return;
            if(new FileInfo(file.Path).Length>10_000_000)throw new InvalidOperationException("Instantané supérieur à 10 Mo.");
            var external=JsonSerializer.Deserialize<SnapshotEntry>(await File.ReadAllTextAsync(file.Path))??throw new InvalidOperationException("Instantané invalide.");
            if(external.Inspection==null||external.Inspection.Components==null||external.Inspection.Flows==null)throw new InvalidOperationException("Instantané incomplet.");
            CompareEnvironment(local.Inspection,external.Inspection,"Instantané importé · "+external.Label);
        }));
        foreach(var line in manualEnvironmentDiff)operationsBody.Children.Add(Card(Code(ExplorerRedaction.Safe(line))));
    }
    private void CompareEnvironment(AgentInspection local,AgentInspection target,string label)
    {
        manualEnvironmentDiff.Clear();manualEnvironmentDiff.Add("Source : "+ExplorerRedaction.Safe(label));
        var left=EnvironmentFields(local);var right=EnvironmentFields(target);
        var agentUnavailable=local.Missing.Contains("agent")||target.Missing.Contains("agent");
        var componentsUnavailable=local.Missing.Contains("components")||target.Missing.Contains("components");
        var connectionsUnavailable=componentsUnavailable||local.Missing.Any(x=>x.StartsWith("connection:"))||target.Missing.Any(x=>x.StartsWith("connection:"));
        var flowsUnavailable=componentsUnavailable||local.Missing.Any(x=>x.StartsWith("flow:"))||target.Missing.Any(x=>x.StartsWith("flow:"));
        foreach(var key in left.Keys.Union(right.Keys).Order(StringComparer.Ordinal).Where(k=>!(agentUnavailable&&k.StartsWith("Agent /"))&&!(componentsUnavailable&&k.StartsWith("Composant /"))&&!(flowsUnavailable&&k.StartsWith("Flux /"))&&!(connectionsUnavailable&&k.StartsWith("Connexion /"))&&left.GetValueOrDefault(k)!=right.GetValueOrDefault(k)).Take(500))
            {
                string Clip(string value)=>value.Length<=3000?value:value[..3000]+"… [extrait tronqué]";
                manualEnvironmentDiff.Add(key+"\nLocal : "+Clip(left.GetValueOrDefault(key,"(absent)"))+"\nCible : "+Clip(right.GetValueOrDefault(key,"(absent)")));
            }
        if(agentUnavailable||componentsUnavailable||flowsUnavailable||connectionsUnavailable)manualEnvironmentDiff.Add("Comparaison partielle : sources non accessibles exclues.");
        operationsStatus.Text="Comparaison terminée : jusqu’à 500 écarts, extraits de 3 000 caractères par côté. Vérifiez les droits et la provenance des deux lectures.";
    }
    private static Dictionary<string,string> EnvironmentFields(AgentInspection inspection)
    {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        void Add(string key,string value) {if(result.ContainsKey(key))throw new InvalidOperationException("Noms de composants ambigus dans l’un des environnements.");result[key]=ExplorerRedaction.Safe(value);}
        foreach(var key in new[]{"name","schemaname","publishedon","authenticationmode","accesscontrolpolicy"})Add("Agent / "+key,ExplorerInspection.Text(inspection.Agent,key));
        foreach(var component in inspection.Components)Add("Composant / "+ExplorerInspection.Text(component,"schemaname"),ExplorerInspection.Text(component,"data"));
        foreach(var connection in inspection.Connections)Add("Connexion / "+ExplorerInspection.Text(connection,"connectionreferencelogicalname"),ExplorerInspection.Text(connection,"connectorid")+" · état "+ExplorerInspection.Text(connection,"statecode"));
        foreach(var flow in inspection.Flows)Add("Flux / "+ExplorerInspection.Text(flow,"name"),ExplorerInspection.Text(flow,"clientdata"));
        return result;
    }
}
