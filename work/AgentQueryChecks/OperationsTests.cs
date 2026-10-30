using System.Net;
using System.Text.Json;
using StudioDataverse;

static class OperationsTests
{
    private static JsonElement J(object value)=>JsonSerializer.SerializeToElement(value);
    public static async Task<int> Run()
    {
        int count=0;
        void Check(bool value,string label){if(!value)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
        var id=Guid.Parse("01000000-0000-0000-0000-000000000001");
        var component=Guid.Parse("02000000-0000-0000-0000-000000000001");
        var flow=Guid.Parse("03000000-0000-0000-0000-000000000001");
        var owner=Guid.Parse("04000000-0000-0000-0000-000000000001");
        var raw="flowId: "+flow+"\napi_key: secret-training-value\nemail: person@example.test\ntel: 06 12 34 56 78";
        Check(ExplorerRedaction.HasSecret(raw),"Audit detects YAML secrets");
        Check(!ExplorerRedaction.Safe(raw).Contains("secret-training-value")&&!ExplorerRedaction.Safe(raw).Contains("person@example.test")&&!ExplorerRedaction.Safe(raw).Contains("06 12"),"Audit redacts credentials, email and phones");
        Check(ExplorerRedaction.Safe("flowId: "+flow).Contains(flow.ToString()),"Audit preserves technical identifiers through masking");
        Check(ExplorerRedaction.Endpoint("https://user:password@host.test/path?sig=secret#fragment")=="https://host.test/path","Audit endpoint drops credentials, query and fragment");
        var opaque=J(new {clientdata=JsonSerializer.Serialize(new {authentication=new{client_secret="nested-training-value"}})});
        Check(ExplorerRedaction.HasSecret(opaque.ToString()),"Audit finds secrets in nested serialized JSON");
        Check(!ExplorerRedaction.Json(opaque).ToString().Contains("nested-training-value"),"Audit removes nested serialized secret before persistence");
        var components=new List<JsonElement>{J(new {botcomponentid=component,name="Créer ticket",componenttype=9,schemaname="rmf_tool",data="kind: TaskDialog\nwebBrowsing: false\nkind: InvokeFlowTaskAction\nflowId: "+flow,modifiedon="2026-10-27T10:00:00Z"})};
        var flows=new List<JsonElement>{J(new {workflowid=flow,name="Flux de création",clientdata=JsonSerializer.Serialize(new {connectionReferenceLogicalName="rmf_mail",url="https://api.example.test/tickets"})})};
        var connections=new List<JsonElement>{J(new {connectionreferenceid=Guid.NewGuid(),connectionreferencelogicalname="rmf_mail",connectionreferencedisplayname="Messagerie"})};
        var agent=J(new {botid=id,name="Agent test",authenticationmode=2,authenticationtrigger=1,accesscontrolpolicy=2,authorizedsecuritygroupids=Guid.NewGuid().ToString(),publishedon="2026-10-28T10:00:00Z"});
        AgentInspection Build(JsonElement? detail=null,List<string>? missing=null,bool secret=false)=>ExplorerInspection.Create(id,"Agent test",detail??agent,components,flows,connections,missing??[],secret,"Actif");
        var inspection=Build();
        Check(inspection.Findings.Count==16,"Audit exposes sixteen explicit security checks");
        Check(inspection.Findings.Single(f=>f.Id=="SEC01").State==FindingState.Conforme,"Integrated authentication recognized");
        Check(Build(J(new{authenticationmode=1})).Findings.Single(f=>f.Id=="SEC01").State==FindingState.Risque,"Anonymous authentication is a critical risk");
        Check(Build(J(new{authenticationmode=0})).Findings.Single(f=>f.Id=="SEC01").State==FindingState.NonVerifiable,"Unspecified authentication never passes");
        Check(Build(J(new{accesscontrolpolicy=2,authorizedsecuritygroupids=""})).Findings.Single(f=>f.Id=="SEC03").State==FindingState.Risque,"Empty authorized groups flagged");
        Check(Build(secret:true).Findings.Single(f=>f.Id=="SEC04").State==FindingState.Risque,"Detected secret produces critical finding");
        Check(Build(missing:["flow:"+flow]).Findings.Single(f=>f.Id=="SEC04").State==FindingState.NonVerifiable,"Inaccessible flow prevents clean-secret assertion");
        Check(inspection.Findings.Where(f=>new[]{"SEC09","SEC10","SEC11","SEC12","SEC13"}.Contains(f.Id)).All(f=>f.State==FindingState.NonVerifiable),"External permissions and protections never fabricated");
        Check(inspection.Findings.Single(f=>f.Id=="SEC06").State==FindingState.AExaminer,"Sensitive actions require a path review even without runtime proof");
        Check(inspection.Nodes.Any(n=>n.Id=="flow:"+flow)&&inspection.Edges.Any(e=>e.Target=="flow:"+flow),"Declared flow reference creates a real graph edge");
        Check(inspection.Edges.Any(e=>e.Source=="flow:"+flow&&e.Target=="connection:rmf_mail"),"Flow connection reference linked");
        Check(inspection.Nodes.Any(n=>n.Kind=="source/destination"&&n.Label=="https://api.example.test/tickets"),"Declared endpoint creates source node without query");
        Check(ExplorerInspection.FlowIds("flowId: not-a-guid").Count==0,"Invalid references do not create fake edges");
        Check(ExplorerInspection.UpstreamAgents(inspection.Nodes,inspection.Edges,"connection:rmf_mail").Contains("agent:"+id),"Impact traversal reaches agent through flow and tool");
        var cycle=inspection.Edges.Concat([new DependencyEdge("cycle","connection:rmf_mail","flow:"+flow,"cycle")]).ToList();
        Check(ExplorerInspection.UpstreamAgents(inspection.Nodes,cycle,"connection:rmf_mail").Count==1,"Impact traversal terminates on cycles");
        var unchanged=inspection with{Components=inspection.Components.AsEnumerable().Reverse().ToList()};
        Check(SnapshotComparer.Compare(inspection,unchanged).Count==0,"Snapshot comparison is stable for record ordering");
        var altered=inspection with{Agent=J(new{botid=id,name="Agent changé",authenticationmode=2,authenticationtrigger=1,accesscontrolpolicy=2,authorizedsecuritygroupids=ExplorerInspection.Text(agent,"authorizedsecuritygroupids"),publishedon="2026-10-28T10:00:00Z"})};
        Check(SnapshotComparer.Compare(inspection,altered).Single().Path=="agent/name","Snapshot comparison identifies actual field change");
        var dated=inspection with{Agent=J(new{botid=id,name="Agent test",authenticationmode=2,authenticationtrigger=1,accesscontrolpolicy=2,authorizedsecuritygroupids=ExplorerInspection.Text(agent,"authorizedsecuritygroupids"),publishedon="2026-10-28T10:00:00Z",modifiedon="2026-10-29T10:00:00Z"})};
        Check(SnapshotComparer.Compare(inspection,dated).Count==0&&SnapshotComparer.Compare(inspection,dated,false).Count==1,"Date noise can be included or ignored");
        try{SnapshotComparer.Compare(inspection,inspection with{AgentId=Guid.NewGuid()});throw new Exception("Wrong agent accepted");}catch(InvalidOperationException){Check(true,"Snapshot comparison rejects different agents");}
        var temp=Path.Combine(Path.GetTempPath(),"ExplorerChecks-"+Guid.NewGuid().ToString("N"));
        var alice=new ExplorerLocalStore("alice@example.test",temp);var bob=new ExplorerLocalStore("bob@example.test",temp);
        await alice.SaveSnapshot(inspection,"Avant publication");
        Check(alice.Snapshots(id).Count==1&&bob.Snapshots(id).Count==0,"Snapshots isolated by signed-in account");
        await alice.SaveNote(new(id,"Production","Inventaire","Commentaire",DateTimeOffset.UtcNow));
        await alice.SaveNote(new(id,"","En cours","Action un",DateTimeOffset.UtcNow,"SEC01"));
        await alice.SaveNote(new(id,"","À traiter","Action deux",DateTimeOffset.UtcNow,"SEC04"));
        Check(alice.Notes().Count==3,"Governance annotations coexist per agent and finding");
        await alice.SaveNote(new(id,"","Accepté","Action revue",DateTimeOffset.UtcNow,"SEC01"));
        Check(alice.Notes().Count==3&&alice.Notes().Single(n=>n.FindingId=="SEC01").Status=="Accepté","Local triage updates only its own finding");
        Check(inspection.Findings.Single(f=>f.Id=="SEC01").State==FindingState.Conforme,"Local triage cannot mutate observed audit facts");
        Check(ExplorerReports.Cell("  =HYPERLINK(1)").StartsWith("\"'"),"Report CSV prevents spreadsheet formula execution");
        var malicious=inspection with{Name="<script>alert(1)</script>"};
        var html=ExplorerReports.Html([malicious],[],[],[]);
        Check(!html.Contains("<script>")&&html.Contains("&lt;script&gt;")&&html.Contains("Ayi NEDJIMI Consultants"),"Standalone report escapes names and includes author");
        Check(ExplorerReports.Csv([inspection],[],[],[]).Contains("SEC01")&&ExplorerReports.Csv([inspection],[],[],[]).Contains("Dépendance"),"Reports include audit evidence and dependency edges");
        var requests=new List<HttpRequestMessage>();var traces=new List<ApiTrace>();
        var cid=Guid.Parse("05000000-0000-0000-0000-000000000001");
        var handler=new Fake(request=>{
            if(request.Method!=HttpMethod.Get)throw new Exception("New module requested mutation");
            requests.Add(request);var path=Uri.UnescapeDataString(request.RequestUri!.ToString());
            string body;
            if(path.Contains("/bots(")&&!path.Contains("botcomponent_"))body=JsonSerializer.Serialize(new{botid=id,name="Agent test",authenticationmode=1,authenticationtrigger=0,accesscontrolpolicy=0,_ownerid_value=owner});
            else if(path.Contains("botcomponent_parent_bot"))body=JsonSerializer.Serialize(new{value=new[]{new {botcomponentid=component,name="Créer ticket",componenttype=9,schemaname="rmf_tool",data="kind: TaskDialog\nflowId: "+flow+"\napi_key: test-inspection-secret"}}});
            else if(path.Contains("/workflows("))body=flows[0].ToString();
            else if(path.Contains("/connectionreferences?"))body=JsonSerializer.Serialize(new{value=connections});
            else if(path.Contains("/systemusers("))body="{\"isdisabled\":true}";
            else if(path.Contains("/conversationtranscripts?"))body=JsonSerializer.Serialize(new{value=new[]{new{conversationtranscriptid=cid,name="Conversation",content="{\"activities\":[{\"id\":\"error1\",\"type\":\"error\",\"text\":\"api_key: test-error-secret\"}]}",metadata="{}"}}});
            else if(path.Contains("/flowruns?"))body=JsonSerializer.Serialize(new{value=new[]{new{status="Failed",starttime="2026-10-30T10:00:00Z",flowrunid=Guid.NewGuid(),error="Email person@example.test"},new{status="Succeeded",starttime="2026-10-30T11:00:00Z",flowrunid=Guid.NewGuid(),error=""}}});
            else body="{\"value\":[]}";
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)};
        });
        using var client=new DataverseClient(new HttpClient(handler),()=>Task.FromResult("local-test-placeholder"));client.Traced+=traces.Add;
        var collected=await client.InspectAgentAsync(new(id,"Agent test","rmf_agent",null),null,CancellationToken.None);
        Check(collected.SecretsDetected&&!JsonSerializer.Serialize(collected).Contains("test-inspection-secret"),"Audit collector detects and strips secrets before snapshot creation");
        Check(collected.OwnerState=="Désactivé"&&collected.Findings.Single(f=>f.Id=="SEC15").State==FindingState.Risque,"Disabled owner read from actual user record");
        Check(collected.Connections.Count==1&&collected.Edges.Any(e=>e.Target=="connection:rmf_mail"),"Collector resolves connection references from decoded flow JSON");
        Check(traces.All(t=>!t.Response.Contains("test-inspection-secret")),"New module API traces do not expose sensitive responses");
        var incidentResult=await client.ExplorerIncidentsAsync(collected,100,CancellationToken.None);
        Check(incidentResult.Items.Count==2&&incidentResult.Items.Select(x=>x.Source).Distinct().Count()==2,"Incident center correlates explicit conversation errors and failed runs only");
        Check(incidentResult.Items.All(i=>!i.Summary.Contains("test-error-secret")&&!i.Summary.Contains("person@example.test")),"Incident summaries redact secrets and email");
        Check(requests.All(r=>r.Method==HttpMethod.Get),"All new Dataverse module requests are read-only");
        Check(requests.Any(r=>Uri.UnescapeDataString(r.RequestUri!.ToString()).Contains("_bot_conversationtranscriptid_value eq "+id)),"Incident queries remain bound to selected agent");
        using var denied=new DataverseClient(new HttpClient(new Fake(_=>new HttpResponseMessage(HttpStatusCode.Forbidden){Content=new StringContent("{}")})),()=>Task.FromResult("local-test-placeholder"));
        var unknown=await denied.InspectAgentAsync(new(id,"Agent test","rmf_agent",null),null,CancellationToken.None);
        Check(unknown.Findings.All(f=>f.State!=FindingState.Conforme),"Denied scope cannot produce reassuring green audit checks");
        using var canceled=new CancellationTokenSource();canceled.Cancel();
        try{await client.InspectAgentAsync(new(id,"Agent test","rmf_agent",null),null,canceled.Token);throw new Exception("Cancelled read continued");}catch(OperationCanceledException){Check(true,"Audit collection is cancellable");}
        var unreadable=inspection with{Components=[],Flows=[],Connections=[],Missing=["components"]};
        Check(SnapshotComparer.Compare(inspection,unreadable).Count==0,"Unreadable snapshot sources never appear as deletions");
        Check(SnapshotComparer.Compare(inspection,inspection with{Components=[]}).Any(x=>x.Change=="Suppression"),"Verified absent components remain real snapshot deletions");
        Check(SecurityRules.Evaluate(inspection with{OwnerState="Équipe"}).Single(f=>f.Id=="SEC15").State==FindingState.AExaminer,"Team ownership requires membership review");
        Check(Build(J(new{authenticationmode=1,authenticationtrigger=1})).Findings.Single(f=>f.Id=="SEC02").State==FindingState.Risque,"Always trigger cannot make anonymous authentication safe");
        Check(ExplorerRedaction.HasSecret("{\"token\":\"arbitrary-training-value\"}")&&!ExplorerRedaction.Json(J(new {token="arbitrary-training-value"})).ToString().Contains("arbitrary-training-value"),"Credential fields are detected and masked beyond API-key patterns");
        return count;
    }
    private sealed class Fake(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(response(request));
    }
}
