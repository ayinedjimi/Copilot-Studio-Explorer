using System.Net;
using System.Text.Json;
using StudioDataverse;
static class AdvancedTests
{
    public static async Task<int> Run()
    {
        int count=0;void Check(bool yes,string title){if(!yes)throw new Exception(title);count++;Console.WriteLine("PASS "+title);}
        var id=Guid.NewGuid();var component=Guid.NewGuid();
        var settingsPath=Path.Combine(Path.GetTempPath(),"explorer-settings-"+Guid.NewGuid().ToString("N")+".json");
        var configuration=new AppSettings{EnvironmentName="Démo",Organization="https://example.crm17.dynamics.com",TranscriptDays=45,ModelContext=2048};await configuration.SaveAsync(settingsPath);
        var restored=AppSettings.Load(out var settingsError,settingsPath);Check(settingsError==""&&restored.Organization==configuration.Organization&&restored.TranscriptDays==45&&restored.ModelContext==2048,"Settings round trip with configurable environment and model options");
        try{(configuration with{Organization="https://example.crm17.dynamics.com.evil.test"}).Validate();throw new Exception("Invalid organization accepted");}catch(InvalidOperationException){Check(true,"Settings reject foreign Dataverse hosts");}
        try{(configuration with{ModelThreads=99}).Validate();throw new Exception("Invalid thread count accepted");}catch(InvalidOperationException){Check(true,"Settings reject invalid runtime limits");}
        await File.WriteAllTextAsync(settingsPath,"not-json");AppSettings.Load(out var badSettings,settingsPath);Check(badSettings.Length>0,"Invalid settings are reported rather than silently applied");File.Delete(settingsPath);
        var agent=JsonSerializer.SerializeToElement(new{botid=id,name="Support",authenticationmode=2});
        var components=new List<JsonElement>{JsonSerializer.SerializeToElement(new{botcomponentid=component,name="Accueil",schemaname="topic",componenttype=15,data="kind: GptComponentMetadata\ninstructions: Répondre en français"})};
        var inspection=ExplorerInspection.Create(id,"Support",agent,components,[],[],[],false,"Actif");
        var rules=new[]{new CustomRule("1","Nom","Nom","Commence par","sup"),new CustomRule("2","Interdit","Instructions","Ne contient pas","secret"),new CustomRule("3","Absent","Instructions","Contient","MOT INTROUVABLE")};
        var results=AdvancedFunctions.EvaluateRules(rules,[inspection]);
        Check(results.Count==3&&results[0].State==FindingState.Conforme&&results[1].State==FindingState.Conforme&&results[2].State==FindingState.AExaminer,"Custom rules evaluate supported operations case-insensitively");
        var unavailable=inspection with{Missing=["components"],Components=[]};
        Check(AdvancedFunctions.EvaluateRules(rules,[unavailable]).Single(x=>x.Rule.Id=="2").State==FindingState.NonVerifiable,"Missing content cannot pass a negative custom rule");
        Check(AdvancedFunctions.Search([inspection],"français").Single().Field=="Configuration","Global search locates configuration text");
        Check(AdvancedFunctions.Search([inspection],"").Count==0,"Empty search does not scan everything");
        var imported=AdvancedFunctions.ParseImportedRules("[{\"name\":\"Règle\",\"field\":\"Nom\",\"operation\":\"Contient\",\"value\":\"Support\"}]");
        Check(imported.Count==1&&AdvancedFunctions.EvaluateRules(imported,[inspection])[0].State==FindingState.Conforme,"Imported rule is evaluated against inspected agent data");
        try{AdvancedFunctions.ParseImportedRules("[{\"name\":\"Règle\",\"field\":\"Secret\",\"operation\":\"Contient\",\"value\":\"x\"}]");throw new Exception("Invalid field accepted");}catch(InvalidOperationException){Check(true,"Imported rule rejects unsupported fields");}
        try{AdvancedFunctions.ParseImportedRules("[{\"name\":\"A\",\"field\":\"Nom\",\"operation\":\"Contient\",\"value\":\"x\"},{\"name\":\"a\",\"field\":\"Nom\",\"operation\":\"Contient\",\"value\":\"y\"}]");throw new Exception("Duplicate name accepted");}catch(InvalidOperationException){Check(true,"Imported rules reject duplicate names");}
        var payload=System.Text.Encoding.UTF8.GetBytes("safe test model");var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload)).ToLowerInvariant();var revision=new string('a',40);
        var metadata=HuggingFaceDownloader.ParseMetadata("author/model","example.gguf",JsonSerializer.Serialize(new{sha=revision,siblings=new[]{new{rfilename="example.gguf",lfs=new{sha256=digest,size=payload.Length}}}}));
        Check(metadata.Size==payload.Length&&!HuggingFaceDownloader.ValidRepository("../bad/name")&&!HuggingFaceDownloader.ValidFile("../model.gguf"),"Model metadata validates repository and filename");
        var downloadDir=Path.Combine(Path.GetTempPath(),"ExplorerModelCheck-"+Guid.NewGuid().ToString("N"));
        using(var modelClient=new HttpClient(new Fake(request=>new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request,Content=new ByteArrayContent(payload)}))){var path=await HuggingFaceDownloader.DownloadAsync(modelClient,metadata,downloadDir,null,CancellationToken.None);Check(File.ReadAllBytes(path).SequenceEqual(payload),"Model download verifies the actual bytes");
            try{await HuggingFaceDownloader.DownloadAsync(modelClient,metadata with{FileName="bad.gguf",Sha256=new string('0',64)},downloadDir,null,CancellationToken.None);throw new Exception("Bad checksum accepted");}catch(InvalidOperationException){Check(!File.Exists(Path.Combine(downloadDir,"bad.gguf"))&&!Directory.EnumerateFiles(downloadDir,"*.tmp").Any(),"Model download rejects a bad checksum and cleans up");}File.Delete(path);}
        var redirects=0;using(var redirectClient=new HttpClient(new Fake(request=>{redirects++;var reply=new HttpResponseMessage(HttpStatusCode.Redirect){RequestMessage=request};reply.Headers.Location=new Uri("https://example.invalid/file");return reply;}))){try{await HuggingFaceDownloader.DownloadAsync(redirectClient,metadata,downloadDir,null,CancellationToken.None);throw new Exception("Untrusted redirect accepted");}catch(InvalidOperationException){Check(redirects==1&&!Directory.EnumerateFiles(downloadDir,"*.tmp").Any(),"Model download blocks untrusted redirects");}}Directory.Delete(downloadDir);
        var scenario=new ScenarioCase("id","Test","Question","réponse","La RÉPONSE attendue");
        Check(AdvancedFunctions.ScenarioStatus(scenario)=="Correspondance textuelle"&&AdvancedFunctions.ScenarioStatus(scenario with{Observed=""})=="Non évalué","Scenario status requires a real recorded response");
        Check(AdvancedFunctions.ScenarioStatus(scenario with{Expected="Description libre",MatchText="réponse"})=="Correspondance textuelle"&&AdvancedFunctions.ScenarioStatus(scenario with{MatchText="absent"})=="À examiner","Scenario verification uses the explicit match text");
        Check(AdvancedFunctions.ScenarioStatus(scenario with{Verification="Manuel"})=="Jugement manuel","Manual verification is not presented as an automatic pass");
        var legacy=JsonSerializer.Deserialize<ScenarioCase>("{\"Id\":\"old\",\"Title\":\"Ancien\",\"Question\":\"Q\",\"Expected\":\"ok\",\"Observed\":\"OK\"}");
        Check(legacy!=null&&AdvancedFunctions.ScenarioStatus(legacy)=="Correspondance textuelle"&&legacy.Runs==null,"Legacy scenario data remains readable");
        var temp=Path.Combine(Path.GetTempPath(),"ExplorerAdvanced-"+Guid.NewGuid().ToString("N"));var first=new ExplorerLocalStore("first@example.test",temp);var other=new ExplorerLocalStore("second@example.test",temp);
        await first.SaveRules(rules.ToList());await first.SaveScenarios([scenario]);await first.SaveRecords([new(id,"Responsable","Élevée",null,"À revoir")]);
        Check(first.Rules().Count==3&&first.Scenarios().Count==1&&first.Records().Count==1,"Advanced data persists locally");
        await first.SaveScenarios([scenario with{Runs=[new ScenarioRun(DateTimeOffset.UtcNow,1.2,"réponse","Correspondance textuelle")]}]);
        Check(first.Scenarios()[0].Runs?.Count==1&&first.Scenarios()[0].Runs![0].DurationSeconds==1.2,"Scenario run history persists locally");
        Check(other.Rules().Count==0&&other.Scenarios().Count==0&&other.Records().Count==0,"Advanced data isolated by account");
        var otherEnvironment=new ExplorerLocalStore("first@example.test",temp,"https://different.crm17.dynamics.com");Check(otherEnvironment.Rules().Count==0&&otherEnvironment.Scenarios().Count==0,"Local governance data is isolated by environment");
        var requests=new List<HttpMethod>();
        using var client=new DataverseClient(new HttpClient(new Fake(request=>{requests.Add(request.Method);var path=request.RequestUri!.AbsolutePath;
            var body=path.EndsWith("solutions")?"{\"value\":[{\"solutionid\":\"11111111-1111-1111-1111-111111111111\",\"friendlyname\":\"Démo\"}]}":path.EndsWith("environmentvariabledefinitions")?"{\"value\":[{\"schemaname\":\"rmf_url\",\"type\":100000000}]}":path.EndsWith("solutioncomponents")?"{\"value\":[{\"objectid\":\"11111111-1111-1111-1111-111111111111\",\"componenttype\":1}]}":"{\"conversationtranscriptid\":\"11111111-1111-1111-1111-111111111111\",\"content\":\"{\\\"activities\\\":[{\\\"type\\\":\\\"message\\\",\\\"text\\\":\\\"Bonjour\\\"}]}\",\"metadata\":\"{}\"}";
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)};})),()=>Task.FromResult("local-test-placeholder"));
        Check((await client.ExplorerSolutionsAsync(CancellationToken.None)).Count==1,"Solutions loaded from Dataverse response");
        Check((await client.ExplorerVariableMetadataAsync(CancellationToken.None)).Count==1,"Environment metadata loaded without values");
        Check((await client.ExplorerSolutionComponentsAsync(id,CancellationToken.None)).Count==1,"Solution components loaded for selected solution");
        Check((await client.ExplorerConversationAsync(id,CancellationToken.None)).Single().Text=="Bonjour","Conversation parser reads real response content");
        Check(requests.All(x=>x==HttpMethod.Get),"Advanced Dataverse features issue GET only");
        Check(AdvancedFunctions.Diagnostic(new HttpRequestException("",null,HttpStatusCode.Forbidden)).Contains("403"),"Diagnostic distinguishes denied access");
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        try{await client.ExplorerSolutionsAsync(cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"Advanced queries honor cancellation");}
        Check(ScenarioRunner.ValidTokenEndpoint("https://powerva.microsoft.com/api/botmanagement/v1/directline/directlinetoken?botId=test")&&!ScenarioRunner.ValidTokenEndpoint("https://powerva.microsoft.com.evil.test/token"),"Scenario token endpoint restricted to trusted HTTPS hosts");
        try {using var invalid=new DataverseClient("https://org.crm17.dynamics.com.evil.test");throw new Exception("Host accepted");}catch(InvalidOperationException){Check(true,"Target environment rejects foreign hosts");}
        int calls=0;var paths=new List<string>();using(var runner=new ScenarioRunner(new HttpClient(new Fake(request=>{calls++;paths.Add(request.RequestUri!.Host);var body=calls==1?"{\"token\":\"temporary-test-token\"}":calls==2?"{\"conversationId\":\"conversation1\"}":calls==3?"{\"activities\":[{\"id\":\"welcome\",\"type\":\"message\",\"text\":\"Accueil\"}],\"watermark\":\"1\"}":calls==4?"{\"id\":\"sent1\"}":"{\"activities\":[{\"id\":\"answer1\",\"type\":\"message\",\"from\":{\"id\":\"bot\"},\"text\":\"Réponse réelle\"}],\"watermark\":\"2\"}";return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)};})),(delay,token)=>Task.CompletedTask)){var response=await runner.RunAsync("https://powerva.microsoft.com/api/token","Europe","Bonjour",CancellationToken.None);Check(response=="Réponse réelle"&&calls==5&&paths.Skip(1).All(x=>x=="europe.directline.botframework.com"),"Scenario sends a manual message and captures a new reply through simulated transport");}
        return count;
    }
    sealed class Fake(Func<HttpRequestMessage,HttpResponseMessage> handler):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(handler(request));
    }
}

