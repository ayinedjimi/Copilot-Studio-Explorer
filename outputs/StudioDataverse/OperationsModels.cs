using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace StudioDataverse;

public enum FindingState { Conforme, AExaminer, Risque, NonVerifiable }
public record SecurityFinding(string Id,string Category,string Title,string Severity,FindingState State,string Evidence,string Recommendation);
public record DependencyNode(string Id,string Label,string Kind,string State,string Detail);
public record DependencyEdge(string Id,string Source,string Target,string Label);
public record AgentInspection(Guid AgentId,string Name,DateTimeOffset CapturedAt,JsonElement Agent,List<JsonElement> Components,List<JsonElement> Flows,List<JsonElement> Connections,List<string> Missing,List<DependencyNode> Nodes,List<DependencyEdge> Edges,List<SecurityFinding> Findings,bool SecretsDetected,string OwnerState,DateTimeOffset? LastConversation=null,string ActivityState="Non vérifiable");
public record ExplorerIncident(string Id,Guid AgentId,string AgentName,string Source,string Type,string Summary,DateTimeOffset? Time,string Correlation,string ObjectId);
public record SnapshotEntry(string Id,Guid AgentId,string Name,DateTimeOffset Timestamp,string Label,AgentInspection Inspection);
public record SnapshotDifference(string Path,string Before,string After,string Change);
public record GovernanceNote(Guid AgentId,string Tags,string Status,string Comment,DateTimeOffset ModifiedAt,string FindingId="");

public static class ExplorerRedaction
{
    private const string Sensitive=@"(?i)(?:api[_-]?key|client[_-]?secret|sharedsecret|secret|subscription[_-]?key|x[_-]?api[_-]?key|x[_-]?functions[_-]?key|token|password|passwd|access[_-]?token|refresh[_-]?token|authorization|accountkey|sharedaccesssignature|connectionstring)";
    public static bool HasSecret(string text)
    {
        bool Plain(string value)=>Regex.IsMatch(value,@"(?i)\b(?:sk-[\w-]{12,}|eyJ[\w-]+\.[\w-]+\.[\w-]+)|Bearer\s+[\w.+/-]{12,}|https?://[^/\s]+:[^/\s]+@")||Regex.IsMatch(value,Sensitive+@"[""']?\s*[=:]\s*[""']?(?!\s*(?:null|false|true|\[MASQUE\]|@parameters))[^\r\n,""';}]{6,}");
        bool Scan(JsonElement value)
        {
            if(value.ValueKind==JsonValueKind.Object)foreach(var field in value.EnumerateObject()) {
                if(Regex.IsMatch(field.Name,Sensitive)&&field.Value.ValueKind==JsonValueKind.String&&field.Value.GetString() is {Length:>0} secret&&!secret.StartsWith('@')&&secret!="[MASQUE]")return true;
                if(Scan(field.Value))return true;
            }
            else if(value.ValueKind==JsonValueKind.Array) {foreach(var item in value.EnumerateArray())if(Scan(item))return true;}
            else if(value.ValueKind==JsonValueKind.String) {
                var raw=value.GetString()??"";if(Plain(raw))return true;
                if(raw.TrimStart().StartsWith('{')||raw.TrimStart().StartsWith('['))try{using var nested=JsonDocument.Parse(raw);return Scan(nested.RootElement);}catch(JsonException){}
            }
            return false;
        }
        try{using var doc=JsonDocument.Parse(text);return Scan(doc.RootElement);}catch(JsonException){return Plain(text);}
    }
    public static string Safe(string text)
    {
        var identifiers=new List<string>();
        text=Regex.Replace(text,@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",m=> {var key="[IDENTIFIANT_"+identifiers.Count+"]";identifiers.Add(m.Value);return key;});
        text=Regex.Replace(text,@"(?i)(https?://)[^/\s:@]+:[^/\s@]+@","$1[IDENTIFIANTS_MASQUES]@");
        text=Regex.Replace(text,@"(?i)(?:DefaultEndpointsProtocol|Server|Data Source|AccountEndpoint)\s*=\s*[^\r\n""']*;[^\r\n""']*","[MASQUE]");
        text=Regex.Replace(text,@"(?i)https?://[^\s""'<>]+[?&]sig=[^\s""'<>]+","[URL_SIGNEE_MASQUEE]");
        text=Regex.Replace(text,@"(?i)Bearer\s+[\w.+/-]{6,}|\bsk-[\w-]{12,}|eyJ[\w-]+\.[\w-]+\.[\w-]+","[MASQUE]");
        text=Regex.Replace(text,"("+Sensitive+@")[""']?\s*[=:]\s*[""']?[^\r\n,""';}]+","$1=[MASQUE]");
        text=Regex.Replace(text,@"[\p{L}\p{N}._%+\-]+@[\p{L}\p{N}.\-]+\.[\p{L}]{2,}","[EMAIL_MASQUE]");
        text=Regex.Replace(text,@"(?<!\w)(?:\+\d{1,3}[ .()-]*\d(?:[ .()-]*\d){7,12}|0[1-9](?:[ .()-]*\d){8})(?!\w)","[TEL_MASQUE]");
        for(int i=0;i<identifiers.Count;i++)text=text.Replace("[IDENTIFIANT_"+i+"]",identifiers[i]);
        return text;
    }
    public static JsonElement Json(JsonElement element)
    {
        object? Walk(JsonElement item)
        {
            if(item.ValueKind==JsonValueKind.Object)return item.EnumerateObject().Where(x=>x.Name!="iconbase64").ToDictionary(x=>x.Name,x=>Regex.IsMatch(x.Name,Sensitive)||x.Name=="authenticationconfiguration"?(object)"[MASQUE]":Walk(x.Value));
            if(item.ValueKind==JsonValueKind.Array)return item.EnumerateArray().Select(Walk).ToArray();
            if(item.ValueKind==JsonValueKind.String)return Safe(item.GetString()??"");
            return item.Clone();
        }
        return JsonSerializer.SerializeToElement(Walk(element));
    }
    public static string Endpoint(string value)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme is not("http" or "https"))return "";
        return new UriBuilder(uri){UserName="",Password="",Query="",Fragment=""}.Uri.GetLeftPart(UriPartial.Path);
    }
    public static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
public static class ExplorerInspection
{
    public static string Text(JsonElement row,string key)=>TranscriptParser.Text(row,key);
    public static int? Number(JsonElement row,string key)=>row.ValueKind==JsonValueKind.Object&&row.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out var n)?n:null;
    public static DateTimeOffset? Date(JsonElement row,string key)=>DateTimeOffset.TryParse(Text(row,key),out var date)?date:null;
    public static List<Guid> FlowIds(string data)=>Regex.Matches(data,@"(?i)\b(?:flowId|workflowId)[""']?\s*[:=]\s*[""']?([0-9a-f]{8}-[0-9a-f-]{27,})").Select(m=>Guid.TryParse(m.Groups[1].Value,out var id)?id:Guid.Empty).Where(x=>x!=Guid.Empty).Distinct().ToList();
    public static List<string> ConnectionNames(string text)=>Regex.Matches(text,@"(?i)\b(?:connectionReferenceLogicalName|connectionReferenceName|connectionReference)[""']?\s*[:=]\s*[""']?([\w.-]+)").Select(m=>m.Groups[1].Value).Where(x=>x is not "null" and not "true" and not "false").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    public static List<string> Urls(string text)=>Regex.Matches(text,@"https?://[^\s""'<>\}\]\)]+").Select(m=>ExplorerRedaction.Endpoint(m.Value)).Where(x=>x.Length>0).Distinct().ToList();
    public static AgentInspection Create(Guid id,string name,JsonElement agent,List<JsonElement> components,List<JsonElement> flows,List<JsonElement> connections,List<string> missing,bool secret,string ownerState)
    {
        var nodes=new Dictionary<string,DependencyNode>();
        var edges=new List<DependencyEdge>();
        var root="agent:"+id;
        nodes[root]=new(root,name,"agent","Accessible",id.ToString());
        void Link(string from,string to,string label)=>edges.Add(new(ExplorerRedaction.Hash(from+"|"+to+"|"+label),from,to,label));
        foreach(var component in components) {
            var cid=Text(component,"botcomponentid");if(cid.Length==0)continue;
            var nodeId="component:"+cid;var type=Number(component,"componenttype");
            var kind=type switch {16=>"connaissance",17=>"déclencheur",19=>"évaluation",9=>"outil/rubrique",15=>"instructions",_=>"composant"};
            var data=Text(component,"data");var label=Text(component,"name");
            nodes[nodeId]=new(nodeId,label.Length>0?label:Text(component,"schemaname"),kind,"Déclaré",Text(component,"schemaname"));
            Link(root,nodeId,"contient");
            foreach(var flowId in FlowIds(data)) {
                var flow=flows.FirstOrDefault(x=>Text(x,"workflowid").Equals(flowId.ToString(),StringComparison.OrdinalIgnoreCase));
                var target="flow:"+flowId;var found=flow.ValueKind==JsonValueKind.Object;
                nodes[target]=new(target,found?Text(flow,"name"):"Flux "+flowId.ToString()[..8],"flux",found?"Accessible":"Non vérifiable",flowId.ToString());
                Link(nodeId,target,type==17?"déclenche":"appelle");
            }
            foreach(var url in Urls(data)) {
                var target="url:"+ExplorerRedaction.Hash(url);nodes[target]=new(target,url,"source/destination","Déclarée",url);Link(nodeId,target,"référence");
            }
            foreach(var conn in ConnectionNames(data))AddConnection(nodeId,conn);
        }
        void AddConnection(string from,string logical)
        {
            var row=connections.FirstOrDefault(x=>Text(x,"connectionreferencelogicalname").Equals(logical,StringComparison.OrdinalIgnoreCase));
            var found=row.ValueKind==JsonValueKind.Object;var target="connection:"+logical.ToLowerInvariant();
            nodes[target]=new(target,found?Text(row,"connectionreferencedisplayname"):logical,"connexion",found?"Référence accessible":"Non vérifiable",logical);
            Link(from,target,"utilise");
        }
        foreach(var flow in flows) {
            var flowId=Text(flow,"workflowid");var node="flow:"+flowId;
            if(!nodes.ContainsKey(node))continue;
            var data=Text(flow,"clientdata");
            foreach(var conn in ConnectionNames(data))AddConnection(node,conn);
            foreach(var url in Urls(data)) {
                var target="url:"+ExplorerRedaction.Hash(url);nodes[target]=new(target,url,"source/destination","Déclarée",url);Link(node,target,"contacte");
            }
        }
        var raw=new AgentInspection(id,ExplorerRedaction.Safe(name),DateTimeOffset.UtcNow,agent,components,flows,connections,missing,nodes.Values.Select(n=>n with {Label=ExplorerRedaction.Safe(n.Label),Detail=ExplorerRedaction.Safe(n.Detail)}).ToList(),edges.DistinctBy(e=>e.Id).ToList(),[],secret,ownerState);
        return raw with {Findings=SecurityRules.Evaluate(raw)};
    }
    public static HashSet<string> UpstreamAgents(IEnumerable<DependencyNode> nodes,IEnumerable<DependencyEdge> edges,string target)
    {
        var visited=new HashSet<string>{target};var queue=new Queue<string>();queue.Enqueue(target);var all=edges.ToArray();
        while(queue.TryDequeue(out var current))foreach(var edge in all.Where(x=>x.Target==current))if(visited.Add(edge.Source))queue.Enqueue(edge.Source);
        return nodes.Where(x=>x.Kind=="agent"&&visited.Contains(x.Id)).Select(x=>x.Id).ToHashSet();
    }
}
public static class SecurityRules
{
    public static List<SecurityFinding> Evaluate(AgentInspection inspection)
    {
        var result=new List<SecurityFinding>();var agent=inspection.Agent;
        bool Known(string source)=>!inspection.Missing.Contains(source);
        var componentText=string.Join("\n",inspection.Components.Select(x=>ExplorerInspection.Text(x,"data")));
        var flowText=string.Join("\n",inspection.Flows.Select(x=>ExplorerInspection.Text(x,"clientdata")));
        void Add(string id,string group,string title,string severity,FindingState state,string evidence,string recommendation)=>result.Add(new(id,group,title,severity,state,ExplorerRedaction.Safe(evidence),recommendation));
        var mode=ExplorerInspection.Number(agent,"authenticationmode");
        Add("SEC01","Identité","Authentification utilisateur","Critique",!Known("agent")||mode is null or 0?FindingState.NonVerifiable:mode==1?FindingState.Risque:FindingState.Conforme,mode.HasValue?"authenticationmode = "+mode:"Champ inaccessible","Exiger une authentification adaptée aux données accessibles par l’agent.");
        var trigger=ExplorerInspection.Number(agent,"authenticationtrigger");
        Add("SEC02","Identité","Authentification à l’entrée","Élevée",!Known("agent")||trigger is null||mode is null or 0?FindingState.NonVerifiable:mode==1?FindingState.Risque:trigger==1?FindingState.Conforme:FindingState.AExaminer,"authenticationtrigger = "+trigger,"Examiner les chemins accessibles avant l’authentification ; choisir Always lorsque requis.");
        var policy=ExplorerInspection.Number(agent,"accesscontrolpolicy");var groups=ExplorerInspection.Text(agent,"authorizedsecuritygroupids");
        Add("SEC03","Exposition","Politique d’accès","Élevée",!Known("agent")||policy is null?FindingState.NonVerifiable:policy is 0 or 3?FindingState.AExaminer:policy==2&&groups.Trim().Length==0?FindingState.Risque:FindingState.Conforme,"accesscontrolpolicy = "+policy+(policy==2?"; groupes renseignés : "+(groups.Length>0):""),"Restreindre l’accès aux lecteurs ou groupes autorisés ; vérifier le partage effectif dans les consoles d’administration.");
        var complete=Known("agent")&&Known("components")&&!inspection.Missing.Any(x=>x.StartsWith("flow:"));
        Add("SEC04","Secrets","Secrets dans les configurations","Critique",inspection.SecretsDetected?FindingState.Risque:complete?FindingState.Conforme:FindingState.NonVerifiable,inspection.SecretsDetected?"Une signature de secret a été détectée ; valeur supprimée.":"Aucune signature détectée dans le périmètre lisible (détection heuristique).","Retirer les secrets des textes et utiliser des connexions sécurisées ; révoquer tout secret exposé.");
        var urls=ExplorerInspection.Urls(componentText+"\n"+flowText);
        Add("SEC05","Réseau","Chiffrement des URL déclarées","Élevée",urls.Any(x=>x.StartsWith("http://"))?FindingState.Risque:!complete?FindingState.NonVerifiable:FindingState.Conforme,urls.Count+" URL distincte(s) ; HTTP clair : "+urls.Count(x=>x.StartsWith("http://")),"Remplacer les URL HTTP par HTTPS et valider les domaines autorisés.");
        var writes=Regex.IsMatch(componentText+flowText,@"(?i)CreateRecord|UpdateRecord|DeleteRecord|SendEmail|HttpRequest|submit|InvokeFlowTaskAction|InvokeConnectorTaskAction");
        var confirm=Regex.IsMatch(componentText,@"(?im)^\s*(?:confirmationMode|requiresConfirmation):\s*(?:Always|true)\b");
        Add("SEC06","Actions","Confirmation avant action sensible","Élevée",!Known("components")?FindingState.NonVerifiable:writes?FindingState.AExaminer:FindingState.Conforme,writes?(confirm?"Une déclaration de confirmation est présente ; sa couverture des chemins doit être vérifiée.":"Actions à effet externe détectées ; confirmation non démontrée."):"Aucune signature d’action sensible observée dans les composants lus.","Vérifier manuellement chaque chemin et imposer une confirmation explicite avant une action à effet externe.");
        var web=Regex.Match(componentText,@"(?im)^\s*webBrowsing:\s*(true|false)\b");
        Add("SEC07","Connaissances","Recherche web","Moyenne",!Known("components")||!web.Success?FindingState.NonVerifiable:web.Groups[1].Value.Equals("true",StringComparison.OrdinalIgnoreCase)?FindingState.AExaminer:FindingState.Conforme,web.Success?web.Value.Trim():"Paramètre non trouvé","Limiter la recherche web aux besoins métier et considérer le contenu externe comme non fiable.");
        var external=inspection.Components.Count(x=>ExplorerInspection.Number(x,"componenttype")==17);
        Add("SEC08","Autonomie","Déclencheurs autonomes","Élevée",!Known("components")?FindingState.NonVerifiable:external>0?FindingState.AExaminer:FindingState.Conforme,external+" déclencheur(s) déclaré(s)","Vérifier filtres, anti-boucle, limites de volume et destinataires fixes.");
        var knowledge=inspection.Components.Count(x=>ExplorerInspection.Number(x,"componenttype")==16);
        Add("SEC09","Connaissances","Autorisations des connaissances","Élevée",FindingState.NonVerifiable,knowledge+" source(s) déclarée(s) ; permissions effectives non fournies par ces enregistrements.","Contrôler les droits des sources dans SharePoint et les autres systèmes concernés.");
        Add("SEC10","Connexions","Identité d’exécution des outils","Élevée",FindingState.NonVerifiable,inspection.Connections.Count+" référence(s) de connexion lisible(s) ; droits effectifs non déduits.","Examiner les identités d’exécution et les autorisations accordées, en particulier celles de l’auteur.");
        Add("SEC11","Politiques","Politiques DLP et connecteurs","Élevée",FindingState.NonVerifiable,"Source Power Platform d’administration non connectée.","Vérifier les politiques DLP : connecteurs, HTTP, canaux et déclencheurs.");
        Add("SEC12","Identité","Autorisations et consentements Entra","Élevée",FindingState.NonVerifiable,"Source Microsoft Entra non connectée.","Revoir les permissions applicatives/déléguées et les consentements avec un administrateur habilité.");
        Add("SEC13","Protection","Protection en exécution et journalisation","Élevée",FindingState.NonVerifiable,"Protection en exécution et journaux d’audit externes non interrogés.","Vérifier les protections et journaux dans les consoles de gouvernance concernées.");
        var published=ExplorerInspection.Date(agent,"publishedon");var modified=inspection.Components.Select(x=>ExplorerInspection.Date(x,"modifiedon")).Where(x=>x.HasValue).Select(x=>x!.Value).DefaultIfEmpty(DateTimeOffset.MinValue).Max();
        Add("SEC14","Cycle de vie","Publication et dernières modifications","Faible",!Known("agent")||!Known("components")?FindingState.NonVerifiable:!published.HasValue||modified>published?FindingState.AExaminer:FindingState.Conforme,published.HasValue?"Publication : "+published.Value.ToString("u"):"Jamais publié ou date absente.","Examiner les modifications avant toute republication ; une date n’atteste pas d’une validation de sécurité.");
        Add("SEC15","Propriété","Propriétaire accessible et actif","Élevée",inspection.OwnerState switch {"Actif"=>FindingState.Conforme,"Équipe"=>FindingState.AExaminer,"Désactivé"=>FindingState.Risque,_=>FindingState.NonVerifiable},"État du propriétaire : "+inspection.OwnerState,"Attribuer la responsabilité de l’agent et vérifier les comptes désactivés.");
        Add("SEC16","Réseau","Destinations et risques d’exfiltration","Élevée",!complete?FindingState.NonVerifiable:urls.Count>0||Regex.IsMatch(flowText+componentText,@"(?i)SendEmail|SMS|HttpRequest")?FindingState.AExaminer:FindingState.Conforme,urls.Count+" destination(s) URL déclarée(s) ; les domaines ne sont pas classés automatiquement comme sûrs.","Valider les domaines, destinataires, données envoyées et possibilités de manipulation par l’utilisateur.");
        return result;
    }
}
