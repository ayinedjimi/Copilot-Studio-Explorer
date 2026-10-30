using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace StudioDataverse;
public sealed partial class MainWindow
{
    private readonly StackPanel componentLeds = new() { Spacing = 8 };
    private readonly StackPanel usageInsights = new() { Spacing = 14 };
    private readonly StackPanel errorInsights = new() { Spacing = 14 };
    private readonly StackPanel performanceInsights = new() { Spacing = 14 };
    private AgentAnalytics? currentAnalytics;
    private int analyticsLoadingGeneration = -1;
    private readonly Dictionary<Guid,AgentAnalytics> agentAnalyticsCache = [];

    private void ResetAgentInsights()
    {
        currentAnalytics = null; analyticsLoadingGeneration = -1;
        componentLeds.Children.Clear();
        componentLeds.Children.Add(Label("État des composants · chargement nécessaire", 12, "#787E8C"));
        foreach (var panel in new[] { usageInsights, errorInsights, performanceInsights })
        {
            panel.Children.Clear(); panel.Children.Add(Label("Analyse des 50 derniers enregistrements de transcription, sur 30 jours. Les résultats dépendent de la collecte et des droits du compte.", 12, "#787E8C"));
            var load = new Button { Content = "Charger les données d’analyse" };
            load.Click += async (_, _) => { if (SelectedAgentOrNull() is Agent agent) await LoadAgentInsightsAsync(agent); };
            panel.Children.Add(load);
        }
    }
    private void RenderComponentLeds(string? error = null)
    {
        componentLeds.Children.Clear();
        componentLeds.Children.Add(Label("État Dataverse des composants", 14));
        componentLeds.Children.Add(Label("Vert : actif · rouge : inactif · gris : état non renseigné. Cet état ne garantit pas la publication ni la configuration d’un outil.", 11, "#858895"));
        if (error != null) { componentLeds.Children.Add(Label(error, 12, "#A4262C")); return; }
        if (loadedComponents.Count == 0) componentLeds.Children.Add(Label("Aucun composant retourné pour cet agent.", 12, "#787E8C"));
        foreach (var item in loadedComponents)
        {
            var state = item.TryGetProperty("statecode", out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : -1;
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(12) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = Brush(state == 0 ? "#168A45" : state == 1 ? "#D63B44" : "#999CA6"), VerticalAlignment = VerticalAlignment.Center });
            Place(row, Label(TranscriptParser.Text(item,"name"),12),0,1);
            Place(row, Label(state == 0 ? "Actif" : state == 1 ? "Inactif" : "Inconnu",11,"#787E8C"),0,2);
            componentLeds.Children.Add(row);
        }
    }
    private async Task LoadAgentInsightsAsync(Agent agent)
    {
        var generation = detailGeneration;
        if (analyticsLoadingGeneration == generation) return;
        analyticsLoadingGeneration = generation;
        foreach (var panel in new[] { usageInsights, errorInsights, performanceInsights }) { panel.Children.Clear(); panel.Children.Add(Label("Chargement des transcriptions…",13,"#787E8C")); }
        try
        {
            var result = await client.AnalyticsAsync(agent);
            if (generation != detailGeneration) return;
            currentAnalytics = result;
            agentAnalyticsCache[agent.Id]=result;FilterAgents();
            RenderAgentInsights(result);
        }
        catch (Exception ex)
        {
            if (generation != detailGeneration) return;
            foreach (var panel in new[] { usageInsights, errorInsights, performanceInsights })
            {
                panel.Children.Clear(); panel.Children.Add(Label(DescribeError(ex),12,"#A4262C"));
                var retry = new Button { Content = "Réessayer" };
                retry.Click += async (_, _) => await LoadAgentInsightsAsync(agent); panel.Children.Add(retry);
            }
        }
        finally { if (analyticsLoadingGeneration == generation) analyticsLoadingGeneration = -1; }
    }
    private static void Insight(StackPanel panel, string label, string value)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(Label(label,11,"#787E8C")); stack.Children.Add(Label(value,22));
        panel.Children.Add(Card(stack,"#FAFAFC"));
    }
    private void RenderAgentInsights(AgentAnalytics data)
    {
        foreach (var panel in new[] { usageInsights, errorInsights, performanceInsights })
        {
            panel.Children.Clear(); panel.Children.Add(Label($"Échantillon : {data.Records} enregistrement(s), {data.Conversations} conversation(s) distincte(s) · 30 derniers jours · limite 50 enregistrements",11,"#787E8C"));
            foreach (var warning in data.Warnings) panel.Children.Add(Label(warning,11,"#A66C10"));
        }
        Insight(usageInsights,"Messages utilisateur",data.UserMessages.ToString());
        Insight(usageInsights,"Messages agent",data.AgentMessages.ToString());
        Insight(usageInsights,"Utilisateurs distincts identifiés",data.IdentifiedUsers == 0 ? "Non disponible" : data.IdentifiedUsers.ToString());
        usageInsights.Children.Add(Label("Les identifiants absents ne sont pas comptés. Les fragments ne sont pas assimilés à des sessions.",11,"#787E8C"));
        Insight(errorInsights,"Activités d’erreur explicites",data.Errors.Count.ToString());
        if (data.Errors.Count == 0) errorInsights.Children.Add(Label("Aucune activité d’erreur explicite trouvée dans l’échantillon. Ce résultat n’exclut pas d’autres erreurs non enregistrées.",12,"#787E8C"));
        foreach (var error in data.Errors)
        {
            var expand = new Expander { Header = (error.Timestamp?.ToLocalTime().ToString("dd/MM HH:mm:ss") ?? "Heure inconnue") + " · " + error.Type, Content = Code(error.Raw), HorizontalAlignment = HorizontalAlignment.Stretch };
            errorInsights.Children.Add(expand);
        }
        Insight(performanceInsights,"Délai médian utilisateur → première réponse",data.MedianSeconds.HasValue ? data.MedianSeconds.Value.ToString("0.00") + " s" : "Non disponible");
        Insight(performanceInsights,"95e percentile du délai observé",data.P95Seconds.HasValue ? data.P95Seconds.Value.ToString("0.00") + " s" : "Non disponible");
        performanceInsights.Children.Add(Label($"{data.ResponseSeconds.Count} paire(s) horodatée(s). Délai conversationnel observé ; ce n’est pas le temps interne du modèle. Les délais négatifs et supérieurs à une heure sont exclus.",11,"#787E8C"));
        performanceInsights.Children.Add(Label("Coûts, crédits, temps d’exécution interne et SLA : non disponibles dans cette source.",12,"#787E8C"));
    }
}
