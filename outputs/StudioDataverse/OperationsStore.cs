using System.Text.Json;
namespace StudioDataverse;
public sealed partial class ExplorerLocalStore
{
    private readonly string root;
    public ExplorerLocalStore(string username,string? basePath=null)
    {
        root=Path.Combine(basePath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CopilotStudioExplorer"),ExplorerRedaction.Hash(DataverseClient.Organization)[..16],ExplorerRedaction.Hash(username.Trim().ToLowerInvariant())[..24]);
    }
    private static async Task Write(string path,object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(value));File.Move(temp,path,true);}
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
    public string PreviewFolder { get {var folder=Path.Combine(root,"preview");Directory.CreateDirectory(folder);return folder;} }
    public async Task<SnapshotEntry> SaveSnapshot(AgentInspection inspection,string label)
    {
        var id=Guid.NewGuid().ToString("N");var snapshot=new SnapshotEntry(id,inspection.AgentId,inspection.Name,DateTimeOffset.UtcNow,ExplorerRedaction.Safe(label),inspection);
        await Write(Path.Combine(root,"snapshots",inspection.AgentId.ToString("D"),id+".json"),snapshot);return snapshot;
    }
    public List<SnapshotEntry> Snapshots(Guid agent)
    {
        var folder=Path.Combine(root,"snapshots",agent.ToString("D"));if(!Directory.Exists(folder))return [];
        return Directory.EnumerateFiles(folder,"*.json").Select(file=> {try{return JsonSerializer.Deserialize<SnapshotEntry>(File.ReadAllText(file));}catch{return null;}}).Where(x=>x!=null&&x.AgentId==agent&&x.Inspection!=null&&x.Inspection.Components!=null&&x.Inspection.Flows!=null&&x.Inspection.Connections!=null&&x.Inspection.Missing!=null&&x.Inspection.Findings!=null&&x.Inspection.Nodes!=null&&x.Inspection.Edges!=null).Cast<SnapshotEntry>().OrderByDescending(x=>x.Timestamp).Take(100).ToList();
    }
    public List<GovernanceNote> Notes()
    {
        try {var path=Path.Combine(root,"governance.json");return File.Exists(path)?(JsonSerializer.Deserialize<List<GovernanceNote>>(File.ReadAllText(path))??[]).Where(x=>x!=null&&x.AgentId!=Guid.Empty).Select(x=>x with{Tags=x.Tags??"",Status=x.Status??"À traiter",Comment=x.Comment??"",FindingId=x.FindingId??""}).ToList():[];}catch{return [];}
    }
    public Task SaveNote(GovernanceNote note)
    {
        var notes=Notes().Where(x=>x.AgentId!=note.AgentId||x.FindingId!=note.FindingId).ToList();
        notes.Add(note with {Tags=ExplorerRedaction.Safe(note.Tags),Comment=ExplorerRedaction.Safe(note.Comment)});
        return Write(Path.Combine(root,"governance.json"),notes);
    }
}
public static class SnapshotComparer
{
    public static Dictionary<string,string> Fields(AgentInspection inspection,bool ignoreDates)
    {
        var fields=new Dictionary<string,string>(StringComparer.Ordinal);
        void Walk(JsonElement value,string path)
        {
            if(value.ValueKind==JsonValueKind.Object) {
                foreach(var field in value.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal)) {
                    if(field.Name.StartsWith("@")||field.Name=="iconbase64"||(ignoreDates&&field.Name is "modifiedon" or "createdon"))continue;
                    Walk(field.Value,path+"/"+field.Name);
                }
            }
            else if(value.ValueKind==JsonValueKind.Array) {var i=0;foreach(var item in value.EnumerateArray())Walk(item,path+"/"+i++);}
            else fields[path]=value.ValueKind==JsonValueKind.Null?"(null)":value.ToString();
        }
        Walk(inspection.Agent,"agent");
        foreach(var (key,rows,id) in new[]{("components",inspection.Components,"botcomponentid"),("flows",inspection.Flows,"workflowid"),("connections",inspection.Connections,"connectionreferenceid")})
            foreach(var row in rows)Walk(row,key+"/"+ExplorerInspection.Text(row,id));
        return fields;
    }
    public static List<SnapshotDifference> Compare(AgentInspection before,AgentInspection after,bool ignoreDates=true)
    {
        if(before.AgentId!=after.AgentId)throw new InvalidOperationException("Choisissez deux versions du même agent.");
        var a=Fields(before,ignoreDates);var b=Fields(after,ignoreDates);
        var missing=before.Missing.Concat(after.Missing).ToArray();
        bool Accessible(string path)
        {
            if(path.StartsWith("agent/")&&missing.Contains("agent"))return false;
            if((path.StartsWith("components/")||path.StartsWith("flows/")||path.StartsWith("connections/"))&&missing.Contains("components"))return false;
            if(path.StartsWith("connections/")&&missing.Any(x=>x.StartsWith("connection:")))return false;
            if(path.StartsWith("flows/")&&missing.Where(x=>x.StartsWith("flow:")).Any(x=>path.StartsWith("flows/"+x[5..].Split(':')[0]+"/",StringComparison.OrdinalIgnoreCase)))return false;
            return true;
        }
        return a.Keys.Union(b.Keys).Where(Accessible).Order(StringComparer.Ordinal).Where(key=>!a.TryGetValue(key,out var av)||!b.TryGetValue(key,out var bv)||av!=bv)
            .Select(key=>new SnapshotDifference(key,a.GetValueOrDefault(key,"(absent)"),b.GetValueOrDefault(key,"(absent)"),!a.ContainsKey(key)?"Ajout":!b.ContainsKey(key)?"Suppression":"Modification")).ToList();
    }
}

