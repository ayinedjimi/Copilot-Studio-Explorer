using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace StudioDataverse;

public sealed partial class MainWindow
{
    private readonly ComboBox stateFilter = Options("État", "Tous les états", "Actifs", "Inactifs");
    private readonly ComboBox publishedFilter = Options("Publication", "Toutes", "Déjà publiés", "Jamais publiés");
    private readonly ComboBox dateFilter = Options("Modifiés", "Toutes les dates", "7 derniers jours", "30 derniers jours", "90 derniers jours");
    private readonly ComboBox languageFilter = Options("Langue", "Toutes les langues");
    private readonly ComboBox managedFilter = Options("Solution", "Toutes les solutions", "Non managée", "Managée");
    private readonly ComboBox sortAgents = Options("Trier par", "Modifications récentes", "Modifications anciennes", "Nom : A → Z", "Nom : Z → A", "Création récente");
    private readonly TextBlock summaryCounts = Label("", 12, "#787E8C");
    private readonly Button moreAgents = new() { Content = "Charger davantage", Visibility = Visibility.Collapsed };
    private readonly StackPanel detailFields = new() { Spacing = 14 };
    private readonly StackPanel componentFields = new() { Spacing = 10 };
    private readonly TextBlock detailStatus = Label("", 12, "#687080");
    private readonly TextBox componentSearch = new() { PlaceholderText = "Filtrer les composants" };
    private readonly Pivot detailTabs = new();
    private string detailJson = "";
    private readonly TextBox rawDetail = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 300, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), FontSize = 11 };
    private List<JsonElement> loadedComponents = [];
    private int detailGeneration;
    private Guid? displayedAgentId;
    private bool changingFilters;
    private bool rebindingAgents;
    private List<Agent> visibleAgents = [];
    private readonly AgentAvatar detailAvatar = new();
    private bool componentsRequested;

    private static ComboBox Options(string header, params string[] items)
    {
        var box = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 120 };
        foreach (var item in items) box.Items.Add(new ComboBoxItem { Content = item });
        box.SelectedIndex = 0;
        return box;
    }

    private UIElement BuildFilterBar()
    {
        var panel = new StackPanel { Spacing = 16 };
        ComboBox[] boxes = [stateFilter, publishedFilter, dateFilter, languageFilter, managedFilter, sortAgents];
        foreach (var box in boxes) box.SelectionChanged += (_, _) => { if (!changingFilters) FilterAgents(); };
        panel.Children.Add(BuildPersonalViews());
        var footer = new Grid { ColumnSpacing = 10 };
        footer.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        summaryCounts.VerticalAlignment = VerticalAlignment.Center;
        footer.Children.Add(summaryCounts);
        var reset = new Button { Content = "Réinitialiser", BorderThickness = new Thickness(0), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent) };
        reset.Click += (_, _) =>
        {
            changingFilters = true;
            foreach (var box in boxes) box.SelectedIndex = 0;
            personalView.SelectedIndex = 0;
            agentSearch.Text = "";
            changingFilters = false; FilterAgents();
        };
        Place(footer, reset, 0, 1);
        var export = new Button { Content = "Exporter CSV" };
        export.Click += async (_, _) => await Run(ExportAgentsAsync);
        actions.Add(export);
        Place(footer, export, 0, 2);
        moreAgents.Click += async (_, _) => await Run(async () =>
        {
            var next = await client.MoreAgentsAsync();
            allAgents = allAgents.Concat(next).DistinctBy(x => x.Id).ToList();
            RefreshLanguages(); FilterAgents(); notice.IsOpen = false;
        });
        actions.Add(moreAgents);
        Place(footer, moreAgents, 0, 3);
        panel.Children.Add(footer);
        return panel;
    }

    private void RefreshLanguages()
    {
        var selected = (languageFilter.SelectedItem as ComboBoxItem)?.Tag as int?;
        changingFilters = true;
        languageFilter.Items.Clear();
        languageFilter.Items.Add(new ComboBoxItem { Content = "Toutes les langues" });
        foreach (var code in allAgents.Where(x => x.Language.HasValue).Select(x => x.Language!.Value).Distinct().Order())
            languageFilter.Items.Add(new ComboBoxItem { Content = LanguageName(code), Tag = code });
        languageFilter.SelectedIndex = 0;
        if (selected.HasValue)
            foreach (ComboBoxItem item in languageFilter.Items)
                if (item.Tag is int code && code == selected) languageFilter.SelectedItem = item;
        changingFilters = false;
        RefreshTranscriptAgents();
    }

    private void ApplyAgentFilters()
    {
        if (changingFilters) return;
        var previous = SelectedAgentOrNull();
        var selected = previous?.Id;
        var days = dateFilter.SelectedIndex switch { 1 => 7, 2 => 30, 3 => 90, _ => 0 };
        visibleAgents = AgentQuery.Apply(allAgents, new(agentSearch.Text, stateFilter.SelectedIndex - 1,
            Math.Max(0, publishedFilter.SelectedIndex), days, (languageFilter.SelectedItem as ComboBoxItem)?.Tag as int?,
            managedFilter.SelectedIndex - 1, Math.Max(0, sortAgents.SelectedIndex)), DateTimeOffset.Now);
        if (personalView.SelectedIndex == 1) visibleAgents = visibleAgents.Where(x => preferences.Favorites.Contains(x.Id)).ToList();
        if (personalView.SelectedIndex == 2) visibleAgents = visibleAgents.Where(x => preferences.Recent.Contains(x.Id)).OrderBy(x => preferences.Recent.IndexOf(x.Id)).ToList();
        var retained = selected.HasValue ? visibleAgents.FirstOrDefault(x => x.Id == selected) : null;
        rebindingAgents = true;
        try { agents.Items.Clear(); foreach (var agent in visibleAgents) agents.Items.Add(CreateAgentRow(agent)); SelectAgent(retained); }
        finally { rebindingAgents = false; }
        if (!ReferenceEquals(previous, retained))
        {
            agentWorkspace.IsPaneOpen = retained != null;
            agentName.Text = retained?.Name ?? "";
            _ = LoadSelectedDetailsAsync(retained);
        }
        agentCount.Text = $"{visibleAgents.Count} agent(s) sur {allAgents.Count} chargé(s)";
        summaryCounts.Text = $"{allAgents.Count(x => x.State == 0)} actifs · {allAgents.Count(x => x.PublishedOn.HasValue)} déjà publiés";
        moreAgents.Visibility = client.NextAgentsPath != null ? Visibility.Visible : Visibility.Collapsed;
        emptyAgents.Text = !authenticated ? "Connectez-vous pour retrouver vos agents." : allAgents.Count == 0 ? "Aucun agent accessible dans cet environnement." : "Aucun résultat. Modifiez ou réinitialisez les filtres.";
        emptyAgents.Visibility = visibleAgents.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateRegistrySummary();
    }

    private UIElement BuildDetailTabs()
    {
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(detailStatus);
        var general = new StackPanel { Spacing = 18 };
        general.Children.Add(componentLeds); general.Children.Add(detailFields);
        var overview = new PivotItem { Header = "Général", Content = general };
        var components = new StackPanel { Spacing = 12 };
        componentSearch.TextChanged += (_, _) => RenderComponents();
        components.Children.Add(componentSearch);
        components.Children.Add(componentFields);
        var jsonPanel = new StackPanel { Spacing = 12 };
        var copyJson = new Button { Content = "Copier le JSON" };
        copyJson.Click += (_, _) => CopyText(detailJson);
        jsonPanel.Children.Add(copyJson); jsonPanel.Children.Add(rawDetail);
        detailTabs.Items.Add(overview);
        detailTabs.Items.Add(new PivotItem { Header = "Composants", Content = components });
        detailTabs.Items.Add(new PivotItem { Header = "Utilisation", Content = usageInsights });
        detailTabs.Items.Add(new PivotItem { Header = "Erreurs", Content = errorInsights });
        detailTabs.Items.Add(new PivotItem { Header = "Performances", Content = performanceInsights });
        detailTabs.Items.Add(new PivotItem { Header = "JSON", Content = jsonPanel });
        detailTabs.SelectionChanged += async (_, _) =>
        {
            if (detailTabs.SelectedIndex == 1 && !componentsRequested && SelectedAgentOrNull() is Agent agent)
                await Run(async () => await LoadComponentsAsync(agent));
            if (detailTabs.SelectedIndex is >= 2 and <= 4 && currentAnalytics == null && SelectedAgentOrNull() is Agent analyticsAgent)
                await LoadAgentInsightsAsync(analyticsAgent);
        };
        panel.Children.Add(detailTabs);
        return panel;
    }

    private async Task LoadSelectedDetailsAsync(Agent? agent)
    {
        var generation = ++detailGeneration;
        displayedAgentId = agent?.Id;
        detailFields.Children.Clear(); componentFields.Children.Clear(); loadedComponents.Clear();
        componentsRequested = false;
        ResetAgentInsights();
        detailJson = ""; rawDetail.Text = "";
        detailAvatar.IconBase64 = agent?.IconBase64;
        detailAvatar.Initial = agent?.Initial ?? "A";
        if (agent == null) return;
        preferences.Visit(agent.Id);
        favoriteButton.Content = preferences.Favorites.Contains(agent.Id) ? "★ Retirer le favori" : "☆ Favori";
        _ = SavePreferencesAsync();
        detailStatus.Text = "Chargement des informations…";
        detailTabs.SelectedIndex = 0;
        AddField("Identifiant", agent.Id.ToString(), true);
        AddField("Nom de schéma", agent.Schema, true);
        AddField("État", agent.StatusLabel);
        AddField("Langue", LanguageName(agent.Language));
        try
        {
            var json = await client.DetailsAsync(agent);
            if (generation != detailGeneration) return;
            detailJson = PrettyJson(json); rawDetail.Text = detailJson;
            using var doc = JsonDocument.Parse(json);
            detailFields.Children.Clear();
            var labels = new Dictionary<string, string> { ["botid"] = "Identifiant", ["name"] = "Nom", ["schemaname"] = "Nom de schéma", ["createdon"] = "Créé le", ["modifiedon"] = "Modifié le", ["publishedon"] = "Dernière publication", ["statecode"] = "État", ["statuscode"] = "Provisionnement", ["language"] = "Langue", ["_ownerid_value"] = "Propriétaire", ["_createdby_value"] = "Créé par", ["_modifiedby_value"] = "Modifié par", ["ismanaged"] = "Solution managée", ["versionnumber"] = "Version", ["supportedlanguages"] = "Langues prises en charge" };
            foreach (var property in doc.RootElement.EnumerateObject().Where(p => !p.Name.Contains('@') && p.Name != "iconbase64").OrderBy(p => labels.ContainsKey(p.Name) ? 0 : 1).ThenBy(p => p.Name))
            {
                var formattedKey = property.Name + "@OData.Community.Display.V1.FormattedValue";
                var value = doc.RootElement.TryGetProperty(formattedKey, out var formatted) ? formatted.ToString() : property.Value.ValueKind == JsonValueKind.Null ? "Non renseigné" : property.Value.ToString();
                if (value.Length > 400) value = value[..400] + "… (contenu complet dans JSON)";
                AddField(labels.GetValueOrDefault(property.Name, property.Name), value, property.Name is "botid" or "schemaname");
            }
            detailStatus.Text = "Informations à jour · Dataverse";
            if (!componentsRequested) componentFields.Children.Add(Label("Cliquez sur Voir les composants pour les charger.", 12, "#687080"));
            if (!componentsRequested) await FetchComponentsAsync(agent, false);
        }
        catch (Exception ex)
        {
            if (generation == detailGeneration) detailStatus.Text = DescribeError(ex);
        }
    }

    private void AddField(string title, string value, bool copy = false)
    {
        var field = new StackPanel { Spacing = 4 };
        field.Children.Add(Label(title, 11, "#858895"));
        var text = Label(value, 13, "#303543"); text.IsTextSelectionEnabled = true;
        field.Children.Add(text);
        if (copy)
        {
            var button = new Button { Content = "Copier", FontSize = 11, Padding = new Thickness(8, 3, 8, 3) };
            button.Click += (_, _) => CopyText(value);
            field.Children.Add(button);
        }
        detailFields.Children.Add(field);
    }

    private async Task LoadComponentsAsync(Agent agent)
        => await FetchComponentsAsync(agent, true);
    private async Task FetchComponentsAsync(Agent agent, bool showTab)
    {
        if (SelectedAgentOrNull()?.Id != agent.Id) SelectAgent(agent);
        var generation = detailGeneration;
        componentsRequested = true;
        agentWorkspace.IsPaneOpen = true;
        if (showTab) detailTabs.SelectedIndex = 1;
        componentFields.Children.Clear();
        componentFields.Children.Add(Label("Chargement des composants…", 13, "#687080"));
        try
        {
            var json = await client.ComponentsAsync(agent);
            if (displayedAgentId != agent.Id || generation != detailGeneration) return;
            using var doc = JsonDocument.Parse(json);
            loadedComponents = doc.RootElement.GetProperty("value").EnumerateArray().Select(x => x.Clone()).ToList();
            componentSearch.Text = ""; RenderComponents(); RenderComponentLeds(); notice.IsOpen = false;
        }
        catch (Exception ex)
        {
            if (displayedAgentId != agent.Id || generation != detailGeneration) return;
            componentFields.Children.Clear(); componentFields.Children.Add(Label(DescribeError(ex), 13, "#A4262C"));
            RenderComponentLeds(DescribeError(ex));
            var retry = new Button { Content = "Réessayer" };
            retry.Click += async (_, _) => await Run(async () => await LoadComponentsAsync(agent));
            componentFields.Children.Add(retry); notice.IsOpen = false;
        }
    }

    private void RenderComponents()
    {
        componentFields.Children.Clear();
        var query = componentSearch.Text.Trim();
        var filtered = loadedComponents.Where(x => x.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        componentFields.Children.Add(Label($"{filtered.Count} composant(s) · 100 premiers maximum", 11, "#858895"));
        if (filtered.Count == 0) componentFields.Children.Add(Label(loadedComponents.Count == 0 ? "Aucun composant rattaché à cet agent n’a été retourné par Dataverse." : "Aucun composant ne correspond à votre recherche.", 13, "#687080"));
        foreach (var item in filtered)
        {
            var name = item.TryGetProperty("name", out var n) ? n.ToString() : "Sans nom";
            var type = item.TryGetProperty("componenttype@OData.Community.Display.V1.FormattedValue", out var f) ? f.ToString() : TranscriptParser.Text(item, "componenttype");
            var stack = new StackPanel { Spacing = 5 };
            stack.Children.Add(Label(name, 13)); stack.Children.Add(Label(type, 11, "#858895"));
            var expand = new Expander { Header = stack, HorizontalAlignment = HorizontalAlignment.Stretch };
            var loaded = false;
            expand.Expanding += async (_, _) =>
            {
                if (loaded) return;
                try
                {
                    var json = await client.ComponentDetailsAsync(item.GetProperty("botcomponentid").GetGuid());
                    expand.Content = Code(PrettyJson(json)); loaded = true;
                }
                catch (Exception ex) { expand.Content = Label(DescribeError(ex), 12); }
            };
            componentFields.Children.Add(expand);
        }
    }

    private async Task ExportAgentsAsync()
    {
        var rows = visibleAgents.ToList();
        var picker = new FileSavePicker { SuggestedFileName = "agents-formation-" + DateTime.Now.ToString("yyyy-MM-dd") };
        picker.FileTypeChoices.Add("CSV (Excel)", new List<string> { ".csv" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        if (file == null) { notice.IsOpen = false; return; }
        static string Cell(string? value)
        {
            value ??= "";
            if (value.Length > 0 && "=+-@\t\r\n".Contains(value[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        var csv = new StringBuilder("Identifiant;Nom;Schéma;État;Langue;Création;Modification;Publication\r\n");
        foreach (var agent in rows)
            csv.AppendLine(string.Join(';', new[] { agent.Id.ToString(), agent.Name, agent.Schema, agent.StatusLabel, LanguageName(agent.Language), agent.CreatedOn?.ToString("O"), agent.ModifiedOn?.ToString("O"), agent.PublishedOn?.ToString("O") }.Select(Cell)));
        await System.IO.File.WriteAllTextAsync(file.Path, csv.ToString(), new UTF8Encoding(true));
        notice.Severity = InfoBarSeverity.Success; notice.Message = $"{rows.Count} agent(s) exporté(s).";
    }

    private static void CopyText(string text) { if (string.IsNullOrEmpty(text)) return; var package = new DataPackage(); package.SetText(text); Clipboard.SetContent(package); }
    private static string LanguageName(int? code)
    {
        if (!code.HasValue) return "Non renseignée";
        try { return CultureInfo.GetCultureInfo(code.Value).DisplayName; }
        catch (CultureNotFoundException) { return "Langue " + code; }
    }
    private static string PrettyJson(string json) { using var doc = JsonDocument.Parse(json); return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }); }
}
