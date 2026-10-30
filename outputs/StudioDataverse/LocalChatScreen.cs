using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.System;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private readonly WebView2 chatWeb=new(){Height=650,HorizontalAlignment=HorizontalAlignment.Stretch};
    private readonly Border chatHost=new(){CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(1)};
    private readonly TextBlock chatStatus=Label("Conversation locale · aucune donnée envoyée à un service distant.",12,"#687080");
    private readonly CheckBox chatUseAgent=new(){Content="Utiliser le contexte de l’agent sélectionné"};
    private readonly List<(string Role,string Text)> chatHistory=[];
    private CancellationTokenSource? chatCancellation;
    private CancellationTokenSource? modelDownloadCancellation;
    private bool chatLoading,chatReady,chatBusy;
    private int chatGeneration;
    private void RenderLocalChat()
    {
        var options=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
        options.Children.Add(BuildModelSelector(true));if(chatUseAgent.Parent is Panel previous)previous.Children.Remove(chatUseAgent);options.Children.Add(chatUseAgent);
        var cancel=new Button{Content="Arrêter"};cancel.Click+=(_,_)=>chatCancellation?.Cancel();options.Children.Add(cancel);
        var clear=new Button{Content="Nouvelle conversation"};clear.Click+=(_,_)=>ClearLocalChat();options.Children.Add(clear);
        operationsBody.Children.Add(options);operationsBody.Children.Add(BuildModelDownloadPanel());operationsBody.Children.Add(chatStatus);chatHost.BorderBrush=Brush("#E3E2ED");if(chatHost.Child==null)chatHost.Child=chatWeb;
        chatWeb.Height=Math.Clamp(pageSurface.ActualHeight-330,260,560);operationsBody.Children.Add(chatHost);
        var composer=new Grid{ColumnSpacing=10};composer.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});composer.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        var input=new TextBox{PlaceholderText="Écrivez votre message…",MaxLength=4000,HorizontalAlignment=HorizontalAlignment.Stretch};composer.Children.Add(input);
        var send=new Button{Content="Envoyer",Background=Brush("#6357D5"),Foreground=Brush("#FFFFFF"),Padding=new Thickness(20,10,20,10)};send.Click+=async (_,_)=>await SendLocalChatAsync(input);input.KeyDown+=async (_,e)=>{if(e.Key==VirtualKey.Enter){e.Handled=true;await SendLocalChatAsync(input);}};Place(composer,send,0,1);operationsBody.Children.Add(composer);
        operationsBody.Children.Add(Label("Entrée pour envoyer · les réponses peuvent contenir des erreurs.",11,"#858895"));
        _=EnsureLocalChatAsync();
    }
    private async Task EnsureLocalChatAsync()
    {
        if(!chatWeb.IsLoaded||chatLoading||chatReady)return;chatLoading=true;
        try{
            await chatWeb.EnsureCoreWebView2Async(await OperationsWebEnvironment());var core=chatWeb.CoreWebView2??throw new InvalidOperationException("WebView2 indisponible.");
            if(!string.IsNullOrEmpty(core.Source)&&core.Source.Contains("/chat.html"))return;
            core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDefaultScriptDialogsEnabled=false;core.Settings.IsStatusBarEnabled=false;
            core.SetVirtualHostNameToFolderMapping("agentmap.local",Path.Combine(AppContext.BaseDirectory,"Assets","AgentMap"),CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting+=(_,e)=>{if(!IsChatOrigin(e.Uri))e.Cancel=true;else chatReady=false;};core.NewWindowRequested+=(_,e)=>e.Handled=true;
            core.WebMessageReceived+=ReceiveLocalChatMessage;core.Navigate(MapOrigin+"/chat.html");
        }catch(Exception ex){chatStatus.Text="Impossible d’ouvrir le chat local. Vérifiez WebView2 Runtime.";await App.ShowErrorAsync(this,App.RecordError(ex));}
        finally{chatLoading=false;}
    }
    private static bool IsChatOrigin(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&IsMapOrigin(value)&&uri.AbsolutePath.Equals("/chat.html",StringComparison.OrdinalIgnoreCase);
    private void PostChat(string type,string text="")
    {
        if(!chatReady||chatWeb.CoreWebView2==null)return;
        try{chatWeb.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{type,text}));}catch(Exception ex){chatStatus.Text="Affichage du chat indisponible.";App.RecordError(ex);}
    }
    private void ClearLocalChat()
    {
        chatGeneration++;chatCancellation?.Cancel();chatHistory.Clear();PostChat("clear");chatStatus.Text="Conversation effacée de la mémoire.";
    }
    private void ReceiveLocalChatMessage(object? sender,CoreWebView2WebMessageReceivedEventArgs e)
    {
        if(!IsChatOrigin(e.Source))return;
        try{
            using var doc=JsonDocument.Parse(e.WebMessageAsJson);var root=doc.RootElement;
            if(!root.TryGetProperty("action",out var action))return;
            if(action.GetString()=="ready")chatReady=true;
        }catch(Exception ex){chatStatus.Text="Le panneau de conversation ne répond pas.";App.RecordError(ex);}
    }
    private async Task SendLocalChatAsync(TextBox input)
    {
        try{
            if(!chatReady){chatStatus.Text="Le panneau de conversation est en cours de chargement.";return;}
            if(chatBusy){chatStatus.Text="Attendez la fin de la réponse en cours.";return;}
            var text=input.Text.Trim();if(text.Length==0||text.Length>4000){chatStatus.Text="Saisissez un message de 4 000 caractères maximum.";return;}
            string context="";
            if(chatUseAgent.IsChecked==true){if(!operationsAgentId.HasValue||!inspections.TryGetValue(operationsAgentId.Value,out var inspection)){PostChat("error","Analysez d’abord l’agent sélectionné pour utiliser son contexte.");return;}
                context=ExplorerRedaction.Safe(inspection.Name+"\n"+string.Join("\n",inspection.Components.Take(25).Select(c=>ExplorerInspection.Text(c,"name")+" · "+ExplorerInspection.Text(c,"componenttype"))));if(context.Length>2500)context=context[..2500];}
            var generation=chatGeneration;var current=chatCancellation=new CancellationTokenSource();chatBusy=true;if(chatModelSelector!=null)chatModelSelector.IsEnabled=false;chatHistory.Add(("user",text));input.Text="";PostChat("user",text);chatStatus.Text="Génération locale en cours…";
            try{var messages=chatHistory.Select(x=>(x.Role,x.Text.Length>1000?x.Text[..1000]:x.Text)).ToArray();var answer=await LocalQuestionEngine.ChatAsync(questionModelPath,messages,context,current.Token,questionModelFormat,EngineOptions());if(generation!=chatGeneration)return;chatHistory.Add(("assistant",answer));PostChat("answer",answer);chatStatus.Text="Réponse produite localement · vérifiez son contenu.";}
            catch(OperationCanceledException){if(generation==chatGeneration){if(chatHistory.Count>0)chatHistory.RemoveAt(chatHistory.Count-1);PostChat("error","Génération annulée.");chatStatus.Text="Génération annulée.";}}
            catch(Exception ex){if(generation==chatGeneration){if(chatHistory.Count>0)chatHistory.RemoveAt(chatHistory.Count-1);PostChat("error",ExplorerRedaction.Safe(DescribeError(ex)));chatStatus.Text="La réponse n’a pas pu être générée.";await App.ShowErrorAsync(this,App.RecordError(ex));}}
            finally{chatBusy=false;if(chatModelSelector!=null)chatModelSelector.IsEnabled=true;if(chatCancellation==current)chatCancellation=null;current.Dispose();}
        }catch(Exception ex){PostChat("error","Message de chat invalide.");App.RecordError(ex);}
    }
}
