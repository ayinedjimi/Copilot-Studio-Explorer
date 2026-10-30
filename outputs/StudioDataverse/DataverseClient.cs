using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;

namespace StudioDataverse;

public record Agent(Guid Id, string Name, string Schema, string? ETag, DateTimeOffset? ModifiedOn = null,
    DateTimeOffset? CreatedOn = null, DateTimeOffset? PublishedOn = null, int? State = null, int? Language = null, bool? Managed = null, string? IconBase64 = null, string? Creator = null)
{
    public override string ToString() => Name;
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "A" : Name[..1].ToUpperInvariant();
    public string ModifiedLabel => ModifiedOn?.ToLocalTime().ToString("dd/MM/yyyy · HH:mm") ?? "—";
    public string StatusLabel => State switch { 0 => "Actif", 1 => "Inactif", _ => "Non renseigné" };
}
public record ApiTrace(string Method, string Url, int? Status, long Milliseconds, string Request, string Response, int Session = 0);

public sealed partial class DataverseClient : IDisposable
{
    public const string Organization = "https://org86c36b58.crm17.dynamics.com";
    public const string EnvironmentId = "96e7335b-3367-efa8-a70f-ea65680c1a5c";
    private readonly IPublicClientApplication identity = PublicClientApplicationBuilder
        .Create("ff6816da-a957-4e33-ba4a-23b15d60b774")
        .WithAuthority("https://login.microsoftonline.com/8ebfeed3-cf5d-4d41-b2ed-35f5369dda1c")
        .WithRedirectUri("http://localhost")
        .Build();
    private readonly HttpClient http;
    // Public desktop clients request the delegated Dataverse scope explicitly.
    // This also permits dynamic consent instead of relying on static /.default permissions.
    private readonly string[] scopes;
    private IAccount? currentAccount;
    private int sessionGeneration;
    public int SessionGeneration=>Volatile.Read(ref sessionGeneration);
    private readonly Func<Task<string>>? testTokenProvider;
    public DataverseClient():this(Organization) { }
    public DataverseClient(string organization)
    {
        if(!Uri.TryCreate(organization.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||uri.AbsolutePath!="/"||!System.Text.RegularExpressions.Regex.IsMatch(uri.Host,@"^[a-z0-9-]+\.crm\d*\.dynamics\.com$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new InvalidOperationException("URL Dataverse invalide. Utilisez l’URL HTTPS racine de l’organisation.");
        var origin=uri.GetLeftPart(UriPartial.Authority);
        http=new HttpClient{BaseAddress=new Uri(origin+"/api/data/v9.2/"),Timeout=TimeSpan.FromSeconds(45)};
        scopes=[origin+"/user_impersonation"];
    }
    internal DataverseClient(HttpClient transport, Func<Task<string>> tokenProvider)
    {
        http = transport;
        scopes=[Organization+"/user_impersonation"];
        http.BaseAddress = new Uri(Organization + "/api/data/v9.2/");
        testTokenProvider = tokenProvider;
    }
    public string? NextAgentsPath { get; private set; }
    public long? TotalAgents { get; private set; }
    public event Action<ApiTrace>? Traced;

    public async Task<string> SignInAsync(CancellationToken cancellationToken)
    {
        var result = await identity.AcquireTokenInteractive(scopes)
            .WithUseEmbeddedWebView(false)
            .WithPrompt(Prompt.SelectAccount)
            .ExecuteAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref sessionGeneration);
        currentAccount = result.Account;
        return result.Account.Username;
    }

    public async Task SignOutAsync()
    {
        Interlocked.Increment(ref sessionGeneration);
        currentAccount=null;
        foreach (var account in await identity.GetAccountsAsync()) await identity.RemoveAsync(account);
        currentAccount = null;
        NextAgentsPath = null;
        TotalAgents = null;
    }

    public async Task<string> SendAsync(HttpMethod method, string path, string? body = null, string? etag = null, CancellationToken cancellationToken = default, bool redactTrace = false)
    {
        var session=SessionGeneration;
        var target = new Uri(http.BaseAddress!, path);
        if (target.Scheme != "https" || !target.IsDefaultPort || target.UserInfo.Length > 0 || target.Host != http.BaseAddress!.Host || !target.AbsolutePath.StartsWith("/api/data/v9.2/", StringComparison.Ordinal))
            throw new InvalidOperationException("La requête doit cibler l’API de l’environnement configuré.");
        string token;
        if (testTokenProvider != null) token = await testTokenProvider();
        else
        {
            if (currentAccount == null) throw new InvalidOperationException("Connectez-vous à Microsoft avec le bouton en haut à gauche.");
            var auth = await identity.AcquireTokenSilent(scopes, currentAccount).ExecuteAsync(cancellationToken);
            token = auth.AccessToken;
        }
        EnsureSession(session);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Add("OData-MaxVersion", "4.0");
        request.Headers.Add("OData-Version", "4.0");
        request.Headers.TryAddWithoutValidation("Prefer", "odata.maxpagesize=100, odata.include-annotations=\"OData.Community.Display.V1.FormattedValue\"");
        if (etag != null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var timer = Stopwatch.StartNew();
        try
        {
            using var response = await http.SendAsync(request,cancellationToken);
            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            EnsureSession(session);
            Traced?.Invoke(new(method.Method, new Uri(http.BaseAddress!, path).ToString(), (int)response.StatusCode, timer.ElapsedMilliseconds, body ?? "(aucun corps)", redactTrace?"Réponse des modules masquée : données de configuration non exposées dans le journal API.":Pretty(result),session));
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Dataverse a répondu {(int)response.StatusCode}. Consultez la réponse dans le panneau API.", null, response.StatusCode);
            return result;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == null)
        {
            // HTTP responses are already traced. Only transport failures need an additional entry.
            if (session==SessionGeneration&&!ex.Message.StartsWith("Dataverse a répondu", StringComparison.Ordinal))
                Traced?.Invoke(new(method.Method, new Uri(http.BaseAddress!, path).ToString(), null, timer.ElapsedMilliseconds, body ?? "(aucun corps)", "Erreur réseau : " + ex.Message,session));
            throw;
        }
    }
    private void EnsureSession(int session)
    {
        if(session!=SessionGeneration)throw new OperationCanceledException("La session a changé. La réponse de l’ancien compte a été ignorée.");
    }

    public async Task<List<Agent>> ListAgentsAsync()
    {
        NextAgentsPath = null;
        TotalAgents = null;
        return await AgentPageAsync("bots?$select=botid,name,schemaname,modifiedon,createdon,publishedon,statecode,language,ismanaged,iconbase64,_createdby_value&$count=true&$orderby=modifiedon%20desc,botid");
    }
    public Task<List<Agent>> MoreAgentsAsync() => NextAgentsPath is { } path ? AgentPageAsync(path) : Task.FromResult(new List<Agent>());
    private async Task<List<Agent>> AgentPageAsync(string path)
    {
        using var doc = JsonDocument.Parse(await SendAsync(HttpMethod.Get, path));
        NextAgentsPath = doc.RootElement.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() : null;
        if (doc.RootElement.TryGetProperty("@odata.count", out var count) && count.TryGetInt64(out var total)) TotalAgents = total;
        return doc.RootElement.GetProperty("value").EnumerateArray().Select(x => new Agent(
            x.GetProperty("botid").GetGuid(), x.GetProperty("name").GetString() ?? "Sans nom",
            x.GetProperty("schemaname").GetString() ?? "",
            x.TryGetProperty("@odata.etag", out var tag) ? tag.GetString() : null,
            Date(x,"modifiedon"), Date(x,"createdon"), Date(x,"publishedon"),
            Number(x,"statecode"), Number(x,"language"),
            x.TryGetProperty("ismanaged", out var managed) && managed.ValueKind is JsonValueKind.True or JsonValueKind.False ? managed.GetBoolean() : null,
            x.TryGetProperty("iconbase64", out var icon) && icon.ValueKind == JsonValueKind.String ? icon.GetString() : null,
            x.TryGetProperty("_createdby_value@OData.Community.Display.V1.FormattedValue", out var creator) ? creator.GetString() : null)).ToList();
    }
    private static DateTimeOffset? Date(JsonElement item, string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var date) ? date : null;
    private static int? Number(JsonElement item, string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : null;
    public Task<string> DetailsAsync(Agent agent) => SendAsync(HttpMethod.Get, $"bots({agent.Id:D})");
    public Task<string> ComponentDetailsAsync(Guid id) => SendAsync(HttpMethod.Get, $"botcomponents({id:D})");

    public Task<string> RenameAsync(Agent agent, string name) => SendAsync(HttpMethod.Patch, $"bots({agent.Id:D})", JsonSerializer.Serialize(new { name }), agent.ETag ?? "*");
    public async Task<string> ComponentsAsync(Agent agent)
    {
        var primary = await SendAsync(HttpMethod.Get, $"bots({agent.Id:D})/botcomponent_parent_bot?$select=botcomponentid,name,componenttype,schemaname,statecode,statuscode&$top=100");
        using var document = JsonDocument.Parse(primary);
        if (document.RootElement.GetProperty("value").GetArrayLength() > 0) return primary;
        return await SendAsync(HttpMethod.Get, $"bots({agent.Id:D})/bot_botcomponent?$select=botcomponentid,name,componenttype,schemaname,statecode,statuscode&$top=100");
    }
    private static string Pretty(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "(réponse sans corps)";
        try { using var doc = JsonDocument.Parse(value); return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }); }
        catch (JsonException) { return value; }
    }
    public void Dispose() => http.Dispose();
}
