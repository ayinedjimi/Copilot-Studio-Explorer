using System.Text.Json;
namespace StudioDataverse;

public record AgentTrend(Guid Id, string Name, int Users);
public record OrganizationAnalytics(int Records, int Unreadable, SortedDictionary<DateOnly,int> DailyUsers, List<AgentTrend> Trends, int IdentifiedUsers = 0)
{
    public static OrganizationAnalytics Parse(IEnumerable<JsonElement> source, TimeZoneInfo zone)
    {
        var rows=source.ToList(); var daily=new Dictionary<DateOnly,HashSet<string>>();
        var agentUsers=new Dictionary<Guid,HashSet<string>>(); var names=new Dictionary<Guid,string>(); int unreadable=0;
        var groups=rows.GroupBy(x=>TranscriptParser.Text(x,"name") is {Length:>0} name ? name : TranscriptParser.Text(x,"conversationtranscriptid"));
        foreach(var group in groups)
        {
            var row=group.First();
            if(!Guid.TryParse(TranscriptParser.Text(row,"_bot_conversationtranscriptid_value"),out var bot)) continue;
            var botName=TranscriptParser.Text(row,"_bot_conversationtranscriptid_value@OData.Community.Display.V1.FormattedValue");
            if(botName.Length==0) botName=TranscriptParser.MetadataText(TranscriptParser.Text(row,"metadata"),"BotName");
            names[bot]=botName.Length>0?botName:bot.ToString();
            List<TranscriptActivity> activities;
            try { activities=TranscriptParser.Parse(group.Select(x=>new TranscriptBatch(Guid.TryParse(TranscriptParser.Text(x,"conversationtranscriptid"),out var id)?id:Guid.NewGuid(),TranscriptParser.Text(x,"content"),TranscriptParser.Text(x,"metadata")))); }
            catch(JsonException) { unreadable++;continue; }
            foreach(var activity in activities.Where(x=>x.Type.Equals("message",StringComparison.OrdinalIgnoreCase)&&x.Role.Equals("user",StringComparison.OrdinalIgnoreCase)&&x.UserId.Length>0))
            {
                if(!agentUsers.TryGetValue(bot,out var users)) agentUsers[bot]=users=[];
                users.Add(activity.UserId);
                if(!activity.Timestamp.HasValue) continue;
                var date=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(activity.Timestamp.Value,zone).DateTime);
                if(!daily.TryGetValue(date,out var day)) daily[date]=day=[];
                day.Add(activity.UserId);
            }
        }
        return new(rows.Count,unreadable,new SortedDictionary<DateOnly,int>(daily.ToDictionary(x=>x.Key,x=>x.Value.Count)),agentUsers.Select(x=>new AgentTrend(x.Key,names[x.Key],x.Value.Count)).OrderByDescending(x=>x.Users).ThenBy(x=>x.Name).Take(5).ToList(),agentUsers.Values.SelectMany(x=>x).Distinct().Count());
    }
}
public sealed partial class DataverseClient
{
    public async Task<OrganizationAnalytics> OrganizationAnalyticsAsync()
    {
        var since=DateTimeOffset.UtcNow.AddDays(-30).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var filter=$"_bot_conversationtranscriptid_value ne null and conversationstarttime ge {since}";
        var path="conversationtranscripts?$select=conversationtranscriptid,name,_bot_conversationtranscriptid_value,metadata,content&$orderby=conversationstarttime%20desc&$top=100&$filter="+Uri.EscapeDataString(filter);
        using var doc=JsonDocument.Parse(await SendAsync(HttpMethod.Get,path));
        return OrganizationAnalytics.Parse(doc.RootElement.GetProperty("value").EnumerateArray(),TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"));
    }
}
