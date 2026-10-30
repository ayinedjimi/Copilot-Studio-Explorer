using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Storage.Pickers;
namespace StudioDataverse;
public sealed partial class MainWindow
{
    private readonly WebView2 dependencyWeb=new(){Height=700,HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly TextBlock dependencyStatus=Label("Carte locale React Flow.",12,"#687080");
    private readonly StackPanel dependencyDetail=new(){Spacing=8};
    private bool dependencyReady,dependencyLoading;
    private readonly WebView2 reportWeb=new(){Height=700,HorizontalAlignment=HorizontalAlignment.Stretch};
    private string loadedReport="";
    private TaskCompletionSource<bool>? reportNavigation;
    private bool reportLoading;
    private TaskCompletionSource<bool>? reportSetup;
    private ulong reportNavigationId;
    private async Task<CoreWebView2Environment> OperationsWebEnvironment()=>await CoreWebView2Environment.CreateWithOptionsAsync(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CopilotStudioExplorer","WebView2"),new CoreWebView2EnvironmentOptions());
    private void RenderDependencies()
    {
        var scope=OperationsScope();var nodes=scope.SelectMany(x=>x.Nodes).DistinctBy(x=>x.Id).ToList();var edges=scope.SelectMany(x=>x.Edges).DistinctBy(x=>x.Id).ToList();
        Metrics(("Objets",nodes.Count.ToString(),"#6357D5"),("Liens observés",edges.Count.ToString(),"#0078D4"),("Sources inaccessibles",scope.Sum(x=>x.Missing.Count).ToString(),"#85838B"));
        operationsBody.Children.Add(Label("Liens extraits des références effectivement déclarées. Une connexion ou un flux non accessible reste visible comme référence non vérifiable. Sélectionnez un objet pour retrouver les agents susceptibles d’être impactés.",13,"#536277"));
        dependencyDetail.Children.Clear();if(dependencyWeb.Parent is Border oldDependencyHost)oldDependencyHost.Child=null;
        operationsBody.Children.Add(dependencyStatus);operationsBody.Children.Add(new Border{Child=dependencyWeb,CornerRadius=new CornerRadius(8),BorderBrush=Brush("#DFE2EA"),BorderThickness=new Thickness(1)});
        operationsBody.Children.Add(dependencyDetail);
        operationsBody.Children.Add(OpButton("Recharger la carte",async _=>{if(dependencyWeb.CoreWebView2!=null)dependencyWeb.CoreWebView2.Reload();else await EnsureDependencyWeb();}));
        _=EnsureDependencyWeb();SendDependencies();
    }
    private async Task EnsureDependencyWeb()
    {
        if(!dependencyWeb.IsLoaded||dependencyReady||dependencyLoading)return;dependencyLoading=true;
        try {
            await dependencyWeb.EnsureCoreWebView2Async(await OperationsWebEnvironment());
            var core=dependencyWeb.CoreWebView2;
            if(core.Source.Length>0&&core.Source.Contains("/dependencies.html")){dependencyReady=true;SendDependencies();return;}
            core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;
            core.SetVirtualHostNameToFolderMapping("agentmap.local",Path.Combine(AppContext.BaseDirectory,"Assets","AgentMap"),CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting+=(_,args)=>{if(!IsMapOrigin(args.Uri)){args.Cancel=true;return;}dependencyReady=false;};
            core.NewWindowRequested+=(_,args)=>args.Handled=true;
            core.WebMessageReceived+=(_,args)=> {
                if(!IsMapOrigin(args.Source))return;
                try {
                    using var doc=JsonDocument.Parse(args.WebMessageAsJson);var action=doc.RootElement.GetProperty("action").GetString();
                    if(action=="ready"){dependencyReady=true;SendDependencies();return;}
                    if(!authenticated||!doc.RootElement.TryGetProperty("id",out var nodeId)||nodeId.ValueKind!=JsonValueKind.String)return;
                    var id=nodeId.GetString()??"";var scope=OperationsScope();var nodes=scope.SelectMany(x=>x.Nodes).DistinctBy(x=>x.Id).ToList();var node=nodes.FirstOrDefault(x=>x.Id==id);if(node==null)return;
                    dependencyDetail.Children.Clear();dependencyDetail.Children.Add(Label(node.Label,20));dependencyDetail.Children.Add(Label(node.Kind+" · "+node.State+" · "+node.Detail,13));
                    var impacted=ExplorerInspection.UpstreamAgents(nodes,scope.SelectMany(x=>x.Edges),id);
                    dependencyDetail.Children.Add(Label("Agents potentiellement impactés : "+string.Join(", ",nodes.Where(x=>impacted.Contains(x.Id)).Select(x=>x.Label)),14,"#6357D5"));
                }catch {dependencyStatus.Text="Message de sélection non reconnu.";}
            };
            core.Navigate(MapOrigin+"/dependencies.html");
        }catch {dependencyStatus.Text="La carte nécessite Microsoft Edge WebView2 Runtime. Les dépendances restent disponibles dans les rapports.";}
        finally{dependencyLoading=false;}
    }
    private void SendDependencies()
    {
        if(!dependencyReady||dependencyWeb.CoreWebView2==null)return;
        var scope=OperationsScope();
        dependencyWeb.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type="dependencies",nodes=scope.SelectMany(x=>x.Nodes).DistinctBy(x=>x.Id).Select(x=>new{id=x.Id,label=x.Label,kind=x.Kind,state=x.State}),edges=scope.SelectMany(x=>x.Edges).DistinctBy(x=>x.Id).Select(x=>new{id=x.Id,source=x.Source,target=x.Target,label=x.Label})}));
        dependencyStatus.Text="React Flow · zoom et déplacement · liens déclarés · aucun secret ni jeton transmis à la carte locale.";
    }
    private void RenderReports()
    {
        var scope=OperationsScope();var ids=scope.Select(x=>x.AgentId).ToHashSet();
        var incidents=incidentsByAgent.Where(x=>ids.Contains(x.Key)).SelectMany(x=>x.Value).ToList();var notes=(operationsStore?.Notes()??[]).Where(x=>ids.Contains(x.AgentId)).ToList();
        operationsBody.Children.Add(Label("Rapport du périmètre sélectionné : "+scope.Count+" agent(s) analysé(s), "+incidents.Count+" incident(s) observé(s). Les agents non analysés ne sont pas inclus.",14));
        if(scope.Count==0){operationsBody.Children.Add(Label("Lancez une analyse avant de produire un rapport.",16));return;}
        reportHtml=ExplorerReports.Html(scope,incidents,notes,currentDifferences);var csv=ExplorerReports.Csv(scope,incidents,notes,currentDifferences);
        var toolbar=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
        toolbar.Children.Add(OpButton("Exporter HTML",async _=>{var file=await OperationsSavePicker(".html","Rapport HTML");if(file!=null)await File.WriteAllTextAsync(file.Path,reportHtml,new UTF8Encoding(true));}));
        toolbar.Children.Add(OpButton("Exporter CSV",async _=>{var file=await OperationsSavePicker(".csv","CSV");if(file!=null)await File.WriteAllTextAsync(file.Path,csv,new UTF8Encoding(true));}));
        toolbar.Children.Add(OpButton("Exporter PDF",async token=>{
            await EnsureReportPreview();if(reportNavigation==null||!await reportNavigation.Task.WaitAsync(TimeSpan.FromSeconds(30),token))throw new InvalidOperationException("L’aperçu PDF n’est pas prêt ; vérifiez WebView2 Runtime.");
            var file=await OperationsSavePicker(".pdf","Rapport PDF");if(file==null)return;token.ThrowIfCancellationRequested();
            if(reportWeb.CoreWebView2==null||!await reportWeb.CoreWebView2.PrintToPdfAsync(file.Path,null))throw new InvalidOperationException("Le PDF n’a pas pu être enregistré.");
            operationsStatus.Text="Rapport PDF enregistré.";
        }));
        if(reportWeb.Parent is Border oldReportHost)oldReportHost.Child=null;
        operationsBody.Children.Add(toolbar);operationsBody.Children.Add(new Border{Child=reportWeb,BorderBrush=Brush("#DFE2EA"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8)});
        _=EnsureReportPreview();
    }
    private async Task<Windows.Storage.StorageFile?> OperationsSavePicker(string extension,string label)
    {
        var picker=new FileSavePicker{SuggestedFileName="Copilot-Studio-Explorer-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")};
        picker.FileTypeChoices.Add(label,new List<string>{extension});WinRT.Interop.InitializeWithWindow.Initialize(picker,WinRT.Interop.WindowNative.GetWindowHandle(this));return await picker.PickSaveFileAsync();
    }
    private async Task EnsureReportPreview()
    {
        if(!reportWeb.IsLoaded)return;
        if(reportLoading){if(reportSetup!=null)try{await reportSetup.Task.WaitAsync(TimeSpan.FromSeconds(30));}catch{}return;}reportLoading=true;reportSetup=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var session=client.SessionGeneration;
        try {
            if(reportWeb.CoreWebView2==null) {
                await reportWeb.EnsureCoreWebView2Async(await OperationsWebEnvironment());
                var initialized=reportWeb.CoreWebView2??throw new InvalidOperationException("WebView2 indisponible.");
                initialized.Settings.AreDevToolsEnabled=false;initialized.Settings.AreDefaultContextMenusEnabled=false;initialized.Settings.AreDefaultScriptDialogsEnabled=false;initialized.Settings.IsScriptEnabled=false;initialized.Settings.IsStatusBarEnabled=false;
                initialized.NavigationStarting+=(_,args)=> {
                    if(args.Uri=="about:blank")return;
                    if(!Uri.TryCreate(args.Uri,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="explorer-report.local"||!uri.IsDefaultPort){args.Cancel=true;return;}
                    reportNavigationId=args.NavigationId;
                };
                initialized.NewWindowRequested+=(_,args)=>args.Handled=true;
                initialized.NavigationCompleted+=(_,args)=>{if(args.NavigationId==reportNavigationId)reportNavigation?.TrySetResult(args.IsSuccess);};
            }
            if(session!=client.SessionGeneration||operationsStore==null)return;
            var core=reportWeb.CoreWebView2??throw new InvalidOperationException("WebView2 indisponible.");
            if(loadedReport!=reportHtml&&reportHtml.Length>0) {
                var folder=operationsStore.PreviewFolder;
                await File.WriteAllTextAsync(Path.Combine(folder,"report.html"),reportHtml,new UTF8Encoding(false));
                if(session!=client.SessionGeneration)return;
                core.ClearVirtualHostNameToFolderMapping("explorer-report.local");
                core.SetVirtualHostNameToFolderMapping("explorer-report.local",folder,CoreWebView2HostResourceAccessKind.DenyCors);
                loadedReport=reportHtml;reportNavigation=new(TaskCreationOptions.RunContinuationsAsynchronously);
                core.Navigate("https://explorer-report.local/report.html?v="+Guid.NewGuid().ToString("N"));
            }
        }catch {operationsStatus.Text="Aperçu indisponible ; vérifiez le stockage local et WebView2 Runtime. Les exports HTML et CSV restent disponibles.";}
        finally{reportLoading=false;reportSetup?.TrySetResult(true);}
    }
}

