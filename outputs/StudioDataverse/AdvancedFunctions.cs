using System.Text.Json;
using System.Text.RegularExpressions;
namespace StudioDataverse;
public record CustomRule(string Id,string Name,string Field,string Operation,string Value);
public record CustomRuleResult(CustomRule Rule,Guid AgentId,string Agent,FindingState State,string Evidence);
public record ScenarioRun(DateTimeOffset Time,double DurationSeconds,string Response,string Status,string Error="");
public record ScenarioCase(string Id,string Title,string Question,string Expected,string Observed,Guid AgentId=default,string MatchText="",string Verification="Contient",List<ScenarioRun>? Runs=null);
public record SearchHit(Guid AgentId,string Agent,string Object,string Field,string Excerpt);
public static class AdvancedFunctions
{
    public static List<CustomRule> ParseImportedRules(string json)
    {
        var raw=JsonSerializer.Deserialize<List<CustomRule>>(json,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidOperationException("Le fichier doit contenir une liste de règles.");
        if(raw.Count is <1 or >100)throw new InvalidOperationException("Le fichier doit contenir entre 1 et 100 règles.");
        var rules=new List<CustomRule>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var rule in raw){
            if(rule==null||string.IsNullOrWhiteSpace(rule.Name)||string.IsNullOrWhiteSpace(rule.Value)||rule.Name.Length>120||rule.Value.Length>1000)throw new InvalidOperationException("Chaque règle doit avoir un nom et un texte non vides.");
            if(rule.Field is not ("Nom" or "Instructions" or "Composants" or "Flux")||rule.Operation is not ("Contient" or "Ne contient pas" or "Commence par"))throw new InvalidOperationException("Champ ou opérateur de règle inconnu.");
            var name=rule.Name.Trim();if(!seen.Add(name))throw new InvalidOperationException("Le fichier contient plusieurs règles de même nom : "+name);
            rules.Add(new(Guid.NewGuid().ToString("N"),name,rule.Field,rule.Operation,rule.Value.Trim()));
        }
        return rules;
    }
    public static List<CustomRuleResult> EvaluateRules(IEnumerable<CustomRule> rules,IEnumerable<AgentInspection> agents)
    {
        var results=new List<CustomRuleResult>();
        foreach(var agent in agents)foreach(var rule in rules) {
            var text=rule.Field switch { "Nom"=>agent.Name,"Instructions"=>string.Join("\n",agent.Components.Where(x=>ExplorerInspection.Number(x,"componenttype")==15).Select(x=>ExplorerInspection.Text(x,"data"))),"Composants"=>string.Join("\n",agent.Components.Select(x=>ExplorerInspection.Text(x,"name")+" "+ExplorerInspection.Text(x,"data"))),"Flux"=>string.Join("\n",agent.Flows.Select(x=>ExplorerInspection.Text(x,"name")+" "+ExplorerInspection.Text(x,"clientdata"))),_=>"" };
            var unavailable=rule.Field switch {"Nom"=>agent.Missing.Contains("agent"),"Instructions" or "Composants"=>agent.Missing.Contains("components"),"Flux"=>agent.Missing.Contains("components")||agent.Missing.Any(x=>x.StartsWith("flow:")),_=>true};
            var match=rule.Operation switch {"Contient"=>text.Contains(rule.Value,StringComparison.OrdinalIgnoreCase),"Commence par"=>text.StartsWith(rule.Value,StringComparison.OrdinalIgnoreCase),"Ne contient pas"=>!text.Contains(rule.Value,StringComparison.OrdinalIgnoreCase),_=>false};
            var evidence=unavailable?"Source non accessible.":text.Length==0?"Aucune donnée disponible.":ExplorerRedaction.Safe(text[..Math.Min(240,text.Length)]);
            results.Add(new(rule,agent.AgentId,agent.Name,unavailable||text.Length==0?FindingState.NonVerifiable:match?FindingState.Conforme:FindingState.AExaminer,evidence));
        }
        return results;
    }
    public static List<SearchHit> Search(IEnumerable<AgentInspection> agents,string query)
    {
        if(string.IsNullOrWhiteSpace(query))return [];
        var hits=new List<SearchHit>();
        foreach(var agent in agents) {
            void Add(string obj,string field,string text) {
                var index=text.IndexOf(query,StringComparison.OrdinalIgnoreCase);if(index<0)return;
                var start=Math.Max(0,index-70);hits.Add(new(agent.AgentId,agent.Name,obj,field,ExplorerRedaction.Safe(text.Substring(start,Math.Min(200,text.Length-start)))));
            }
            Add(agent.Name,"Nom",agent.Name);
            foreach(var component in agent.Components) {var name=ExplorerInspection.Text(component,"name");Add(name,"Nom",name);Add(name,"Configuration",ExplorerInspection.Text(component,"data"));}
            foreach(var flow in agent.Flows) {var name=ExplorerInspection.Text(flow,"name");Add(name,"Nom",name);Add(name,"Définition",ExplorerInspection.Text(flow,"clientdata"));}
        }
        return hits;
    }
    public static string ScenarioStatus(ScenarioCase item)=>string.IsNullOrWhiteSpace(item.Observed)?"Non évalué":item.Verification=="Manuel"?"Jugement manuel":string.IsNullOrWhiteSpace(item.MatchText)&&string.IsNullOrWhiteSpace(item.Expected)?"Attendu manquant":item.Observed.Contains(string.IsNullOrWhiteSpace(item.MatchText)?item.Expected:item.MatchText,StringComparison.OrdinalIgnoreCase)?"Correspondance textuelle":"À examiner";
    public static string Diagnostic(Exception error)=>error is HttpRequestException http ? http.StatusCode switch {
        System.Net.HttpStatusCode.Unauthorized=>"401 : reconnectez-vous et vérifiez le consentement Dataverse.",
        System.Net.HttpStatusCode.Forbidden=>"403 : vérifiez les rôles de sécurité et les droits sur les tables.",
        System.Net.HttpStatusCode.NotFound=>"404 : vérifiez l’environnement, la table et les références.",
        _=>"Erreur réseau ou réponse Dataverse indisponible : vérifiez l’accès et consultez le journal API."
    }:error is OperationCanceledException?"Opération annulée ou délai dépassé.":"Vérifiez la connexion Microsoft, les droits Dataverse et le réseau.";
}
public sealed partial class DataverseClient
{
    public async Task<List<JsonElement>> ExplorerVariableMetadataAsync(CancellationToken token)=>await ExplorerRows("environmentvariabledefinitions?$select=schemaname,type&$orderby=schemaname",token);
    public async Task<List<JsonElement>> ExplorerSolutionsAsync(CancellationToken token)=>await ExplorerRows("solutions?$select=solutionid,friendlyname,uniquename,version,ismanaged,_publisherid_value&$orderby=friendlyname",token);
    public async Task<List<JsonElement>> ExplorerSolutionComponentsAsync(Guid solution,CancellationToken token)=>await ExplorerRows("solutioncomponents?$select=solutioncomponentid,componenttype,objectid&$filter="+Uri.EscapeDataString("_solutionid_value eq "+solution),token);
    public async Task<List<TranscriptActivity>> ExplorerConversationAsync(Guid transcript,CancellationToken token)
    {
        using var doc=JsonDocument.Parse(await ExplorerGet("conversationtranscripts("+transcript+")?$select=conversationtranscriptid,content,metadata",token));
        var root=doc.RootElement;
        return TranscriptParser.Parse([new(transcript,ExplorerInspection.Text(root,"content"),ExplorerInspection.Text(root,"metadata"))]);
    }
}
public sealed partial class ExplorerLocalStore
{
    private async Task SaveNamed<T>(string name,T value) {
        if(name is not ("rules" or "scenarios" or "records"))throw new InvalidOperationException();
        await Write(Path.Combine(root,name+".json"),value!);
    }
    private List<T> ReadNamed<T>(string name) {
        try{var path=Path.Combine(root,name+".json");return File.Exists(path)?JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path))??[]:[];}catch{return [];}
    }
    public List<CustomRule> Rules()=>ReadNamed<CustomRule>("rules");
    public Task SaveRules(List<CustomRule> rules)=>SaveNamed("rules",rules.Select(r=>r with{Name=ExplorerRedaction.Safe(r.Name),Value=ExplorerRedaction.Safe(r.Value)}).ToList());
    public List<ScenarioCase> Scenarios()=>ReadNamed<ScenarioCase>("scenarios");
    public Task SaveScenarios(List<ScenarioCase> cases)=>SaveNamed("scenarios",cases.Select(c=>c with{Title=ExplorerRedaction.Safe(c.Title),Question=ExplorerRedaction.Safe(c.Question),Expected=ExplorerRedaction.Safe(c.Expected),Observed=ExplorerRedaction.Safe(c.Observed),MatchText=ExplorerRedaction.Safe(c.MatchText??""),Runs=c.Runs?.Select(r=>r with{Response=ExplorerRedaction.Safe(r.Response),Error=ExplorerRedaction.Safe(r.Error)}).ToList()}).ToList());
    public List<GovernanceRecord> Records()=>ReadNamed<GovernanceRecord>("records");
    public Task SaveRecords(List<GovernanceRecord> records)=>SaveNamed("records",records.Select(r=>r with{Owner=ExplorerRedaction.Safe(r.Owner),BusinessCriticality=ExplorerRedaction.Safe(r.BusinessCriticality),Comment=ExplorerRedaction.Safe(r.Comment)}).ToList());
}
public record GovernanceRecord(Guid AgentId,string Owner,string BusinessCriticality,DateTimeOffset? ReviewDue,string Comment);
