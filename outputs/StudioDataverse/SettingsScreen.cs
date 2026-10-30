using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private void RenderSettings()
    {
        var saved=AppSettings.Load(out var loadError);var settings=loadError.Length==0?saved:appSettings;
        var intro=new StackPanel{Spacing=6};var title=Label("Paramètres",22,"#27203D");title.FontWeight=Microsoft.UI.Text.FontWeights.SemiBold;intro.Children.Add(title);
        intro.Children.Add(Label("Configurez la connexion, les collectes et le moteur local. Les modifications prennent effet au prochain démarrage.",13,"#687080"));operationsBody.Children.Add(Card(intro,"#F7F5FF"));
        if(settingsLoadError.Length>0||loadError.Length>0)operationsBody.Children.Add(Label(settingsLoadError.Length>0?settingsLoadError:loadError,13,"#B04445"));
        var connection=new StackPanel{Spacing=12,MaxWidth=880,HorizontalAlignment=HorizontalAlignment.Stretch};connection.Children.Add(Label("Environnement et connexion",18));
        var environment=new TextBox{Header="Nom affiché",Text=settings.EnvironmentName};var organization=new TextBox{Header="URL de l’organisation Dataverse",Text=settings.Organization};var tenant=new TextBox{Header="ID du tenant",Text=settings.TenantId};var clientId=new TextBox{Header="ID de l’application Entra",Text=settings.ClientId};
        NumberBox Number(string header,int value,int min,int max)=>new(){Header=header,Value=value,Minimum=min,Maximum=max,SmallChange=1,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Inline,HorizontalAlignment=HorizontalAlignment.Stretch};
        var timeout=Number("Délai des requêtes Dataverse (secondes)",settings.RequestTimeoutSeconds,10,300);
        connection.Children.Add(environment);connection.Children.Add(organization);connection.Children.Add(tenant);connection.Children.Add(clientId);connection.Children.Add(timeout);
        connection.Children.Add(Label("Connexion déléguée : aucun secret client ni jeton n’est enregistré ici. L’application Entra et les droits Dataverse doivent être configurés dans le tenant choisi.",12,"#687080"));operationsBody.Children.Add(Card(connection));
        var collection=new StackPanel{Spacing=12,MaxWidth=880,HorizontalAlignment=HorizontalAlignment.Stretch};collection.Children.Add(Label("Collecte et navigation",18));
        var days=Number("Période initiale des transcriptions (jours)",settings.TranscriptDays,1,365);
        var incidentDays=Number("Période de recherche des incidents (jours)",settings.IncidentDays,1,365);
        var sample=Options("Échantillon initial des incidents","50","100","250","500");sample.SelectedIndex=settings.IncidentSample switch{50=>0,250=>2,500=>3,_=>1};
        var scenarioRegionSetting=Options("Région du canal de scénarios","Europe","Global","Inde");scenarioRegionSetting.SelectedIndex=settings.ScenarioRegion switch{"Global"=>1,"Inde"=>2,_=>0};
        var sidebar=Number("Largeur de la navigation ouverte (pixels)",settings.SidebarWidth,200,360);
        var logs=new ToggleSwitch{Header="Journal des erreurs dans le dossier de l’application",OnContent="Activé",OffContent="Désactivé",IsOn=settings.ErrorLogEnabled};
        collection.Children.Add(days);collection.Children.Add(incidentDays);collection.Children.Add(sample);collection.Children.Add(scenarioRegionSetting);collection.Children.Add(sidebar);collection.Children.Add(logs);collection.Children.Add(Label("Le journal peut contenir des détails techniques ; relisez-le avant de le partager. L’écriture dépend des droits sur le dossier de l’application.",12,"#687080"));operationsBody.Children.Add(Card(collection));
        var local=new StackPanel{Spacing=12,MaxWidth=880,HorizontalAlignment=HorizontalAlignment.Stretch};local.Children.Add(Label("Moteur local",18));
        var model=new ComboBox{Header="Modèle par défaut",HorizontalAlignment=HorizontalAlignment.Stretch};model.Items.Add(new ComboBoxItem{Content="Sélection automatique"});foreach(var option in InstalledModels())model.Items.Add(new ComboBoxItem{Content=option.Name,Tag=option});model.SelectedIndex=0;
        foreach(ComboBoxItem item in model.Items)if(item.Tag is LocalModelOption option&&option.Path.Equals(settings.DefaultModelPath,StringComparison.OrdinalIgnoreCase))model.SelectedItem=item;
        var context=Number("Contexte du modèle (tokens)",settings.ModelContext,1024,8192);var output=Number("Longueur maximale de réponse (tokens)",settings.ModelOutputTokens,64,2048);var modelTimeout=Number("Délai de génération (secondes)",settings.ModelTimeoutSeconds,30,900);var threads=Number("Threads CPU (0 = automatique)",settings.ModelThreads,0,32);
        var questionNumber=Options("Nombre initial de questions","3","5","10");questionNumber.SelectedIndex=settings.DefaultQuestionCount switch{3=>0,10=>2,_=>1};
        local.Children.Add(model);local.Children.Add(questionNumber);local.Children.Add(context);local.Children.Add(output);local.Children.Add(modelTimeout);local.Children.Add(threads);local.Children.Add(Label("Les paramètres d’échantillonnage du moteur ne sont pas modifiables dans cette version de la DLL.",12,"#687080"));operationsBody.Children.Add(Card(local));
        int Integer(NumberBox box){if(double.IsNaN(box.Value)||box.Value!=Math.Truncate(box.Value)||box.Value<int.MinValue||box.Value>int.MaxValue)throw new InvalidOperationException("Saisissez des nombres entiers valides.");return (int)box.Value;}
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};
        actions.Children.Add(OpButton("Enregistrer les paramètres",async _=>{
            var chosen=(model.SelectedItem as ComboBoxItem)?.Tag as LocalModelOption;
            var next=new AppSettings{EnvironmentName=environment.Text.Trim(),Organization=organization.Text.Trim().TrimEnd('/'),TenantId=tenant.Text.Trim(),ClientId=clientId.Text.Trim(),RequestTimeoutSeconds=Integer(timeout),TranscriptDays=Integer(days),IncidentDays=Integer(incidentDays),IncidentSample=new[]{50,100,250,500}[Math.Clamp(sample.SelectedIndex,0,3)],ScenarioRegion=new[]{"Europe","Global","Inde"}[Math.Clamp(scenarioRegionSetting.SelectedIndex,0,2)],DefaultQuestionCount=new[]{3,5,10}[Math.Clamp(questionNumber.SelectedIndex,0,2)],SidebarWidth=Integer(sidebar),ErrorLogEnabled=logs.IsOn,DefaultModelPath=chosen?.Path??"",DefaultModelFormat=chosen?.Format??"ChatML",ModelContext=Integer(context),ModelOutputTokens=Integer(output),ModelTimeoutSeconds=Integer(modelTimeout),ModelThreads=Integer(threads)};
            await next.SaveAsync();operationsStatus.Text="Paramètres enregistrés. Fermez et relancez l’application pour les appliquer.";
        }));
        actions.Children.Add(OpButton("Rétablir les valeurs initiales",async _=>{await new AppSettings().SaveAsync();operationsStatus.Text="Paramètres réinitialisés. Relancez l’application pour les appliquer.";}));operationsBody.Children.Add(actions);
        operationsBody.Children.Add(Label("Fichier local : "+AppSettings.DefaultPath,12,"#687080"));
    }
}
