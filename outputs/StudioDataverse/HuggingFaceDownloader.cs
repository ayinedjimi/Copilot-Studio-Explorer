using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StudioDataverse;
public record HubModelFile(string Repository,string Revision,string FileName,long Size,string Sha256);
public static class HuggingFaceDownloader
{
    public const long MaximumBytes=8_000_000_000;
    public static bool ValidRepository(string repo)=>Regex.IsMatch(repo,@"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,95}/[A-Za-z0-9][A-Za-z0-9_.-]{0,95}\z")&&!repo.Contains("..");
    public static bool ValidFile(string name)=>Regex.IsMatch(name,@"\A[A-Za-z0-9][A-Za-z0-9_.-]{0,160}\.gguf\z",RegexOptions.IgnoreCase)&&!name.Contains("..")&&!name.StartsWith("mmproj",StringComparison.OrdinalIgnoreCase);
    private static bool TrustedDownloadUri(Uri uri)=>uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&(uri.Host.Equals("huggingface.co",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".huggingface.co",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("hf.co",StringComparison.OrdinalIgnoreCase)||uri.Host.EndsWith(".hf.co",StringComparison.OrdinalIgnoreCase));
    private static async Task<HttpResponseMessage> OpenDownloadAsync(HttpClient client,Uri target,CancellationToken token)
    {
        for(int redirects=0;redirects<=6;redirects++){
            if(!TrustedDownloadUri(target))throw new InvalidOperationException("Redirection de téléchargement non autorisée.");
            using var request=new HttpRequestMessage(HttpMethod.Get,target);HttpResponseMessage response;
            try{response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token);}catch(HttpRequestException){throw new InvalidOperationException("Connexion au service de téléchargement impossible.");}
            if((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308))return response;
            var location=response.Headers.Location;response.Dispose();if(location==null)throw new InvalidOperationException("Redirection de téléchargement invalide.");target=location.IsAbsoluteUri?location:new Uri(target,location);
        }
        throw new InvalidOperationException("Trop de redirections de téléchargement.");
    }
    public static HubModelFile ParseMetadata(string repository,string filename,string json)
    {
        if(!ValidRepository(repository)||!ValidFile(filename))throw new InvalidOperationException("Dépôt ou nom de fichier GGUF invalide.");
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        var revision=root.GetProperty("sha").GetString()??"";if(!Regex.IsMatch(revision,@"\A[0-9a-fA-F]{40}\z"))throw new InvalidOperationException("Révision du dépôt invalide.");
        foreach(var item in root.GetProperty("siblings").EnumerateArray())if(item.TryGetProperty("rfilename",out var n)&&n.GetString()==filename){
            if(!item.TryGetProperty("lfs",out var lfs)||!lfs.TryGetProperty("sha256",out var digest)||!lfs.TryGetProperty("size",out var size))throw new InvalidOperationException("Empreinte du fichier indisponible.");
            var hash=digest.GetString()??"";var bytes=size.GetInt64();if(!Regex.IsMatch(hash,@"\A[0-9a-fA-F]{64}\z")||bytes<=0||bytes>MaximumBytes)throw new InvalidOperationException("Empreinte invalide ou fichier supérieur à 8 Go.");
            return new(repository,revision,filename,bytes,hash.ToLowerInvariant());
        }
        throw new InvalidOperationException("Fichier GGUF introuvable dans ce dépôt public.");
    }
    public static async Task<HubModelFile> GetMetadataAsync(HttpClient client,string repo,string file,CancellationToken token)
    {
        if(!ValidRepository(repo)||!ValidFile(file))throw new InvalidOperationException("Saisissez un dépôt auteur/nom et un nom de fichier GGUF simple.");
        using var response=await client.GetAsync("https://huggingface.co/api/models/"+repo+"?blobs=true",HttpCompletionOption.ResponseHeadersRead,token);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Dépôt inaccessible ("+(int)response.StatusCode+"). Les dépôts privés ou soumis à autorisation ne sont pas pris en charge.");
        if(response.Content.Headers.ContentLength is >5_000_000)throw new InvalidOperationException("Métadonnées du dépôt trop volumineuses.");
        var json=await response.Content.ReadAsStringAsync(token);if(json.Length>5_000_000)throw new InvalidOperationException("Métadonnées du dépôt trop volumineuses.");
        return ParseMetadata(repo,file,json);
    }
    public static async Task<string> DownloadAsync(HttpClient client,HubModelFile file,string folder,Action<long>? progress,CancellationToken token)
    {
        if(!ValidRepository(file.Repository)||!ValidFile(file.FileName)||!Regex.IsMatch(file.Revision,@"\A[0-9a-fA-F]{40}\z")||!Regex.IsMatch(file.Sha256,@"\A[0-9a-fA-F]{64}\z")||file.Size<=0||file.Size>MaximumBytes)throw new InvalidOperationException("Métadonnées du modèle invalides.");
        Directory.CreateDirectory(folder);var destination=Path.Combine(folder,file.FileName);
        if(File.Exists(destination))throw new InvalidOperationException("Un fichier de ce nom existe déjà dans le dossier des modèles.");
        var free=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))!).AvailableFreeSpace;if(free<file.Size+256_000_000)throw new InvalidOperationException("Espace disque insuffisant pour ce modèle.");
        var temporary=Path.Combine(folder,".download-"+Guid.NewGuid().ToString("N")+".tmp");
        try{
            using var response=await OpenDownloadAsync(client,new Uri("https://huggingface.co/"+file.Repository+"/resolve/"+file.Revision+"/"+Uri.EscapeDataString(file.FileName)),token);
            if(!response.IsSuccessStatusCode)throw new InvalidOperationException("Téléchargement refusé ("+(int)response.StatusCode+"). Vérifiez les conditions d’accès au modèle.");
            if(response.Content.Headers.ContentLength is long length&&length!=file.Size)throw new InvalidOperationException("Taille du modèle différente des métadonnées.");
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);await using var input=await response.Content.ReadAsStreamAsync(token);await using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*1024,true)){
                var buffer=new byte[1024*1024];long total=0;int read;while((read=await input.ReadAsync(buffer,token))>0){total+=read;if(total>file.Size)throw new InvalidOperationException("Le fichier reçu dépasse la taille annoncée.");hash.AppendData(buffer,0,read);await output.WriteAsync(buffer.AsMemory(0,read),token);progress?.Invoke(total);}
                if(total!=file.Size||!Convert.ToHexString(hash.GetHashAndReset()).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Taille ou empreinte du modèle incorrecte.");
            }
            token.ThrowIfCancellationRequested();File.Move(temporary,destination);return destination;
        }finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
