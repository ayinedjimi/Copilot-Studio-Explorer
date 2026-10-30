using System.Text.Json;
using System.Text.RegularExpressions;

namespace StudioDataverse;

public sealed record AppSettings
{
    public string Organization { get; init; } = DataverseClient.Organization;
    public string TenantId { get; init; } = "8ebfeed3-cf5d-4d41-b2ed-35f5369dda1c";
    public string ClientId { get; init; } = "ff6816da-a957-4e33-ba4a-23b15d60b774";
    public string EnvironmentName { get; init; } = "Formation";
    public int RequestTimeoutSeconds { get; init; } = 45;
    public int TranscriptDays { get; init; } = 30;
    public int IncidentSample { get; init; } = 100;
    public int IncidentDays { get; init; } = 30;
    public int DefaultQuestionCount { get; init; } = 5;
    public string ScenarioRegion { get; init; } = "Europe";
    public int ModelContext { get; init; } = 4096;
    public int ModelOutputTokens { get; init; } = 512;
    public int ModelTimeoutSeconds { get; init; } = 180;
    public int ModelThreads { get; init; }
    public int SidebarWidth { get; init; } = 238;
    public bool ErrorLogEnabled { get; init; } = true;
    public string DefaultModelPath { get; init; } = "";
    public string DefaultModelFormat { get; init; } = "ChatML";
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CopilotStudioExplorer","settings.json");
    public void Validate()
    {
        if(!DataverseClient.IsValidOrganization(Organization))throw new InvalidOperationException("L’URL doit être une adresse HTTPS racine Dataverse valide.");
        if(!Guid.TryParse(TenantId,out _)||!Guid.TryParse(ClientId,out _))throw new InvalidOperationException("Les identifiants du tenant et de l’application doivent être des GUID valides.");
        if(string.IsNullOrWhiteSpace(EnvironmentName)||EnvironmentName.Length>80)throw new InvalidOperationException("Renseignez un nom d’environnement de 80 caractères maximum.");
        if(RequestTimeoutSeconds is <10 or >300||TranscriptDays is <1 or >365||IncidentDays is <1 or >365||IncidentSample is not (50 or 100 or 250 or 500)||DefaultQuestionCount is not (3 or 5 or 10)||ScenarioRegion is not ("Europe" or "Global" or "Inde"))throw new InvalidOperationException("Les paramètres de connexion ou de collecte sont hors limites.");
        if(ModelContext is <1024 or >8192||ModelOutputTokens is <64 or >2048||ModelTimeoutSeconds is <30 or >900||ModelThreads is <0 or >32||SidebarWidth is <200 or >360)throw new InvalidOperationException("Les paramètres du modèle ou de la navigation sont hors limites.");
        if(DefaultModelFormat is not ("ChatML" or "Gemma 2" or "Gemma 4"))throw new InvalidOperationException("Format de modèle inconnu.");
        if(!string.IsNullOrWhiteSpace(DefaultModelPath)&&(!Path.IsPathFullyQualified(DefaultModelPath)||!DefaultModelPath.EndsWith(".gguf",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Le modèle par défaut doit être un fichier GGUF local.");
    }
    public static AppSettings Load(out string error,string? path=null)
    {
        error="";path??=DefaultPath;if(!File.Exists(path))return new();
        try{if(new FileInfo(path).Length>100_000)throw new InvalidOperationException("Fichier de paramètres trop volumineux.");var result=JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))??throw new InvalidOperationException("Fichier vide.");result.Validate();return result;}
        catch(Exception ex) when(ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException){error="Paramètres invalides ou illisibles : ouvrez Paramètres et enregistrez une configuration valide avant de vous connecter.";return new();}
    }
    public async Task SaveAsync(string? path=null)
    {
        Validate();path??=DefaultPath;Directory.CreateDirectory(Path.GetDirectoryName(path)!);var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
