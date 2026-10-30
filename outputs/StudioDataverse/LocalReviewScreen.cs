using System.Text;
using Microsoft.UI.Xaml.Controls;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private string localAnalysis="",localSecurity="";
    private void RenderLocalReview(bool security)
    {
        var intro=new StackPanel{Spacing=8};var title=Label(security?"Sécurité IA":"Analyse IA",22,"#27203D");title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;intro.Children.Add(title);
        intro.Children.Add(Label(security?"Obtenez une lecture assistée des constats de sécurité déjà collectés pour l’agent sélectionné.":"Obtenez une synthèse des composants et liens accessibles de l’agent sélectionné.",13,"#687080"));
        intro.Children.Add(Label("Génération locale · données analysées uniquement · résultat à vérifier",12,"#6550B5"));operationsBody.Children.Add(Card(intro,"#F7F5FF"));
        var controls=new StackPanel{Spacing=12};controls.Children.Add(BuildModelSelector());
        controls.Children.Add(OpButton(security?"Générer la synthèse de sécurité":"Analyser avec le modèle local",async token=>{
            var agent=OperationsAgent();if(!inspections.TryGetValue(agent.Id,out var inspection))throw new InvalidOperationException("Analysez d’abord cet agent avec le bouton de la barre supérieure.");
            var details=new List<string>{"Agent : "+inspection.Name,"Sources non accessibles : "+string.Join(", ",inspection.Missing)};
            if(security)details.AddRange(inspection.Findings.Select(f=>f.Id+" · "+FindingLabel(f.State)+" · "+f.Title+" · "+f.Evidence+" · "+f.Recommendation));
            else {details.AddRange(inspection.Components.Take(30).Select(c=>"Composant : "+ExplorerInspection.Text(c,"name")+" · type "+ExplorerInspection.Text(c,"componenttype")));details.AddRange(inspection.Nodes.Take(30).Select(n=>n.Kind+" : "+n.Label+" · "+n.State));}
            var context=ExplorerRedaction.Safe(string.Join("\n",details));if(context.Length>7000)context=context[..7000]+"\n[Contexte tronqué : analyse partielle.]";
            var question=security?"Résume les risques observés, les sources non vérifiables et les vérifications à effectuer. Ne déduis jamais qu’un contrôle manquant est conforme.":"Résume le rôle probable de cet agent, ses composants et dépendances accessibles. Distingue clairement ce qui est observé de ce qui reste inconnu.";
            operationsStatus.Text="Synthèse locale en cours…";var answer=await LocalQuestionEngine.ChatAsync(questionModelPath,[("user",question)],context,token,questionModelFormat,EngineOptions());
            if(security)localSecurity=answer;else localAnalysis=answer;operationsStatus.Text="Synthèse produite localement ; vérifiez chaque proposition.";
        }));operationsBody.Children.Add(Card(controls));
        var result=security?localSecurity:localAnalysis;
        operationsBody.Children.Add(Label("Résultat",18));
        var text=new TextBox{Text=result,IsReadOnly=true,AcceptsReturn=true,TextWrapping=Microsoft.UI.Xaml.TextWrapping.Wrap,MinHeight=220,PlaceholderText="La synthèse apparaîtra ici après génération."};operationsBody.Children.Add(text);
        var actions=new StackPanel{Orientation=Microsoft.UI.Xaml.Controls.Orientation.Horizontal,Spacing=10};actions.Children.Add(OpButton("Copier",_=>{CopyText(result);return Task.CompletedTask;}));actions.Children.Add(OpButton("Exporter en texte",async _=>{var file=await OperationsSavePicker(".txt","Synthèse");if(file!=null)await File.WriteAllTextAsync(file.Path,result,new UTF8Encoding(true));}));operationsBody.Children.Add(actions);
        operationsBody.Children.Add(Label("La synthèse peut contenir des erreurs et ne remplace pas les preuves ni les contrôles de sécurité. Elle ne modifie aucune donnée Dataverse.",12,"#687080"));
    }
}
