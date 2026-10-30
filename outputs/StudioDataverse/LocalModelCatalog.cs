using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Text.Json;

namespace StudioDataverse;
public sealed record LocalModelOption(string Path,string Name,string Format);
public sealed partial class MainWindow
{
    private ComboBox? chatModelSelector;
    private List<LocalModelOption> InstalledModels()
    {
        var paths=new[]{Path.Combine(AppContext.BaseDirectory,"models"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CopilotStudioExplorer","Models")};
        var result=new List<LocalModelOption>();
        foreach(var directory in paths.Distinct(StringComparer.OrdinalIgnoreCase))try{
            if(!Directory.Exists(directory))continue;
            var catalogPath=Path.Combine(directory,"formats.json");var formats=File.Exists(catalogPath)&&new FileInfo(catalogPath).Length<100_000?JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(catalogPath))??[]:[];
            foreach(var path in Directory.EnumerateFiles(directory,"*.gguf",SearchOption.TopDirectoryOnly)){
                var name=Path.GetFileName(path);if(name.StartsWith("mmproj",StringComparison.OrdinalIgnoreCase))continue;
                if(formats.TryGetValue(name,out var format)&&format is "ChatML" or "Gemma 2" or "Gemma 4")result.Add(new(path,name+" · "+format,format));
                else if(name.Contains("gemma-4",StringComparison.OrdinalIgnoreCase)||name.Contains("gemma4",StringComparison.OrdinalIgnoreCase))result.Add(new(path,"Gemma 4 E2B · "+name,"Gemma 4"));
                else if(name.Contains("gemma-2",StringComparison.OrdinalIgnoreCase)||name.Contains("gemma2",StringComparison.OrdinalIgnoreCase))result.Add(new(path,"Gemma 2 · "+name,"Gemma 2"));
                else if(name.Equals("model.gguf",StringComparison.OrdinalIgnoreCase)||name.Contains("qwen",StringComparison.OrdinalIgnoreCase))result.Add(new(path,name.Equals("model.gguf",StringComparison.OrdinalIgnoreCase)?"Modèle léger fourni · 0,5 B":"Modèle compatible ChatML · "+name,"ChatML"));
            }
        }catch(IOException){}catch(UnauthorizedAccessException){}catch(JsonException){}
        return result.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    private ComboBox BuildModelSelector(bool forChat=false)
    {
        var selector=new ComboBox{Header="Modèle local",MinWidth=220,MaxWidth=430,HorizontalAlignment=HorizontalAlignment.Left};
        PopulateModelSelector(selector);selector.SelectionChanged+=(_,_)=>{if(selector.SelectedItem is ComboBoxItem {Tag:LocalModelOption model}){questionModelPath=model.Path;questionModelFormat=model.Format;if(forChat)chatStatus.Text="Modèle local sélectionné : "+model.Name;}};
        if(forChat)chatModelSelector=selector;
        return selector;
    }
    private void PopulateModelSelector(ComboBox selector)
    {
        var models=InstalledModels();selector.Items.Clear();foreach(var model in models)selector.Items.Add(new ComboBoxItem{Content=model.Name,Tag=model});
        foreach(ComboBoxItem item in selector.Items)if(item.Tag is LocalModelOption model&&string.Equals(model.Path,questionModelPath,StringComparison.OrdinalIgnoreCase)){selector.SelectedItem=item;break;}
        if(selector.SelectedItem==null&&selector.Items.Count>0)selector.SelectedIndex=0;
        if(selector.SelectedItem is ComboBoxItem {Tag:LocalModelOption selected}){questionModelPath=selected.Path;questionModelFormat=selected.Format;}
    }
}
