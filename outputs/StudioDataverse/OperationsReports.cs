using System.Net;
using System.Text;
namespace StudioDataverse;
public static class ExplorerReports
{
    public static string Cell(string text)
    {
        text=ExplorerRedaction.Safe(text);
        if(text.TrimStart().StartsWith('=')||text.TrimStart().StartsWith('+')||text.TrimStart().StartsWith('-')||text.TrimStart().StartsWith('@'))text="'"+text;
        return "\""+text.Replace("\"","\"\"")+"\"";
    }
    public static string Csv(List<AgentInspection> inspections,List<ExplorerIncident> incidents,List<GovernanceNote> notes,List<SnapshotDifference> differences)
    {
        var lines=new List<string>{"module;agent;identifiant;criticite;statut;preuve;recommandation;horodatage"};
        void Row(params string[] values)=>lines.Add(string.Join(";",values.Select(Cell)));
        foreach(var i in inspections)foreach(var f in i.Findings)Row("Audit",i.Name,f.Id,f.Severity,f.State.ToString(),f.Evidence,f.Recommendation,i.CapturedAt.ToString("O"));
        foreach(var i in inspections)foreach(var e in i.Edges)Row("Dépendance",i.Name,e.Id,"",e.Label,i.Nodes.FirstOrDefault(n=>n.Id==e.Source)?.Label??e.Source,i.Nodes.FirstOrDefault(n=>n.Id==e.Target)?.Label??e.Target,i.CapturedAt.ToString("O"));
        foreach(var i in incidents)Row("Incident",i.AgentName,i.Correlation,"",i.Type,i.Summary,"Source : "+i.Source,i.Time?.ToString("O")??"");
        foreach(var n in notes)Row("Gouvernance",inspections.FirstOrDefault(i=>i.AgentId==n.AgentId)?.Name??n.AgentId.ToString(),n.FindingId,"",n.Status,n.Tags,n.Comment,n.ModifiedAt.ToString("O"));
        foreach(var d in differences)Row("Comparaison","",d.Path,"",d.Change,d.Before,d.After,"");
        return string.Join("\r\n",lines);
    }
    public static string Html(List<AgentInspection> inspections,List<ExplorerIncident> incidents,List<GovernanceNote> notes,List<SnapshotDifference> differences)
    {
        string H(string text)=>WebUtility.HtmlEncode(ExplorerRedaction.Safe(text));
        var findings=inspections.SelectMany(i=>i.Findings).ToArray();
        var risks=findings.Count(f=>f.State==FindingState.Risque);
        var review=findings.Count(f=>f.State==FindingState.AExaminer);
        var unknown=findings.Count(f=>f.State==FindingState.NonVerifiable);
        var b=new StringBuilder("<!doctype html><html lang='fr'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; style-src 'unsafe-inline'; img-src data:; base-uri 'none'; form-action 'none'\"><title>Copilot Studio Explorer · Rapport</title><style>body{font:14px Segoe UI,Arial;color:#242424;background:#fafafa;margin:30px}header{background:linear-gradient(110deg,#f6e1ef,#e4ecff);padding:28px;border-radius:12px}h1{font-size:28px;margin:0 0 12px}h2{margin:28px 0 14px}.cards{display:flex;gap:12px;margin:22px 0}.card{background:#fff;border:1px solid #dedee5;border-radius:9px;padding:18px;flex:1}.number{font-size:32px;font-weight:600}.risk{color:#c4314b}.review{color:#b76a06}.unknown{color:#777}table{border-collapse:collapse;width:100%;background:#fff;table-layout:fixed}td,th{text-align:left;vertical-align:top;padding:10px;border-bottom:1px solid #e6e6ee;overflow-wrap:anywhere}th{background:#f0eff8}small,.muted{color:#727080}article{break-inside:avoid;margin-bottom:20px}pre{white-space:pre-wrap;word-break:break-word}@media print{body{margin:10mm;background:white}.cards{display:flex}header{background:#eef0fa}thead{display:table-header-group}}</style></head><body><header><h1>Copilot Studio Explorer</h1><p>Rapport de sécurité, dépendances, incidents et gouvernance</p><p>Auteur : "+H(ProductIdentity.Author)+" · "+DateTimeOffset.Now.ToString("dd/MM/yyyy HH:mm")+"</p><small>Périmètre : "+inspections.Count+" agent(s) analysé(s). Lecture seule. Résultats statiques ; les données inaccessibles et les échantillons ne prouvent pas l’absence de risque.</small></header>");
        b.Append("<div class='cards'><div class='card'><div class='number risk'>"+risks+"</div>risques détectés</div><div class='card'><div class='number review'>"+review+"</div>points à examiner</div><div class='card'><div class='number unknown'>"+unknown+"</div>non vérifiables</div><div class='card'><div class='number'>"+incidents.Count+"</div>incidents observés dans les échantillons</div></div>");
        foreach(var inspection in inspections) {
            b.Append("<article><h2>"+H(inspection.Name)+"</h2><p>Collecte : "+inspection.CapturedAt.ToLocalTime().ToString("g")+" · Propriétaire : "+H(inspection.OwnerState)+"</p><table><thead><tr><th style='width:24%'>Contrôle</th><th style='width:17%'>État / criticité</th><th>Preuve</th><th>Recommandation</th></tr></thead><tbody>");
            foreach(var f in inspection.Findings)b.Append("<tr><td>"+H(f.Id+" · "+f.Title)+"</td><td>"+H(f.State+" · "+f.Severity)+"</td><td>"+H(f.Evidence)+"</td><td>"+H(f.Recommendation)+"</td></tr>");
            b.Append("</tbody></table><h3>Dépendances observées</h3><ul>");
            foreach(var e in inspection.Edges)b.Append("<li>"+H(inspection.Nodes.FirstOrDefault(n=>n.Id==e.Source)?.Label??e.Source)+" → "+H(e.Label)+" → "+H(inspection.Nodes.FirstOrDefault(n=>n.Id==e.Target)?.Label??e.Target)+"</li>");
            b.Append("</ul>");
            if(inspection.Missing.Count>0)b.Append("<p class='unknown'>Sources inaccessibles : "+H(string.Join(", ",inspection.Missing))+"</p>");
            b.Append("</article>");
        }
        b.Append("<h2>Incidents observés</h2><p class='muted'>Conversations sur 30 jours, nombre d’enregistrements borné ; flux limités aux derniers runs accessibles. Un extrait n’est pas un journal exhaustif.</p><table><tr><th>Agent / date</th><th>Source</th><th>Résumé</th><th>Corrélation</th></tr>");
        foreach(var i in incidents)b.Append("<tr><td>"+H(i.AgentName)+"<br>"+H(i.Time?.ToLocalTime().ToString("g")??"Heure inconnue")+"</td><td>"+H(i.Source+" · "+i.Type)+"</td><td><pre>"+H(i.Summary)+"</pre></td><td>"+H(i.Correlation)+"</td></tr>");
        b.Append("</table><h2>Suivi local des recommandations</h2><p class='muted'>Statuts déclaratifs locaux : ils ne modifient pas les constats de sécurité ni les objets Dataverse.</p><table><tr><th>Agent / contrôle</th><th>État</th><th>Étiquettes</th><th>Commentaire</th></tr>");
        foreach(var n in notes)b.Append("<tr><td>"+H((inspections.FirstOrDefault(i=>i.AgentId==n.AgentId)?.Name??n.AgentId.ToString())+" · "+n.FindingId)+"</td><td>"+H(n.Status)+"</td><td>"+H(n.Tags)+"</td><td>"+H(n.Comment)+"</td></tr>");
        b.Append("</table>");
        if(differences.Count>0) {
            b.Append("<h2>Comparaison d’instantanés</h2><table><tr><th>Champ</th><th>Avant</th><th>Après</th></tr>");
            foreach(var d in differences)b.Append("<tr><td>"+H(d.Change+" · "+d.Path)+"</td><td><pre>"+H(d.Before)+"</pre></td><td><pre>"+H(d.After)+"</pre></td></tr>");
            b.Append("</table>");
        }
        return b.Append("<p class='muted'>Copilot Studio Explorer · Ayi NEDJIMI Consultants · Les secrets détectés ont été masqués. DLP, autorisations externes et protections d’exécution nécessitent leurs sources d’administration.</p></body></html>").ToString();
    }
}
