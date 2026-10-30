using StudioDataverse;

var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
var a = new Agent(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Alpha Support", "demo_alpha", null, now.AddDays(-1), now.AddDays(-10), now.AddDays(-2), 0, 1036, false);
var b = new Agent(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Beta Sales", "demo_beta", null, now.AddDays(-60), now.AddDays(-100), null, 1, 1033, true);
var c = new Agent(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Gamma Support", "other_gamma", null, now.AddDays(-8), now.AddDays(-20), null, 0, 1036, false);
var d = new Agent(Guid.Parse("00000000-0000-0000-0000-000000000004"), "Unknown", "unknown", null);
Agent[] source = [a,b,c,d];
var checks = 0;
void Check(string title, AgentFilter filter, params Agent[] expected)
{
    var actual = AgentQuery.Apply(source, filter, now);
    if (!actual.Select(x => x.Id).SequenceEqual(expected.Select(x => x.Id))) throw new Exception(title + " failed");
    checks++;
}
Check("Combined filters", new(Search:"support", Status:0, Days:7, Language:1036, Managed:0), a);
Check("Multiple search terms across name and schema", new(Search:"demo SUPPORT"), a);
Check("Unknown dates excluded from period", new(Days:30), a,c);
Check("Published", new(Publication:1), a);
Check("Never published", new(Publication:2), c,b,d);
Check("Inactive", new(Status:1), b);
Check("Managed solution", new(Managed:1), b);
Check("Name descending", new(Sort:3), d,c,b,a);
Check("No results", new(Search:"missing"));
Check("Oldest first handles unknown date", new(Sort:1), d,b,c,a);
Check("Created recently", new(Sort:4), a,c,b,d);
Check("Search by identifier", new(Search:b.Id.ToString()), b);
Console.WriteLine($"{checks} scenario checks passed.");
using var client = new DataverseClient();
foreach (var foreignUrl in new[] { "https://example.com/api/data/v9.2/bots", "http://org86c36b58.crm17.dynamics.com/api/data/v9.2/bots", "https://org86c36b58.crm17.dynamics.com/not-dataverse", "https://org86c36b58.crm17.dynamics.com:4443/api/data/v9.2/bots", "https://user@org86c36b58.crm17.dynamics.com/api/data/v9.2/bots" })
{
    try { await client.SendAsync(HttpMethod.Get, foreignUrl); throw new Exception("Foreign API target accepted"); }
    catch (InvalidOperationException ex) when (ex.Message.Contains("API de l’environnement")) { }
}
Console.WriteLine("5 API target validation checks passed (no network requests).");
var prefs = new AgentPreferences();
if (!prefs.Toggle(a.Id) || prefs.Toggle(a.Id)) throw new Exception("Favorite toggle failed");
prefs.Toggle(b.Id);
for (var i = 0; i < 40; i++) prefs.Visit(Guid.NewGuid());
prefs.Visit(a.Id); prefs.Visit(a.Id);
if (prefs.Recent.Count != 30 || prefs.Recent[0] != a.Id || prefs.Recent.Distinct().Count() != 30) throw new Exception("Recent visits ordering/bound failed");
if (AgentPreferences.AccountKey("FIRST@example.test") != AgentPreferences.AccountKey(" first@example.test ") || AgentPreferences.AccountKey("first@example.test") == AgentPreferences.AccountKey("second@example.test")) throw new Exception("Account separation failed");
var prefsPath = Path.Combine(AppContext.BaseDirectory, "test-preferences.json");
await prefs.SaveAsync(prefsPath);
var restored = await AgentPreferences.LoadAsync(prefsPath);
if (!restored.Favorites.SetEquals(prefs.Favorites) || !restored.Recent.SequenceEqual(prefs.Recent)) throw new Exception("Preferences round trip failed");
Console.WriteLine("4 preference checks passed: favorites, recent history, account separation, persistence.");
var firstBatch = new TranscriptBatch(Guid.NewGuid(), """{"activities":[{"id":"1","type":"message","text":"Bonjour","from":{"role":"user","name":"Client"},"timestamp":1792929600},{"id":"2","type":"message","text":"Bienvenue","from":{"role":"bot"},"timestamp":"2026-10-25T12:00:01Z"}]}""", """{"BatchId":1}""");
var secondBatch = new TranscriptBatch(Guid.NewGuid(), """{"activities":[{"id":"2","type":"message","text":"Bienvenue","from":{"role":"bot"}},{"type":"message","text":"Merci"},{"type":"message","text":"Merci"},{"id":"3","type":"event","name":"SessionInfo","timestamp":1792929602000}]}""", """{"BatchId":"2"}""");
var parsed = TranscriptParser.Parse([secondBatch, firstBatch]);
if (parsed.Count != 5 || parsed[0].Text != "Bonjour" || parsed[0].Role != "user" || parsed[1].Role != "bot") throw new Exception("Batch sorting or de-duplication failed");
if (parsed.Count(x => x.Text == "Merci") != 2) throw new Exception("Distinct repeated messages were incorrectly merged");
if (parsed[0].Timestamp != now || parsed[^1].Timestamp != now.AddSeconds(2)) throw new Exception("Epoch seconds/milliseconds conversion failed");
var array = new TranscriptBatch(Guid.NewGuid(), """[{"type":"message","attachments":[{}]}]""", "{}");
if (!TranscriptParser.Parse([array])[0].Text.Contains("pièce jointe")) throw new Exception("Attachment fallback failed");
var nestedContent = System.Text.Json.JsonSerializer.Serialize(firstBatch.Content);
if (TranscriptParser.Parse([firstBatch with {Content=nestedContent}]).Count != 2) throw new Exception("Nested JSON content failed");
try { TranscriptParser.Parse([array with {Content="{broken"}]); throw new Exception("Invalid transcript not detected"); }
catch (System.Text.Json.JsonException) { }
var queryPath = Uri.UnescapeDataString(DataverseClient.TranscriptListPath(a.Id, now, now.AddDays(1)));
if (!queryPath.Contains("_bot_conversationtranscriptid_value eq " + a.Id) || !queryPath.Contains("conversationstarttime ge 2026-10-25T12:00:00Z") || !queryPath.Contains("conversationstarttime lt 2026-10-26T12:00:00Z") || queryPath.Contains("$select=content")) throw new Exception("Transcript listing filter failed");
Console.WriteLine("7 transcript checks passed: fragments, repeated messages, timestamps, attachments, nested JSON, malformed JSON, server filters.");
const string pixel = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/nhoAAAAASUVORK5CYII=";
if (AgentIconPayload.Decode(pixel) == null) throw new Exception("PNG icon rejected");
if (AgentIconPayload.Decode("data:image/png;base64," + pixel) == null) throw new Exception("PNG data URI rejected");
if (AgentIconPayload.Decode(null) != null || AgentIconPayload.Decode("invalid") != null || AgentIconPayload.Decode(Convert.ToBase64String("not a PNG image"u8.ToArray())) != null) throw new Exception("Invalid icon fallback failed");
if (AgentIconPayload.Decode(new string('A', 150001)) != null) throw new Exception("Oversized icon accepted");
var oversizedDimensions = Convert.FromBase64String(pixel);
System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(oversizedDimensions.AsSpan(16, 4), 999999);
if (AgentIconPayload.Decode(Convert.ToBase64String(oversizedDimensions)) != null) throw new Exception("Excessive PNG dimensions accepted");
Console.WriteLine("5 icon checks passed: PNG, data URI, invalid input fallback, payload limit, dimension limit.");
var componentId = Guid.NewGuid();
var componentJson = System.Text.Json.JsonSerializer.Serialize(new { value = new[] { new { botcomponentid = componentId, name = "Topic", componenttype = 9 } } });
using (var transport = new StubHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(componentJson) }))
using (var api = new DataverseClient(new HttpClient(transport), () => Task.FromResult("test-only")))
{
    var result = await api.ComponentsAsync(a);
    if (!result.Contains(componentId.ToString()) || transport.Paths.Count != 1 || !transport.Paths[0].Contains("/botcomponent_parent_bot?")) throw new Exception("Primary component relationship failed");
}
using (var transport = new StubHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("{\"value\":[]}") }, new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(componentJson) }))
using (var api = new DataverseClient(new HttpClient(transport), () => Task.FromResult("test-only")))
{
    var result = await api.ComponentsAsync(a);
    if (!result.Contains(componentId.ToString()) || transport.Paths.Count != 2 || !transport.Paths[1].Contains("/bot_botcomponent?")) throw new Exception("Linked component fallback failed");
}
using (var transport = new StubHandler(new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden) { Content = new StringContent("{\"error\":{\"message\":\"denied\"}}") }))
using (var api = new DataverseClient(new HttpClient(transport), () => Task.FromResult("test-only")))
{
    try { await api.ComponentsAsync(a); throw new Exception("403 should fail"); }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden) { }
}
var iconJson = System.Text.Json.JsonSerializer.Serialize(new { value = new[] { new { botid = a.Id, name = "Icon Agent", schemaname = "test_icon", iconbase64 = pixel } } });
using (var transport = new StubHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(iconJson) }))
using (var api = new DataverseClient(new HttpClient(transport), () => Task.FromResult("test-only")))
{
    var data = await api.ListAgentsAsync();
    if (!transport.Paths[0].Contains("iconbase64") || data[0].IconBase64 != pixel || AgentIconPayload.Decode(data[0].IconBase64) == null) throw new Exception("Agent icon API projection failed");
}
Console.WriteLine("4 HTTP integration checks passed: parent components, linked fallback, 403 propagation, agent icons (simulated transport only).");

var analyticsRows = System.Text.Json.JsonSerializer.Serialize(new[] { new { conversationtranscriptid = Guid.NewGuid(), name = "conversation-1", content = """{"activities":[{"id":"u1","type":"message","text":"Bonjour","from":{"role":"user","id":"person-1"},"timestamp":"2026-10-25T12:00:00Z"},{"id":"b1","type":"message","text":"Réponse","from":{"role":"bot"},"timestamp":"2026-10-25T12:00:02Z"},{"id":"e1","type":"event","channelData":{"DialogErrorDetail":{"code":"failure"}}},{"id":"e2","type":"message","text":"No error today","from":{"role":"bot"}}]}""", metadata = "{}" } });
using(var analyticsDoc = System.Text.Json.JsonDocument.Parse(analyticsRows))
{
    var metrics = AgentAnalytics.FromRows(analyticsDoc.RootElement.EnumerateArray());
    if (metrics.UserMessages != 1 || metrics.AgentMessages != 2 || metrics.IdentifiedUsers != 1 || metrics.Conversations != 1) throw new Exception("Usage counts failed");
    if(metrics.Errors.Count != 1 || metrics.MedianSeconds != 2 || metrics.P95Seconds != 2) throw new Exception("Error classification or response timing failed");
    var emptyMetrics = AgentAnalytics.FromRows([]);
    if(emptyMetrics.MedianSeconds != null || emptyMetrics.P95Seconds != null) throw new Exception("Missing performance must not become zero");
}
var badAnalytics = System.Text.Json.JsonSerializer.Serialize(new[] { new { conversationtranscriptid = Guid.NewGuid(), name = "bad", content = "bad json", metadata = "{}" } });
using(var badDoc=System.Text.Json.JsonDocument.Parse(badAnalytics))
    if(AgentAnalytics.FromRows(badDoc.RootElement.EnumerateArray()).Warnings.Count != 1) throw new Exception("Malformed transcript analytics warning failed");
using(var transport = new StubHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {Content = new StringContent("{\"value\":[]}")}))
using(var api = new DataverseClient(new HttpClient(transport), () => Task.FromResult("test-only")))
{
    await api.AnalyticsAsync(a);
    var decoded=Uri.UnescapeDataString(transport.Paths[0]);
    if(!decoded.Contains("$top=50") || !decoded.Contains(a.Id.ToString()) || !decoded.Contains("conversationstarttime ge")) throw new Exception("Analytics sample bounds failed");
}
Console.WriteLine("5 analytics checks passed: usage, errors/latency, missing data, malformed transcripts, bounded agent sample.");

var organizationRows = System.Text.Json.JsonSerializer.Serialize(new[] {
    new {conversationtranscriptid=Guid.NewGuid(), name="org-conversation-1", _bot_conversationtranscriptid_value=a.Id, metadata="{\"BotName\":\"Alpha\"}", content="""{"activities":[{"type":"message","from":{"role":"user","id":"same-user"},"timestamp":"2026-10-25T12:00:00Z"},{"type":"message","from":{"role":"user","id":"same-user"},"timestamp":"2026-10-25T13:00:00Z"}]}"""},
    new {conversationtranscriptid=Guid.NewGuid(), name="org-conversation-2", _bot_conversationtranscriptid_value=b.Id, metadata="{\"BotName\":\"Beta\"}", content="""{"activities":[{"type":"message","from":{"role":"user","id":"same-user"},"timestamp":"2026-10-25T12:00:00Z"},{"type":"message","from":{"role":"user"},"timestamp":"2026-10-25T13:00:00Z"}]}"""}
});
using(var orgDoc=System.Text.Json.JsonDocument.Parse(organizationRows))
{
    var metrics=OrganizationAnalytics.Parse(orgDoc.RootElement.EnumerateArray(),TimeZoneInfo.Utc);
    if(metrics.DailyUsers.Count!=1 || metrics.DailyUsers.Values.Single()!=1)throw new Exception("Organization daily users de-duplication failed");
    if(metrics.Trends.Count!=2 || metrics.Trends.Any(x=>x.Users!=1))throw new Exception("Per-agent user totals failed");
    if(metrics.DailyUsers.ContainsKey(new DateOnly(2026,10,24)))throw new Exception("Unobserved day incorrectly filled with zero");
}
using(var transport=new StubHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent("{\"value\":[]}")}))
using(var api=new DataverseClient(new HttpClient(transport),()=>Task.FromResult("test-only")))
{
    await api.OrganizationAnalyticsAsync();
    var path=Uri.UnescapeDataString(transport.Paths.Single());
    if(!path.Contains("$top=100")||!path.Contains("conversationstarttime ge")||!path.Contains("_bot_conversationtranscriptid_value ne null"))throw new Exception("Organization analytics query bounds failed");
}
Console.WriteLine("4 dashboard checks passed: unique users, per-agent ranking, missing days, bounded query.");

var pendingToken=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
using(var transport=new StubHandler())
using(var api=new DataverseClient(new HttpClient(transport),()=>pendingToken.Task))
{
    var request=api.SendAsync(HttpMethod.Get,"bots");
    await api.SignOutAsync();pendingToken.SetResult("test-only");
    try{await request;throw new Exception("Stale authentication request was accepted");}
    catch(OperationCanceledException){}
    if(transport.Paths.Count!=0)throw new Exception("Stale request reached transport");
}
using(var transport=new DeferredHandler())
using(var api=new DataverseClient(new HttpClient(transport),()=>Task.FromResult("test-only")))
{
    var traces=0;api.Traced+=_=>traces++;
    var request=api.SendAsync(HttpMethod.Get,"bots");
    await transport.Started.Task;
    await api.SignOutAsync();transport.Response.SetResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent("{\"value\":[]}")});
    try{await request;throw new Exception("Old account response accepted");}
    catch(OperationCanceledException){}
    if(traces!=0)throw new Exception("Old account response leaked into journal");
}
await File.WriteAllTextAsync(prefsPath,"{\"Favorites\":null,\"Recent\":null}");
var emptyPrefs=await AgentPreferences.LoadAsync(prefsPath);
if(emptyPrefs.Favorites.Count!=0||emptyPrefs.Recent.Count!=0)throw new Exception("Null preferences not normalized");
Console.WriteLine("3 finalization checks passed: pending token cancellation, old-account response isolation, null preferences recovery.");

Console.WriteLine((await OperationsTests.Run())+" operations checks passed.");

Console.WriteLine((await AdvancedTests.Run())+" advanced checks passed." );

sealed class DeferredHandler:HttpMessageHandler
{
    public TaskCompletionSource<bool> Started{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource<HttpResponseMessage> Response{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){Started.TrySetResult(true);return Response.Task;}
}
sealed class StubHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> pending = new(responses);
    public List<string> Paths { get; } = [];
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Paths.Add(request.RequestUri!.PathAndQuery);
        return Task.FromResult(pending.Dequeue());
    }
}


