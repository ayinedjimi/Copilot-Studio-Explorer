using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace StudioDataverse;
public sealed class ScenarioRunner : IDisposable
{
    private readonly HttpClient http;
    private readonly Func<TimeSpan,CancellationToken,Task> delay;
    public ScenarioRunner(HttpClient? transport=null,Func<TimeSpan,CancellationToken,Task>? delay=null)
    {
        http=transport??new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(45)};
        this.delay=delay??((time,token)=>Task.Delay(time,token));
    }
    public static bool ValidTokenEndpoint(string value)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length>0||uri.Fragment.Length>0)return false;
        var host=uri.Host.ToLowerInvariant();
        return host=="powerva.microsoft.com"||host=="powervirtualagents.microsoft.com"||host.EndsWith(".powervirtualagents.microsoft.com")||host.EndsWith(".api.powerplatform.com");
    }
    public static string DirectLineOrigin(string region)=>region switch {"Europe"=>"https://europe.directline.botframework.com","Inde"=>"https://india.directline.botframework.com","Global"=>"https://directline.botframework.com",_=>throw new InvalidOperationException("Région de conversation inconnue.")};
    public async Task<string> RunAsync(string tokenEndpoint,string region,string question,CancellationToken token)
    {
        if(!ValidTokenEndpoint(tokenEndpoint))throw new InvalidOperationException("Point d’accès de conversation Microsoft invalide.");
        if(string.IsNullOrWhiteSpace(question))throw new InvalidOperationException("Question vide.");
        var origin=DirectLineOrigin(region);
        async Task<JsonDocument> Call(HttpMethod method,string url,string? bearer=null,string? body=null)
        {
            using var request=new HttpRequestMessage(method,url);
            if(bearer!=null)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",bearer);
            if(body!=null)request.Content=new StringContent(body,Encoding.UTF8,"application/json");
            HttpResponseMessage response;
            try{response=await http.SendAsync(request,token);}catch(HttpRequestException){throw new InvalidOperationException("Connexion au canal impossible. Vérifiez le réseau et la région.");}
            using var ownedResponse=response;
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Le canal de conversation a refusé la requête ("+(int)response.StatusCode+"). Vérifiez le canal, les droits et la région.");
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        }
        using var auth=await Call(HttpMethod.Get,tokenEndpoint);
        var temporaryToken=auth.RootElement.GetProperty("token").GetString();
        if(string.IsNullOrWhiteSpace(temporaryToken))throw new InvalidOperationException("Le canal n’a pas retourné de jeton de conversation.");
        using var conversation=await Call(HttpMethod.Post,origin+"/v3/directline/conversations",temporaryToken,"{}");
        var conversationId=conversation.RootElement.GetProperty("conversationId").GetString()??"";
        if(!Regex.IsMatch(conversationId,@"^[A-Za-z0-9_|=-]{1,200}$"))throw new InvalidOperationException("Identifiant de conversation invalide.");
        conversationId=Uri.EscapeDataString(conversationId);
        var conversationToken=conversation.RootElement.TryGetProperty("token",out var refreshed)&&refreshed.ValueKind==JsonValueKind.String?refreshed.GetString():temporaryToken;
        using var initial=await Call(HttpMethod.Get,origin+"/v3/directline/conversations/"+conversationId+"/activities",conversationToken);
        string watermark=initial.RootElement.TryGetProperty("watermark",out var initialWatermark)&&initialWatermark.ValueKind==JsonValueKind.String?initialWatermark.GetString()??"":"";
        var seen=new HashSet<string>();
        if(initial.RootElement.TryGetProperty("activities",out var previous)&&previous.ValueKind==JsonValueKind.Array)foreach(var activity in previous.EnumerateArray()){var previousId=TranscriptParser.Text(activity,"id");if(previousId.Length>0)seen.Add(previousId);}
        using var sent=await Call(HttpMethod.Post,origin+"/v3/directline/conversations/"+conversationId+"/activities",conversationToken,JsonSerializer.Serialize(new{type="message",from=new{id="explorer-scenario"},text=question}));
        for(int attempt=0;attempt<20;attempt++) {
            token.ThrowIfCancellationRequested();await delay(TimeSpan.FromSeconds(3),token);
            var suffix=watermark.Length==0?"":"?watermark="+Uri.EscapeDataString(watermark);
            using var activities=await Call(HttpMethod.Get,origin+"/v3/directline/conversations/"+conversationId+"/activities"+suffix,conversationToken);
            if(activities.RootElement.TryGetProperty("watermark",out var next)&&next.ValueKind==JsonValueKind.String)watermark=next.GetString()??"";
            var messages=new List<string>();
            if(activities.RootElement.TryGetProperty("activities",out var rows)&&rows.ValueKind==JsonValueKind.Array)
                foreach(var activity in rows.EnumerateArray()) {
                    var id=TranscriptParser.Text(activity,"id");if(id.Length>0&&!seen.Add(id))continue;
                    var type=TranscriptParser.Text(activity,"type");var from=activity.TryGetProperty("from",out var sender)?TranscriptParser.Text(sender,"id"):"";
                    if(type=="message"&&from!="explorer-scenario"&&TranscriptParser.Text(activity,"text") is {Length:>0} text)messages.Add(text);
                }
            if(messages.Count>0)return ExplorerRedaction.Safe(string.Join("\n",messages));
        }
        throw new InvalidOperationException("Aucune réponse textuelle reçue dans le délai. Consultez le canal et les transcriptions.");
    }
    public void Dispose()=>http.Dispose();
}

