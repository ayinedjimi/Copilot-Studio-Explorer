using System.Globalization;
using System.Text.Json;

namespace StudioDataverse;

public record TranscriptRecord(Guid Id, string Name, Guid? BotId, string AgentName, DateTimeOffset? Start, string Metadata)
{
    public string StartLabel => Start?.ToLocalTime().ToString("dd/MM/yyyy · HH:mm") ?? "Date non renseignée";
    public string Subtitle => AgentName + " · " + StartLabel;
    public override string ToString() => Name + "\n" + Subtitle;
}
public record TranscriptPage(List<TranscriptRecord> Items, string? Next);
public record TranscriptBatch(Guid Id, string Content, string Metadata);
public record TranscriptActivity(string Id, string Role, string Sender, string Type, string Text, DateTimeOffset? Timestamp, string Raw, string UserId = "");

public sealed partial class DataverseClient
{
    public static string TranscriptListPath(Guid? agent, DateTimeOffset? from, DateTimeOffset? to)
    {
        var filters = new List<string>();
        if (agent.HasValue) filters.Add($"_bot_conversationtranscriptid_value eq {agent.Value:D}");
        if (from.HasValue) filters.Add("conversationstarttime ge " + from.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        if (to.HasValue) filters.Add("conversationstarttime lt " + to.Value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        return "conversationtranscripts?$select=conversationtranscriptid,name,conversationstarttime,metadata,_bot_conversationtranscriptid_value&$orderby=conversationstarttime%20desc,conversationtranscriptid" +
            (filters.Count == 0 ? "" : "&$filter=" + Uri.EscapeDataString(string.Join(" and ", filters)));
    }
    public async Task<TranscriptPage> ListTranscriptsAsync(string path)
    {
        using var doc = JsonDocument.Parse(await SendAsync(HttpMethod.Get, path));
        var items = doc.RootElement.GetProperty("value").EnumerateArray().Select(x =>
        {
            var metadata = TranscriptParser.Text(x, "metadata");
            var agentName = TranscriptParser.Text(x, "_bot_conversationtranscriptid_value@OData.Community.Display.V1.FormattedValue");
            if (agentName.Length == 0) agentName = TranscriptParser.MetadataText(metadata, "BotName");
            return new TranscriptRecord(x.GetProperty("conversationtranscriptid").GetGuid(), TranscriptParser.Text(x, "name"),
                x.TryGetProperty("_bot_conversationtranscriptid_value", out var bot) && bot.ValueKind == JsonValueKind.String && Guid.TryParse(bot.GetString(), out var id) ? id : null,
                agentName.Length > 0 ? agentName : "Agent non renseigné", Date(x, "conversationstarttime"), metadata);
        }).ToList();
        return new(items, doc.RootElement.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() : null);
    }
    public async Task<TranscriptBatch> TranscriptAsync(Guid id)
    {
        using var doc = JsonDocument.Parse(await SendAsync(HttpMethod.Get, $"conversationtranscripts({id:D})?$select=conversationtranscriptid,content,metadata"));
        return Batch(doc.RootElement);
    }
    public async Task<List<TranscriptBatch>> TranscriptFragmentsAsync(TranscriptRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.Name) || !record.Start.HasValue)
            throw new InvalidOperationException("Le nom et l’heure de début sont nécessaires pour retrouver les fragments.");
        var filter = "name eq '" + record.Name.Replace("'", "''") + "' and conversationstarttime eq " + record.Start.Value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        if (record.BotId.HasValue) filter += $" and _bot_conversationtranscriptid_value eq {record.BotId:D}";
        string? path = "conversationtranscripts?$select=conversationtranscriptid,content,metadata&$filter=" + Uri.EscapeDataString(filter);
        var result = new List<TranscriptBatch>();
        do
        {
            using var doc = JsonDocument.Parse(await SendAsync(HttpMethod.Get, path));
            result.AddRange(doc.RootElement.GetProperty("value").EnumerateArray().Select(Batch));
            path = doc.RootElement.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() : null;
            if (result.Count > 200) throw new InvalidOperationException("La conversation dépasse 200 fragments. Affichez les enregistrements séparément.");
        } while (path != null);
        return result;
    }
    private static TranscriptBatch Batch(JsonElement x) => new(x.GetProperty("conversationtranscriptid").GetGuid(), TranscriptParser.Text(x, "content"), TranscriptParser.Text(x, "metadata"));
}

public static class TranscriptParser
{
    public static string Text(JsonElement item, string key)
    {
        if (item.ValueKind != JsonValueKind.Object) return "";
        foreach (var property in item.EnumerateObject())
            if (property.Name.Equals(key, StringComparison.OrdinalIgnoreCase)) return property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? "" : property.Value.ToString();
        return "";
    }
    public static string MetadataText(string metadata, string key)
    {
        try { using var doc = JsonDocument.Parse(metadata); return Text(doc.RootElement, key); }
        catch (JsonException) { return ""; }
    }
    public static int BatchNumber(TranscriptBatch batch) => int.TryParse(MetadataText(batch.Metadata, "BatchId"), out var number) ? number : 0;
    public static List<TranscriptActivity> Parse(IEnumerable<TranscriptBatch> batches)
    {
        var activities = new List<TranscriptActivity>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var batch in batches.OrderBy(BatchNumber))
        {
            if (string.IsNullOrWhiteSpace(batch.Content)) continue;
            using var doc = JsonDocument.Parse(batch.Content);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                using var nested = JsonDocument.Parse(root.GetString()!);
                root = nested.RootElement.Clone();
            }
            var array = root;
            if (root.ValueKind == JsonValueKind.Object)
            {
                var property = root.EnumerateObject().FirstOrDefault(p => p.Name.Equals("activities", StringComparison.OrdinalIgnoreCase));
                array = property.Value;
            }
            if (array.ValueKind != JsonValueKind.Array) throw new JsonException("Le contenu ne contient pas de tableau activities reconnu.");
            foreach (var activity in array.EnumerateArray())
            {
                if (activity.ValueKind != JsonValueKind.Object) continue;
                var id = Text(activity, "id");
                if (id.Length > 0 && !ids.Add(id)) continue;
                var from = activity.EnumerateObject().FirstOrDefault(p => p.Name.Equals("from", StringComparison.OrdinalIgnoreCase)).Value;
                var role = Text(from, "role");
                var sender = Text(from, "name");
                var type = Text(activity, "type");
                var text = Text(activity, "text");
                if (text.Length == 0) text = Text(activity, "speak");
                if (text.Length == 0) text = Text(activity, "name");
                if (text.Length == 0 && activity.TryGetProperty("attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Array && attachments.GetArrayLength() > 0)
                    text = $"[Carte ou pièce jointe : {attachments.GetArrayLength()}]";
                DateTimeOffset? timestamp = null;
                var time = Text(activity, "timestamp");
                if (double.TryParse(time, NumberStyles.Float, CultureInfo.InvariantCulture, out var epoch))
                {
                    try { timestamp = Math.Abs(epoch) > 100_000_000_000 ? DateTimeOffset.FromUnixTimeMilliseconds((long)epoch) : DateTimeOffset.FromUnixTimeSeconds((long)epoch); }
                    catch (ArgumentOutOfRangeException) { }
                }
                else if (DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)) timestamp = date;
                activities.Add(new(id, role, sender, type, text, timestamp, activity.GetRawText(), Text(from, "id")));
            }
        }
        return activities;
    }
}
