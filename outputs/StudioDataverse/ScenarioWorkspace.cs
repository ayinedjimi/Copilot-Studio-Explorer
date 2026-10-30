using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private string? scenarioSelectedId;
    private string scenarioFilter="",scenarioRegion="Europe";
    private bool scenarioNew,scenarioHistoryVisible;

    private void RenderScenarioWorkspace()
    {
        if(operationsStore==null||operationsAgentId==null){operationsBody.Children.Add(Label("Connectez-vous et sélectionnez un agent pour gérer ses scénarios.",14,"#687080"));return;}
        var store=operationsStore;var agent=OperationsAgent();var cases=store.Scenarios();var visible=cases.Where(x=>x.AgentId==agent.Id||x.AgentId==Guid.Empty).ToList();
        var selected=visible.FirstOrDefault(x=>x.Id==scenarioSelectedId);
        if(!scenarioNew&&selected==null){selected=visible.FirstOrDefault();scenarioSelectedId=selected?.Id;}
        var header=new Grid{ColumnSpacing=12,RowSpacing=10};header.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});header.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var name=new StackPanel{Spacing=4};var title=Label("Scénarios de test",22);title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;name.Children.Add(title);name.Children.Add(Label(agent.Name,13,"#687080"));header.Children.Add(name);
        var runAll=OpButton("Relancer les "+visible.Count,async token=>{
            if(string.IsNullOrWhiteSpace(scenarioEndpoint))throw new InvalidOperationException("Configurez le point d’accès du canal avant de lancer les scénarios.");
            var session=client.SessionGeneration;var total=visible.Count;int index=0;
            foreach(var scenario in visible){token.ThrowIfCancellationRequested();operationsStatus.Text=$"Scénario {++index}/{total} · {scenario.Title}";await RunScenarioAsync(store,scenario,token,session);}
            operationsStatus.Text=$"Exécution terminée : {total} scénario(s). Consultez les résultats et les éventuelles erreurs.";
        });runAll.IsEnabled=!operationsBusy&&visible.Count>0;Place(header,runAll,0,1);
        var create=new Button{Content="＋ Nouveau scénario",Background=Brush("#0F6CBD"),Foreground=Brush("#FFFFFF"),Padding=new Thickness(15,9,15,9)};create.Click+=(_,_)=>{scenarioNew=true;scenarioSelectedId=null;scenarioHistoryVisible=false;RenderOperations();};Place(header,create,0,2);operationsBody.Children.Add(header);
        var channel=new StackPanel{Spacing=10};var endpoint=new TextBox{Header="Point d’accès du canal mobile",Text=scenarioEndpoint,PlaceholderText="URL du canal de cet agent"};endpoint.TextChanged+=(_,_)=>scenarioEndpoint=endpoint.Text;
        var region=Options("Région","Europe","Global","Inde");region.SelectedIndex=scenarioRegion=="Global"?1:scenarioRegion=="Inde"?2:0;region.SelectionChanged+=(_,_)=>scenarioRegion=region.SelectedIndex switch{1=>"Global",2=>"Inde",_=>"Europe"};channel.Children.Add(endpoint);channel.Children.Add(region);
        channel.Children.Add(Label("Le point d’accès reste en mémoire pour la session et n’est pas enregistré.",12,"#687080"));operationsBody.Children.Add(new Expander{Header="Canal · "+scenarioRegion+(string.IsNullOrWhiteSpace(scenarioEndpoint)?" · à configurer":" · renseigné"),Content=channel,HorizontalAlignment=HorizontalAlignment.Stretch});
        var split=new Grid{ColumnSpacing=18};split.ColumnDefinitions.Add(new(){Width=new GridLength(300)});split.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var left=new StackPanel{Spacing=8};var filter=new TextBox{PlaceholderText="Filtrer les scénarios",Text=scenarioFilter};left.Children.Add(filter);
        var list=new ListView{MinHeight=360,MaxHeight=650,SelectionMode=ListViewSelectionMode.Single,HorizontalContentAlignment=HorizontalAlignment.Stretch};
        void Populate(){list.Items.Clear();foreach(var item in visible.Where(x=>x.Title.Contains(scenarioFilter,StringComparison.OrdinalIgnoreCase)||x.Question.Contains(scenarioFilter,StringComparison.OrdinalIgnoreCase))){var last=item.Runs?.LastOrDefault();var content=new StackPanel{Spacing=4};var line=Label(item.Title,14);line.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;content.Children.Add(line);content.Children.Add(Label(last==null?"Jamais lancé":last.Time.ToLocalTime().ToString("dd/MM HH:mm")+" · "+last.DurationSeconds.ToString("0.0")+" s · "+last.Status,11,"#687080"));var row=new ListViewItem{Content=content,Tag=item.Id};list.Items.Add(row);if(!scenarioNew&&item.Id==scenarioSelectedId)list.SelectedItem=row;}}
        bool updating=true;Populate();updating=false;filter.TextChanged+=(_,_)=>{scenarioFilter=filter.Text;updating=true;Populate();updating=false;};
        list.SelectionChanged+=(_,_)=>{if(updating||list.SelectedItem is not ListViewItem row||row.Tag is not string id)return;scenarioSelectedId=id;scenarioNew=false;scenarioHistoryVisible=false;RenderOperations();};left.Children.Add(list);split.Children.Add(new Border{Child=left,Background=Brush("#FAFAFC"),BorderBrush=Brush("#E2E2E8"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(12)});
        var detail=new StackPanel{Spacing=14};Place(split,detail,0,1);operationsBody.Children.Add(split);
        if(selected==null&&!scenarioNew){detail.Children.Add(Label("Sélectionnez ou créez un scénario",20));detail.Children.Add(Label("Les scénarios sont enregistrés localement pour ce compte et cet agent.",13,"#687080"));return;}
        var draft=selected??new ScenarioCase(Guid.NewGuid().ToString("N"),"","","","",agent.Id);
        var state=AdvancedFunctions.ScenarioStatus(draft);var lastRun=draft.Runs?.LastOrDefault();if(lastRun?.Status=="Erreur")state="Erreur d’exécution";
        var top=new StackPanel{Orientation=Orientation.Horizontal,Spacing=12};var heading=Label(scenarioNew?"Nouveau scénario":draft.Title,20);heading.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;top.Children.Add(heading);top.Children.Add(Badge(state));detail.Children.Add(top);
        var scenarioTitle=new TextBox{Header="Nom du scénario",Text=draft.Title,PlaceholderText="Ex. Recherche d’un document"};detail.Children.Add(scenarioTitle);
        var fields=new Grid{ColumnSpacing=12};fields.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});fields.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var question=new TextBox{Header="Question",Text=draft.Question,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=96};var expected=new TextBox{Header="Réponse attendue",Text=draft.Expected,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=96};fields.Children.Add(question);Place(fields,expected,0,1);detail.Children.Add(fields);
        var verification=Options("Vérification","Contient","Jugement manuel");verification.SelectedIndex=draft.Verification=="Manuel"?1:0;var match=new TextBox{Header="Texte à rechercher dans la réponse",Text=draft.MatchText,PlaceholderText="Ex. périmètre"};match.Visibility=verification.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;verification.SelectionChanged+=(_,_)=>match.Visibility=verification.SelectedIndex==0?Visibility.Visible:Visibility.Collapsed;detail.Children.Add(verification);detail.Children.Add(match);
        detail.Children.Add(new Border{Background=Brush("#FFF1D6"),CornerRadius=new CornerRadius(8),Padding=new Thickness(14),Child=Label("Un test réel peut déclencher les actions de l’agent. Utilisez un environnement de formation.",13,"#805000")});
        ScenarioCase Edited(){if(string.IsNullOrWhiteSpace(scenarioTitle.Text)||string.IsNullOrWhiteSpace(question.Text))throw new InvalidOperationException("Renseignez le nom et la question.");if(verification.SelectedIndex==0&&string.IsNullOrWhiteSpace(match.Text)&&string.IsNullOrWhiteSpace(expected.Text))throw new InvalidOperationException("Renseignez le texte à rechercher, ou choisissez un jugement manuel.");return draft with{Title=scenarioTitle.Text.Trim(),Question=question.Text.Trim(),Expected=expected.Text.Trim(),MatchText=match.Text.Trim(),Verification=verification.SelectedIndex==1?"Manuel":"Contient",AgentId=agent.Id};}
        async Task Save(ScenarioCase item){var all=store.Scenarios();all.RemoveAll(x=>x.Id==item.Id);all.Add(item);await store.SaveScenarios(all);scenarioSelectedId=item.Id;scenarioNew=false;}
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
        actions.Children.Add(OpButton("Lancer",async token=>{var item=Edited();await Save(item);var session=client.SessionGeneration;await RunScenarioAsync(store,item,token,session);operationsStatus.Text="Exécution terminée ; relisez la réponse et son contexte.";}));
        actions.Children.Add(OpButton("Enregistrer",async _=>{await Save(Edited());operationsStatus.Text="Scénario enregistré localement.";}));
        if(!scenarioNew)actions.Children.Add(OpButton("Supprimer",async _=>{await store.SaveScenarios(store.Scenarios().Where(x=>x.Id!=draft.Id).ToList());scenarioSelectedId=null;scenarioNew=false;operationsStatus.Text="Scénario local supprimé.";}));detail.Children.Add(actions);
        if(!scenarioNew){
            var resultHeader=new Grid();resultHeader.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});resultHeader.ColumnDefinitions.Add(new(){Width=GridLength.Auto});resultHeader.Children.Add(Label("Dernier résultat",17));var history=new Button{Content=(draft.Runs?.Count??0)+" exécution(s) · Historique"};history.Click+=(_,_)=>{scenarioHistoryVisible=!scenarioHistoryVisible;RenderOperations();};Place(resultHeader,history,0,1);detail.Children.Add(resultHeader);
            if(lastRun==null&&string.IsNullOrWhiteSpace(draft.Observed))detail.Children.Add(Label("Aucune exécution enregistrée.",13,"#687080"));
            else {if(lastRun!=null)detail.Children.Add(Label(lastRun.Time.ToLocalTime().ToString("g")+" · "+lastRun.DurationSeconds.ToString("0.0")+" s · "+lastRun.Status,12,"#687080"));detail.Children.Add(Card(Label(lastRun?.Status=="Erreur"?lastRun.Error:draft.Observed,14)));}
            if(scenarioHistoryVisible)foreach(var entry in (draft.Runs??[]).AsEnumerable().Reverse().Take(50))detail.Children.Add(Card(Label(entry.Time.ToLocalTime().ToString("g")+" · "+entry.DurationSeconds.ToString("0.0")+" s · "+entry.Status+"\n"+(entry.Status=="Erreur"?entry.Error:entry.Response),12)));
            detail.Children.Add(OpButton("Ouvrir les transcriptions de cet agent",_=>{ShowTranscripts(agent.Id);return Task.CompletedTask;}));
        }
    }
    private async Task RunScenarioAsync(ExplorerLocalStore store,ScenarioCase scenario,CancellationToken token,int session)
    {
        var timer=Stopwatch.StartNew();string response="",error="",status;
        try{using var runner=new ScenarioRunner();response=await runner.RunAsync(scenarioEndpoint,scenarioRegion,scenario.Question,token);status=AdvancedFunctions.ScenarioStatus(scenario with{Observed=response});}
        catch(OperationCanceledException)when(token.IsCancellationRequested){throw;}
        catch(Exception ex){status="Erreur";error=ExplorerRedaction.Safe(DescribeError(ex));App.RecordError(ex);}
        timer.Stop();token.ThrowIfCancellationRequested();if(session!=client.SessionGeneration)return;
        var all=store.Scenarios();var current=all.FirstOrDefault(x=>x.Id==scenario.Id);if(current==null)return;
        var runs=(current.Runs??[]).ToList();runs.Add(new(DateTimeOffset.Now,timer.Elapsed.TotalSeconds,response,status,error));
        var updated=current with{Observed=response,Runs=runs};all.RemoveAll(x=>x.Id==current.Id);all.Add(updated);await store.SaveScenarios(all);
    }
}
