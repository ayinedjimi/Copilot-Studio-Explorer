using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StudioDataverse;

public sealed class AgentPreferences
{
    public HashSet<Guid> Favorites { get; set; } = [];
    public List<Guid> Recent { get; set; } = [];
    public void Visit(Guid id) { Recent.Remove(id); Recent.Insert(0, id); if (Recent.Count > 30) Recent.RemoveRange(30, Recent.Count - 30); }
    public bool Toggle(Guid id) { if (Favorites.Remove(id)) return false; Favorites.Add(id); return true; }
    public static string AccountKey(string username) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(username.Trim().ToLowerInvariant())));
    public static string PathFor(string username,string? organization=null) => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StudioDataverse", "preferences-" + (organization==null||organization.TrimEnd('/').Equals(DataverseClient.Organization,StringComparison.OrdinalIgnoreCase)?"":AccountKey(organization.TrimEnd('/'))[..16]+"-") + AccountKey(username) + ".json");
    public static async Task<AgentPreferences> LoadAsync(string path)
    {
        if (!File.Exists(path)) return new();
        try
        {
            var preferences=JsonSerializer.Deserialize<AgentPreferences>(await File.ReadAllTextAsync(path))??new();
            preferences.Favorites??=[];
            preferences.Recent=(preferences.Recent??[]).Distinct().Take(30).ToList();
            return preferences;
        }
        catch (JsonException) { return new(); }
    }
    public async Task SaveAsync(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(this));
        File.Move(temp, path, true);
    }
}
