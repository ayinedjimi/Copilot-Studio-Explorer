using System.Text.Json;

namespace StudioDataverse;

public record AgentAnalytics(int Records, int Conversations, int UserMessages, int AgentMessages, int IdentifiedUsers,
    List<TranscriptActivity> Errors, List<double> ResponseSeconds, List<string> Warnings)
{
    public double? MedianSeconds => ResponseSeconds.Count == 0 ? null : Percentile(ResponseSeconds, .5);
    public double? P95Seconds => ResponseSeconds.Count == 0 ? null : Percentile(ResponseSeconds, .95);
    private static double Percentile(List<double> data, double p) { var sorted = data.Order().ToArray(); return sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Length) - 1, 0, sorted.Length - 1)]; }
    public static AgentAnalytics FromRows(IEnumerable<JsonElement> source)
    {
        var rows = source.ToList();
        var errors = new List<TranscriptActivity>(); var delays = new List<double>(); var users = new HashSet<string>();
        var warnings = new List<string>(); int userMessages = 0, botMessages = 0;
        var groups = rows.GroupBy(x => TranscriptParser.Text(x, "name") is { Length: > 0 } name ? name : TranscriptParser.Text(x, "conversationtranscriptid"));
        foreach (var group in groups)
        {
            var batches = group.Select(x => new TranscriptBatch(Guid.TryParse(TranscriptParser.Text(x,"conversationtranscriptid"),out var id) ? id : Guid.NewGuid(), TranscriptParser.Text(x,"content"), TranscriptParser.Text(x,"metadata")));
            List<TranscriptActivity> activities;
            try { activities = TranscriptParser.Parse(batches); }
            catch (JsonException) { warnings.Add("Une conversation contient un format non reconnu et n’a pas été analysée."); continue; }
            DateTimeOffset? pending = null;
            foreach (var activity in activities)
            {
                if (IsError(activity)) errors.Add(activity);
                if (!activity.Type.Equals("message", StringComparison.OrdinalIgnoreCase)) continue;
                if (activity.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                {
                    userMessages++; if (activity.UserId.Length > 0) users.Add(activity.UserId); pending = activity.Timestamp;
                }
                else if (activity.Role.Equals("bot", StringComparison.OrdinalIgnoreCase))
                {
                    botMessages++;
                    if (pending.HasValue && activity.Timestamp.HasValue)
                    {
                        var seconds = (activity.Timestamp.Value - pending.Value).TotalSeconds;
                        if (seconds >= 0 && seconds <= 3600) delays.Add(seconds);
                    }
                    pending = null;
                }
            }
        }
        return new(rows.Count, groups.Count(), userMessages, botMessages, users.Count, errors, delays, warnings);
    }
    public static bool IsError(TranscriptActivity activity)
    {
        if (activity.Type.Equals("error", StringComparison.OrdinalIgnoreCase) || activity.Type.Equals("exception", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            using var doc = JsonDocument.Parse(activity.Raw);
            bool Scan(JsonElement value)
            {
                if (value.ValueKind == JsonValueKind.Object)
                    foreach (var field in value.EnumerateObject())
                    {
                        if ((field.Name.Equals("DialogErrorDetail",StringComparison.OrdinalIgnoreCase) || field.Name.Equals("exception",StringComparison.OrdinalIgnoreCase)) && field.Value.ValueKind != JsonValueKind.Null && field.Value.ToString().Length > 0) return true;
                        if (Scan(field.Value)) return true;
                    }
                else if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().Any(Scan);
                return false;
            }
            return Scan(doc.RootElement);
        }
        catch (JsonException) { return false; }
    }
}

public sealed partial class DataverseClient
{
    public async Task<AgentAnalytics> AnalyticsAsync(Agent agent)
    {
        var since = DateTimeOffset.UtcNow.AddDays(-30).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ");
        var path = "conversationtranscripts?$select=conversationtranscriptid,name,conversationstarttime,metadata,content&$orderby=conversationstarttime%20desc&$top=50&$filter=" + Uri.EscapeDataString($"_bot_conversationtranscriptid_value eq {agent.Id:D} and conversationstarttime ge {since}");
        using var doc = JsonDocument.Parse(await SendAsync(HttpMethod.Get, path));
        return AgentAnalytics.FromRows(doc.RootElement.GetProperty("value").EnumerateArray());
    }
}
