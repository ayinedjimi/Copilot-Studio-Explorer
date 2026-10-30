using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private void BuildHomeDashboard(){RenderHomeDashboard();}
    private void OpenExplorerAgent(Agent agent)
    {
        if(operationsAgentId!=agent.Id){generatedQuestions="";localAnalysis=localSecurity="";scenarioSelectedId=null;scenarioNew=false;scenarioEndpoint="";}explorerAgentId=explorerExpandedAgent=operationsAgentId=agent.Id;explorerGroups.Add(agent.PublishedOn.HasValue?"Publiés":"Non publiés");explorerTab=0;ShowOperations("Explorateur");
        if(!inspections.ContainsKey(agent.Id))_ = LoadExplorerAgentAsync(agent.Id);
    }
    private void RenderHomeDashboard()
    {
        homePanel.Children.Clear();var heading=Label("Bonjour, prêt à explorer ?",30);heading.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;homePanel.Children.Add(heading);
        homePanel.Children.Add(Label("Votre environnement · données chargées dans cette session",14,"#687080"));
        var refresh=new Button{Content="Actualiser les agents",Background=Brush("#6357D5"),Foreground=Brush("#FFFFFF"),Padding=new Thickness(14,9,14,9)};refresh.Click+=async (_,_)=>{await Run(LoadAgents);RenderHomeDashboard();};homePanel.Children.Add(refresh);
        var metrics=new Grid{ColumnSpacing=12};for(int i=0;i<4;i++)metrics.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        var values=new[]{("Agents chargés",allAgents.Count.ToString()),("Publiés",allAgents.Count(x=>x.PublishedOn.HasValue).ToString()),("Analysés",inspections.Count.ToString()),("Incidents observés",incidentsByAgent.Values.Sum(x=>x.Count).ToString())};
        for(int i=0;i<values.Length;i++){var column=new StackPanel{Spacing=8};column.Children.Add(Label(values[i].Item1,12,"#687080"));column.Children.Add(Label(values[i].Item2,26,"#342B80"));Place(metrics,Card(column),0,i);}homePanel.Children.Add(metrics);
        homePanel.Children.Add(Label("Favoris",19));var favs=allAgents.Where(a=>preferences.Favorites.Contains(a.Id)).OrderBy(a=>a.Name).Take(5).ToList();
        if(favs.Count==0)homePanel.Children.Add(Label("Aucun favori chargé. Ajoutez-en depuis la fiche d’un agent.",13,"#687080"));
        foreach(var agent in favs){var b=new Button{Content=agent.Name+"  →",HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>OpenExplorerAgent(agent);homePanel.Children.Add(b);}
        homePanel.Children.Add(Label("Agents modifiés récemment",19));var recent=allAgents.Where(a=>a.ModifiedOn.HasValue).OrderByDescending(a=>a.ModifiedOn).Take(5).ToList();
        if(recent.Count==0)homePanel.Children.Add(Label("Aucune date de modification disponible dans les agents chargés.",13,"#687080"));
        foreach(var agent in recent){var b=new Button{Content=agent.Name+" · "+agent.ModifiedLabel,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>OpenExplorerAgent(agent);homePanel.Children.Add(b);}
        homePanel.Children.Add(Label("Dernières transcriptions chargées",19));
        if(transcriptRecords.Count==0)homePanel.Children.Add(Label("Aucune transcription chargée dans cette session.",13,"#687080"));
        foreach(var record in transcriptRecords.OrderByDescending(x=>x.Start).Take(3)){var b=new Button{Content=record.AgentName+" · "+record.StartLabel,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>ShowTranscripts(record.BotId);homePanel.Children.Add(b);}
        homePanel.Children.Add(Label("Derniers incidents chargés",19));var incidents=incidentsByAgent.Values.SelectMany(x=>x).OrderByDescending(x=>x.Time).Take(3).ToList();
        if(incidents.Count==0)homePanel.Children.Add(Label(incidentsByAgent.Count==0?"Incidents non chargés dans cette session.":"Aucun incident dans les échantillons chargés.",13,"#687080"));
        foreach(var item in incidents){var b=new Button{Content=item.AgentName+" · "+item.Type,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left};b.Click+=(_,_)=>{operationsAgentId=item.AgentId;ShowOperations("Incidents");};homePanel.Children.Add(b);}
    }
}
