using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private void AddAuditRuleImport()
    {
        var panel=new StackPanel{Spacing=10};
        panel.Children.Add(Label("Importez une liste JSON de règles textuelles locales. Elles sont évaluées sur les données accessibles et restent séparées des contrôles standard.",13,"#687080"));
        if(operationsStore==null){panel.Children.Add(Label("Connectez-vous pour importer des règles.",13));operationsBody.Children.Add(new Expander{Header="Règles personnalisées",Content=panel});return;}
        var store=operationsStore;
        panel.Children.Add(OpButton("Importer des règles JSON",async token=>{
            var picker=new FileOpenPicker();picker.FileTypeFilter.Add(".json");WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));
            var file=await picker.PickSingleFileAsync();token.ThrowIfCancellationRequested();if(file==null)return;if(new FileInfo(file.Path).Length>1_000_000)throw new InvalidOperationException("Le fichier de règles dépasse 1 Mo.");
            var incoming=AdvancedFunctions.ParseImportedRules(await File.ReadAllTextAsync(file.Path));var existing=store.Rules();var added=0;
            foreach(var rule in incoming)if(existing.All(r=>!r.Name.Equals(rule.Name,StringComparison.OrdinalIgnoreCase))){existing.Add(rule);added++;}
            token.ThrowIfCancellationRequested();await store.SaveRules(existing);operationsStatus.Text=$"{added} règle(s) importée(s) ; {incoming.Count-added} nom(s) déjà présent(s) conservé(s).";
        }));
        panel.Children.Add(OpButton("Télécharger un exemple JSON",async _=>{
            var file=await OperationsSavePicker(".json","Règles JSON");if(file==null)return;
            var sample=new[]{new{Name="Nom avec préfixe",Field="Nom",Operation="Commence par",Value="Agent"}};
            await File.WriteAllTextAsync(file.Path,JsonSerializer.Serialize(sample,new JsonSerializerOptions{WriteIndented=true}),new UTF8Encoding(true));
        }));
        var rules=store.Rules();var results=AdvancedFunctions.EvaluateRules(rules,OperationsScope());
        panel.Children.Add(Label($"{rules.Count} règle(s) locale(s) · {results.Count} résultat(s) pour le périmètre analysé.",12,"#687080"));
        foreach(var result in results.Take(150))panel.Children.Add(Label(result.Agent+" · "+result.Rule.Name+" · "+FindingLabel(result.State)+" · "+result.Evidence,12,FindingColor(result.State)));
        if(results.Count>150)panel.Children.Add(Label("150 premiers résultats affichés.",12,"#687080"));
        operationsBody.Children.Add(new Expander{Header="Règles personnalisées · "+rules.Count,Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch});
    }
}
