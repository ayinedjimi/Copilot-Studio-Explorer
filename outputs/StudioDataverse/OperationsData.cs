using System.Text.Json;
namespace StudioDataverse;
public sealed partial class DataverseClient
{
    private async Task<string> ExplorerGet(string path,CancellationToken token)=>await SendAsync(HttpMethod.Get,path,cancellationToken:token,redactTrace:true);
    private async Task<List<JsonElement>> ExplorerRows(string path,CancellationToken token,int? maximum=null)
    {
        var rows=new List<JsonElement>();var seen=new HashSet<string>();
        do {
            token.ThrowIfCancellationRequested();if(!seen.Add(path))throw new InvalidOperationException("Pagination répétée.");
            using var doc=JsonDocument.Parse(await ExplorerGet(path,token));
            rows.AddRange(doc.RootElement.GetProperty("value").EnumerateArray().Select(x=>x.Clone()));
            if(maximum.HasValue&&rows.Count>=maximum.Value)return rows.Take(maximum.Value).ToList();
            if(rows.Count>20000)throw new InvalidOperationException("Lecture trop volumineuse ; restreignez le périmètre.");
            path=doc.RootElement.TryGetProperty("@odata.nextLink",out var next)?next.GetString()??"":"";
        }while(path.Length>0);
        return rows;
    }
    public async Task<AgentInspection> InspectAgentAsync(Agent agent,Action<string>? progress,CancellationToken token)
    {
        var missing=new List<string>();
        var detail=JsonSerializer.SerializeToElement(new {botid=agent.Id,name=agent.Name});
        var components=new List<JsonElement>();var flows=new List<JsonElement>();var connections=new List<JsonElement>();var secret=false;var ownerState="Non vérifiable";
        async Task Collect(string source,Func<Task> action)
        {
            progress?.Invoke(source);token.ThrowIfCancellationRequested();
            try {await action();}
            catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
            catch(HttpRequestException ex) {missing.Add(source+(ex.StatusCode==System.Net.HttpStatusCode.NotFound?":introuvable":""));if(!missing.Contains(source))missing.Add(source);}
            catch {missing.Add(source);}
        }
        await Collect("agent",async ()=> {using var doc=JsonDocument.Parse(await ExplorerGet($"bots({agent.Id:D})",token));detail=doc.RootElement.Clone();secret|=ExplorerRedaction.HasSecret(detail.ToString());});
        await Collect("components",async ()=> {
            components=await ExplorerRows($"bots({agent.Id:D})/botcomponent_parent_bot?$select=botcomponentid,name,componenttype,schemaname,description,data,statecode,statuscode,modifiedon",token);
            if(components.Count==0)components=await ExplorerRows($"bots({agent.Id:D})/bot_botcomponent?$select=botcomponentid,name,componenttype,schemaname,description,data,statecode,statuscode,modifiedon",token);
            secret|=components.Any(x=>ExplorerRedaction.HasSecret(x.ToString()));
        });
        foreach(var id in components.SelectMany(x=>ExplorerInspection.FlowIds(ExplorerInspection.Text(x,"data"))).Distinct())
            await Collect("flow:"+id,async ()=> {using var doc=JsonDocument.Parse(await ExplorerGet($"workflows({id:D})",token));flows.Add(doc.RootElement.Clone());secret|=ExplorerRedaction.HasSecret(doc.RootElement.ToString());});
        foreach(var name in components.Concat(flows).SelectMany(x=>ExplorerInspection.ConnectionNames(ExplorerInspection.Text(x,"data")+"\n"+ExplorerInspection.Text(x,"clientdata"))).Distinct(StringComparer.OrdinalIgnoreCase))
            await Collect("connection:"+name,async ()=> {
                var rows=await ExplorerRows("connectionreferences?$select=connectionreferenceid,connectionreferencelogicalname,connectionreferencedisplayname,connectorid,connectionid,statecode&$filter="+Uri.EscapeDataString("connectionreferencelogicalname eq '"+name.Replace("'","''")+"'"),token);
                if(rows.Count==0)missing.Add("connection:"+name+":introuvable");
                connections.AddRange(rows);secret|=rows.Any(x=>ExplorerRedaction.HasSecret(x.ToString()));
            });
        if(Guid.TryParse(ExplorerInspection.Text(detail,"_ownerid_value"),out var owner))
            await Collect("owner",async ()=> {
                try {
                    using var doc=JsonDocument.Parse(await ExplorerGet($"systemusers({owner:D})?$select=isdisabled",token));
                    ownerState=doc.RootElement.TryGetProperty("isdisabled",out var disabled)&&disabled.ValueKind is JsonValueKind.True or JsonValueKind.False?disabled.GetBoolean()?"Désactivé":"Actif":"Non vérifiable";
                }catch(HttpRequestException ex) when(ex.StatusCode==System.Net.HttpStatusCode.NotFound) {
                    using var doc=JsonDocument.Parse(await ExplorerGet($"teams({owner:D})?$select=teamid",token));ownerState="Équipe";
                }
            });
        DateTimeOffset? lastConversation=null;var activityState="Non vérifiable";
        await Collect("activity",async ()=> {
            var rows=await ExplorerRows("conversationtranscripts?$select=conversationstarttime&$filter="+Uri.EscapeDataString("_bot_conversationtranscriptid_value eq "+agent.Id)+"&$orderby=conversationstarttime desc&$top=1",token,1);
            if(rows.Count==0)activityState="Aucune transcription accessible";
            else {lastConversation=ExplorerInspection.Date(rows[0],"conversationstarttime");activityState=lastConversation.HasValue?"Observée":"Date non renseignée";}
        });
        var safeAgent=ExplorerRedaction.Json(detail);
        return ExplorerInspection.Create(agent.Id,ExplorerInspection.Text(detail,"name") is {Length:>0} currentName?currentName:agent.Name,safeAgent,components.Select(ExplorerRedaction.Json).ToList(),flows.Select(ExplorerRedaction.Json).ToList(),connections.Select(ExplorerRedaction.Json).ToList(),missing.Distinct().ToList(),secret,ownerState) with {LastConversation=lastConversation,ActivityState=activityState};
    }
    public async Task<(List<ExplorerIncident> Items,List<string> Warnings)> ExplorerIncidentsAsync(AgentInspection inspection,int sample,CancellationToken token)
    {
        var items=new List<ExplorerIncident>();var warnings=new List<string>();sample=Math.Clamp(sample,1,500);
        var since=DateTimeOffset.UtcNow.AddDays(-30).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        try {
            var path="conversationtranscripts?$select=conversationtranscriptid,name,conversationstarttime,content,metadata&$filter="+Uri.EscapeDataString($"_bot_conversationtranscriptid_value eq {inspection.AgentId:D} and conversationstarttime ge {since}")+"&$orderby=conversationstarttime desc&$top="+sample;
            var rows=await ExplorerRows(path,token,sample);
            foreach(var row in rows) {
                var id=ExplorerInspection.Text(row,"conversationtranscriptid");
                try {
                    var activities=TranscriptParser.Parse([new(Guid.TryParse(id,out var guid)?guid:Guid.NewGuid(),ExplorerInspection.Text(row,"content"),ExplorerInspection.Text(row,"metadata"))]);
                    foreach(var activity in activities.Where(AgentAnalytics.IsError)) {
                        var summary=activity.Text.Length>0?activity.Text:activity.Raw;
                        summary=ExplorerRedaction.Safe(summary);
                        if(summary.Length>3000)summary=summary[..3000]+"…";
                        items.Add(new(id+":"+activity.Id,inspection.AgentId,inspection.Name,"Conversation",activity.Type,summary,activity.Timestamp,id,id));
                    }
                }catch(JsonException) {warnings.Add("Une transcription n’a pas pu être interprétée : "+id);}
            }
            warnings.Add($"Conversations : {rows.Count} enregistrement(s) lu(s), sur 30 jours, limite {sample} ; absence d’erreur dans l’échantillon ≠ absence d’incident.");
        }catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}catch {warnings.Add("Transcriptions non accessibles : résultats partiels.");}
        foreach(var flow in inspection.Flows) {
            var id=ExplorerInspection.Text(flow,"workflowid");if(!Guid.TryParse(id,out var guid))continue;
            try {
                var rows=await ExplorerRows("flowruns?$filter="+Uri.EscapeDataString("_workflow_value eq "+guid)+"&$orderby=starttime desc&$top=20",token,20);
                foreach(var run in rows.Where(x=>ExplorerInspection.Text(x,"status").Equals("Failed",StringComparison.OrdinalIgnoreCase))) {
                    var correlation=ExplorerInspection.Text(run,"correlationid");if(correlation.Length==0)correlation=ExplorerInspection.Text(run,"flowrunid");
                    var summary=ExplorerRedaction.Safe(run.ToString());if(summary.Length>3000)summary=summary[..3000]+"…";
                    items.Add(new(ExplorerRedaction.Hash(run.ToString()),inspection.AgentId,inspection.Name,"Flux","Failed · "+ExplorerInspection.Text(flow,"name"),summary,ExplorerInspection.Date(run,"starttime"),correlation,id));
                }
                warnings.Add("Flux "+ExplorerInspection.Text(flow,"name")+" : "+rows.Count+" derniers runs lus, sans garantie d’exhaustivité historique.");
            }catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}catch {warnings.Add("Historique du flux inaccessible : "+ExplorerInspection.Text(flow,"name"));}
        }
        return (items.DistinctBy(x=>x.Id).OrderByDescending(x=>x.Time).ToList(),warnings);
    }
}
