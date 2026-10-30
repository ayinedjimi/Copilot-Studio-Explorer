using System.Diagnostics;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private UIElement BuildModelDownloadPanel()
    {
        var panel=new StackPanel{Spacing=10};
        panel.Children.Add(Label("Téléchargez un fichier GGUF public à partir de Hugging Face. Vérifiez la licence et choisissez son format de conversation. Les modèles privés ou soumis à autorisation ne sont pas pris en charge.",12,"#687080"));
        var repo=new TextBox{Header="Dépôt (auteur/nom)",Text="unsloth/gemma-4-E2B-it-GGUF"};var file=new TextBox{Header="Fichier GGUF à la racine du dépôt",Text="gemma-4-E2B-it-Q4_K_M.gguf"};
        var format=Options("Format du modèle","Gemma 4","Gemma 2","ChatML");panel.Children.Add(repo);panel.Children.Add(file);panel.Children.Add(format);
        var note=Label("Limite : 8 Go par fichier. Une empreinte SHA-256 est exigée ; le modèle est vérifié avant son ajout.",12,"#687080");panel.Children.Add(note);
        var progress=new ProgressBar{Minimum=0,Maximum=100,Height=6,Visibility=Visibility.Collapsed};panel.Children.Add(progress);
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};var download=new Button{Content="Télécharger depuis Hugging Face",Background=Brush("#6357D5"),Foreground=Brush("#FFFFFF")};var stop=new Button{Content="Annuler",IsEnabled=false};
        stop.Click+=(_,_)=>modelDownloadCancellation?.Cancel();download.Click+=async (_,_)=>{
            if(modelDownloadCancellation!=null)return;var source=modelDownloadCancellation=new CancellationTokenSource();download.IsEnabled=false;stop.IsEnabled=true;progress.Visibility=Visibility.Visible;progress.Value=0;
            try{
                using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
                note.Text="Lecture des métadonnées publiques…";var selectedFormat=((ComboBoxItem)format.SelectedItem).Content.ToString()!;
                var metadata=await HuggingFaceDownloader.GetMetadataAsync(http,repo.Text.Trim(),file.Text.Trim(),source.Token);
                note.Text=$"Téléchargement · {metadata.Size/1_000_000_000d:0.00} Go · empreinte vérifiée à la fin.";
                var timer=Stopwatch.StartNew();var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CopilotStudioExplorer","Models");
                var path=await HuggingFaceDownloader.DownloadAsync(http,metadata,folder,total=>{if(timer.ElapsedMilliseconds<250&&total<metadata.Size)return;timer.Restart();DispatcherQueue.TryEnqueue(()=>{progress.Value=100d*total/metadata.Size;});},source.Token);
                var manifest=Path.Combine(folder,"formats.json");Dictionary<string,string> formats=[];if(File.Exists(manifest)&&new FileInfo(manifest).Length<100_000)formats=JsonSerializer.Deserialize<Dictionary<string,string>>(await File.ReadAllTextAsync(manifest,source.Token))??[];
                formats[metadata.FileName]=selectedFormat;var temp=manifest+"."+Guid.NewGuid().ToString("N")+".tmp";
                try{await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(formats),source.Token);File.Move(temp,manifest,true);}finally{if(File.Exists(temp))File.Delete(temp);}
                questionModelPath=path;questionModelFormat=selectedFormat;if(chatModelSelector!=null)PopulateModelSelector(chatModelSelector);
                note.Text="Modèle téléchargé, vérifié et sélectionné. Son format doit correspondre au fichier choisi.";chatStatus.Text="Modèle local prêt.";
            }catch(OperationCanceledException){note.Text="Téléchargement annulé.";}
            catch(Exception ex){note.Text="Téléchargement impossible : "+ExplorerRedaction.Safe(DescribeError(ex));await App.ShowErrorAsync(this,App.RecordError(ex));}
            finally{if(modelDownloadCancellation==source)modelDownloadCancellation=null;source.Dispose();download.IsEnabled=true;stop.IsEnabled=false;progress.Visibility=Visibility.Collapsed;}
        };actions.Children.Add(download);actions.Children.Add(stop);panel.Children.Add(actions);
        return new Expander{Header="Télécharger un modèle depuis Hugging Face",Content=panel,HorizontalAlignment=HorizontalAlignment.Stretch};
    }
}
