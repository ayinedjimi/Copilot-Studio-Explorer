using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private string questionModelPath=DefaultLocalModel(),questionModelFormat="ChatML",generatedQuestions="";
    private static string DefaultLocalModel(){var gemma4=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CopilotStudioExplorer","Models","gemma-4-E2B-it-Q4_K_M.gguf");if(File.Exists(gemma4))return gemma4;var gemma2=Path.Combine(AppContext.BaseDirectory,"models","gemma-2-2b-it-Q4_K_M.gguf");return File.Exists(gemma2)?gemma2:Path.Combine(AppContext.BaseDirectory,"models","model.gguf");}
    private int questionCount=5;
    private bool questionGenerationEnabled;
    private void RenderQuestionGenerator()
    {
        var introduction=new StackPanel{Spacing=6};var title=Label("Créer un jeu de questions",22,"#24243A");title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;introduction.Children.Add(title);
        introduction.Children.Add(Label("Générez localement des questions à partir des informations accessibles de l’agent sélectionné, puis relisez et modifiez le résultat.",13,"#687080"));operationsBody.Children.Add(Card(introduction,"#F7F6FF"));
        var config=new StackPanel{Spacing=12};
        var enabled=new ToggleSwitch{Header="Génération locale",OnContent="Activée",OffContent="Désactivée",IsOn=questionGenerationEnabled};config.Children.Add(enabled);config.Children.Add(BuildModelSelector());
        var count=Options("Nombre de questions","3","5","10");count.SelectedIndex=questionCount==3?0:questionCount==10?2:1;count.SelectionChanged+=(_,_)=>questionCount=count.SelectedIndex switch{0=>3,2=>10,_=>5};config.Children.Add(count);
        config.Children.Add(Label("Choisissez un modèle installé ; les données utilisées restent sur ce poste.",12,"#687080"));
        var generate=OpButton("Générer les questions",async token=>{
            if(!questionGenerationEnabled)throw new InvalidOperationException("Activez la génération locale avant de continuer.");
            var agent=OperationsAgent();if(!inspections.TryGetValue(agent.Id,out var inspection))throw new InvalidOperationException("Analysez d’abord l’agent sélectionné.");
            var parts=new List<string>{"Nom : "+agent.Name,"Schéma : "+agent.Schema};
            foreach(var component in inspection.Components.Take(30)){var name=ExplorerInspection.Text(component,"name");var type=ExplorerInspection.Text(component,"componenttype");var data=ExplorerInspection.Text(component,"data");if(data.Length>600)data=data[..600];parts.Add("Composant : "+name+" · type "+type+" · "+data);}
            var context=ExplorerRedaction.Safe(string.Join("\n",parts));if(context.Length>8000)context=context[..8000];
            operationsStatus.Text="Génération locale en cours…";generatedQuestions=await LocalQuestionEngine.GenerateAsync(questionModelPath,context,questionCount,token,questionModelFormat,EngineOptions());operationsStatus.Text="Questions générées localement. Vérifiez leur pertinence avant utilisation.";
        });generate.IsEnabled=questionGenerationEnabled&&!operationsBusy;enabled.Toggled+=(_,_)=>{questionGenerationEnabled=enabled.IsOn;generate.IsEnabled=enabled.IsOn&&!operationsBusy;};config.Children.Add(generate);operationsBody.Children.Add(Card(config));
        var result=new TextBox{Header="Jeu de questions",Text=generatedQuestions,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=220};result.TextChanged+=(_,_)=>generatedQuestions=result.Text;operationsBody.Children.Add(result);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};actions.Children.Add(OpButton("Copier",_=>{CopyText(generatedQuestions);operationsStatus.Text="Questions copiées.";return Task.CompletedTask;}));actions.Children.Add(OpButton("Exporter en texte",async _=>{var file=await OperationsSavePicker(".txt","Questions");if(file!=null)await File.WriteAllTextAsync(file.Path,generatedQuestions,new UTF8Encoding(true));}));operationsBody.Children.Add(actions);
        operationsBody.Children.Add(Label("Ces propositions ne sont pas des résultats de test et peuvent contenir des erreurs. L’annulation utilise le bouton Annuler de la barre d’actions.",12,"#687080"));
    }
}
