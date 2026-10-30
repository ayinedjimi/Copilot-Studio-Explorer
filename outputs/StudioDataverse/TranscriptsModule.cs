using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.Storage.Pickers;

namespace StudioDataverse;

public sealed partial class MainWindow
{
    private readonly StackPanel transcriptPanel = new() { Spacing = 18, Visibility = Visibility.Collapsed };
    private Button transcriptsNav = null!;
    private bool viewingTranscripts;
    private readonly ComboBox transcriptAgent = new() { Header = "Agent", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly DatePicker transcriptFrom = new() { Header = "Du", Date = DateTimeOffset.Now.Date.AddDays(-30), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly DatePicker transcriptTo = new() { Header = "Au (inclus)", Date = DateTimeOffset.Now.Date, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox transcriptSearch = new() { PlaceholderText = "Rechercher dans les conversations chargées" };
    private readonly ListView transcriptList = new() { SelectionMode = ListViewSelectionMode.Single, MaxHeight = 620, MinHeight = 100, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock transcriptCount = Label("Aucune conversation chargée", 12, "#787E8C");
    private readonly TextBlock transcriptStatus = Label("Connectez-vous, puis cliquez sur Récupérer.", 13, "#687080");
    private readonly TextBlock conversationTitle = Label("Sélectionnez une conversation", 18);
    private readonly TextBlock conversationInfo = Label("", 11, "#858895");
    private readonly StackPanel conversationMessages = new() { Spacing = 12 };
    private readonly TextBox messageSearch = new() { PlaceholderText = "Rechercher dans les messages" };
    private readonly CheckBox transcriptEvents = new() { Content = "Afficher aussi les événements" };
    private readonly TextBox transcriptRaw = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 11, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), MaxHeight = 440 };
    private readonly TextBox transcriptMetadata = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 440 };
    private readonly Button transcriptMore = new() { Content = "Charger davantage", Visibility = Visibility.Collapsed };
    private readonly List<Button> transcriptDetailActions = [];
    private List<TranscriptRecord> transcriptRecords = [];
    private List<TranscriptBatch> transcriptBatches = [];
    private List<TranscriptActivity> transcriptActivities = [];
    private string? transcriptNext;
    private int transcriptGeneration;
    private bool refreshingTranscriptAgents;

    private void BuildTranscripts()
    {
        transcriptFrom.Date=DateTimeOffset.Now.Date.AddDays(-appSettings.TranscriptDays);
        transcriptPanel.Children.Add(Label("Récupérez les transcriptions enregistrées dans Dataverse et explorez les échanges.", 14, "#687080"));
        transcriptPanel.Children.Add(transcriptAgent);
        var presets=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};
        foreach(var days in new[]{7,30,90}){var n=days;var button=new Button{Content=n+" jours"};button.Click+=(_,_)=>{transcriptTo.Date=DateTimeOffset.Now.Date;transcriptFrom.Date=DateTimeOffset.Now.Date.AddDays(-n);};presets.Children.Add(button);}transcriptPanel.Children.Add(presets);
        var dates=new Grid{ColumnSpacing=12,RowSpacing=8};dates.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});dates.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});dates.RowDefinitions.Add(new(){Height=GridLength.Auto});dates.RowDefinitions.Add(new(){Height=GridLength.Auto});
        Place(dates,transcriptFrom,0,0);Place(dates,transcriptTo,0,1);dates.SizeChanged+=(_,_)=>{var narrow=dates.ActualWidth<650;Grid.SetRow(transcriptTo,narrow?1:0);Grid.SetColumn(transcriptTo,narrow?0:1);Grid.SetColumnSpan(transcriptFrom,narrow?2:1);Grid.SetColumnSpan(transcriptTo,narrow?2:1);};transcriptPanel.Children.Add(dates);
        RefreshTranscriptAgents();
        transcriptAgent.SelectionChanged += (_, _) => TranscriptFiltersChanged();
        transcriptFrom.DateChanged += (_, _) => TranscriptFiltersChanged();
        transcriptTo.DateChanged += (_, _) => TranscriptFiltersChanged();
        var toolbar = new Grid { ColumnSpacing = 12 };
        toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        toolbar.Children.Add(ActionButton("Récupérer les transcriptions", async () => await LoadTranscriptsAsync(false)));
        transcriptSearch.TextChanged += (_, _) => FilterTranscripts();
        Place(toolbar, transcriptSearch, 0, 1);
        transcriptMore.Click += async (_, _) => await Run(async () => await LoadTranscriptsAsync(true));
        actions.Add(transcriptMore);
        Place(toolbar, transcriptMore, 0, 2);
        transcriptPanel.Children.Add(toolbar);
        transcriptPanel.Children.Add(transcriptCount);
        transcriptPanel.Children.Add(transcriptStatus);
        var workspace = new Grid { ColumnSpacing = 16 };
        workspace.ColumnDefinitions.Add(new() { Width = new GridLength(300) });
        workspace.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        transcriptList.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <StackPanel Spacing="6" Padding="8,12" MaxWidth="250"><TextBlock Text="{Binding StartLabel}" FontSize="14" FontWeight="SemiBold" Foreground="#303543"/><TextBlock Text="{Binding AgentName}" FontSize="12" Foreground="#6853B8" TextTrimming="CharacterEllipsis"/><TextBlock Text="Ouvrir pour voir les tours et la durée" FontSize="10" Foreground="#858895"/></StackPanel>
            </DataTemplate>
            """);
        transcriptList.SelectionChanged += async (_, _) => await OpenTranscriptAsync(transcriptList.SelectedItem as TranscriptRecord);
        workspace.Children.Add(new Border { Child = transcriptList, Background = Brush("#FFFFFF"), CornerRadius = new CornerRadius(10), BorderBrush = Brush("#E1E3E9"), BorderThickness = new Thickness(1) });
        var reader = new StackPanel { Spacing = 12, Margin = new Thickness(20) };
        reader.Children.Add(conversationTitle); reader.Children.Add(conversationInfo);
        var menu = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        Button DetailAction(string title, Func<Task> action)
        {
            var button = new Button { Content = title, IsEnabled = false };
            button.Click += async (_, _) => await Run(action);
            transcriptDetailActions.Add(button);
            return button;
        }
        menu.Children.Add(DetailAction("Tous les fragments", async () =>
        {
            var record = transcriptList.SelectedItem as TranscriptRecord ?? throw new InvalidOperationException("Sélectionnez une conversation.");
            var generation = transcriptGeneration;
            var batches = await client.TranscriptFragmentsAsync(record);
            if (generation != transcriptGeneration) return;
            SetTranscriptContent(batches);
            notice.IsOpen = false;
        }));
        menu.Children.Add(DetailAction("Exporter", ExportTranscriptAsync));
        menu.Children.Add(DetailAction("Copier", () => { CopyText(TranscriptText()); notice.IsOpen = false; return Task.CompletedTask; }));
        reader.Children.Add(menu);
        var tabs = new Pivot();
        var messages = new StackPanel { Spacing = 12 };
        messageSearch.TextChanged += (_, _) => RenderTranscriptMessages();
        transcriptEvents.Checked += (_, _) => RenderTranscriptMessages();
        transcriptEvents.Unchecked += (_, _) => RenderTranscriptMessages();
        messages.Children.Add(messageSearch); messages.Children.Add(transcriptEvents);
        messages.Children.Add(new ScrollViewer { Content = conversationMessages, MaxHeight = 480, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        tabs.Items.Add(new PivotItem { Header = "Messages", Content = messages });
        tabs.Items.Add(new PivotItem { Header = "JSON", Content = transcriptRaw });
        tabs.Items.Add(new PivotItem { Header = "Métadonnées", Content = transcriptMetadata });
        reader.Children.Add(tabs);
        Place(workspace, new Border { Child = reader, Background = Brush("#FFFFFF"), CornerRadius = new CornerRadius(10), BorderBrush = Brush("#E1E3E9"), BorderThickness = new Thickness(1) }, 0, 1);
        transcriptPanel.Children.Add(workspace);
        transcriptPanel.Children.Add(Label("Une transcription peut être découpée en plusieurs fragments. Les conversations récentes peuvent apparaître après un délai d’enregistrement. L’accès dépend des droits Dataverse et de l’activation de la collecte.", 11, "#858895"));
        content.Children.Add(transcriptPanel);
    }

    private void RefreshTranscriptAgents()
    {
        refreshingTranscriptAgents = true;
        var selected = (transcriptAgent.SelectedItem as ComboBoxItem)?.Tag as Guid?;
        transcriptAgent.Items.Clear();
        transcriptAgent.Items.Add(new ComboBoxItem { Content = "Tous les agents" });
        foreach (var agent in allAgents.OrderBy(x => x.Name))
            transcriptAgent.Items.Add(new ComboBoxItem { Content = agent.Name, Tag = agent.Id });
        transcriptAgent.SelectedIndex = 0;
        if (selected.HasValue)
            foreach (ComboBoxItem item in transcriptAgent.Items) if (item.Tag is Guid id && id == selected.Value) transcriptAgent.SelectedItem = item;
        refreshingTranscriptAgents = false;
    }

    private void TranscriptFiltersChanged()
    {
        if (refreshingTranscriptAgents) return;
        transcriptNext = null; transcriptMore.Visibility = Visibility.Collapsed;
        transcriptStatus.Text = "Filtres modifiés : cliquez sur Récupérer pour appliquer la nouvelle sélection.";
    }

    private void ShowTranscripts(Guid? agentId = null)
    {
        HideOverview();SetPageChrome("Transcriptions");
        HideDashboard();
        HideAgentMap();
        viewingTranscripts = true;
        homePanel.Visibility = agentPanel.Visibility = Visibility.Collapsed;
        transcriptPanel.Visibility = Visibility.Visible;
        welcome.Text = "Transcriptions des conversations"; welcome.Visibility = Visibility.Visible;
        content.MaxWidth = double.PositiveInfinity; content.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.Margin = new Thickness(32, 24, 32, 32);
        homeNav.Background = agentsNav.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        transcriptsNav.Background = Brush("#E7E7E7");
        notice.IsOpen = false;
        RefreshTranscriptAgents();
        if (agentId.HasValue)
            foreach (ComboBoxItem item in transcriptAgent.Items) if (item.Tag is Guid id && id == agentId) transcriptAgent.SelectedItem = item;
    }

    private async Task LoadTranscriptsAsync(bool more)
    {
        if (!authenticated) throw new InvalidOperationException("Connectez-vous depuis le menu du compte en bas à gauche pour récupérer les transcriptions.");
        if (transcriptFrom.Date.Date > transcriptTo.Date.Date) throw new InvalidOperationException("La date de début doit précéder la date de fin.");
        if (more && transcriptNext == null) return;
        var path = more ? transcriptNext! : DataverseClient.TranscriptListPath((transcriptAgent.SelectedItem as ComboBoxItem)?.Tag as Guid?,
            new DateTimeOffset(transcriptFrom.Date.Date), new DateTimeOffset(transcriptTo.Date.Date.AddDays(1)));
        if (!more) { transcriptRecords.Clear(); transcriptNext = null; FilterTranscripts(); ClearTranscriptReader(); }
        transcriptStatus.Text = "Récupération depuis Dataverse…";
        try
        {
            var page = await client.ListTranscriptsAsync(path);
            transcriptRecords = transcriptRecords.Concat(page.Items).DistinctBy(x => x.Id).ToList();
            transcriptNext = page.Next;
            FilterTranscripts();
            transcriptStatus.Text = transcriptRecords.Count == 0 ? "Aucune transcription sur cette période. Vérifiez la collecte, la rétention et les droits du compte si des conversations sont attendues." : "Transcriptions récupérées · sélectionnez un enregistrement pour lire les échanges.";
            notice.IsOpen = false;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            transcriptStatus.Text = "Dataverse refuse l’accès (403). Faites vérifier les droits de lecture de ConversationTranscript et le rôle Bot Transcript Viewer par l’administrateur.";
            throw new InvalidOperationException(transcriptStatus.Text, ex);
        }
        catch (Exception ex) { transcriptStatus.Text = DescribeError(ex); throw; }
    }

    private void FilterTranscripts()
    {
        var query = transcriptSearch.Text.Trim();
        var records = transcriptRecords.Where(x => (x.Name + " " + x.AgentName + " " + x.Id).Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        transcriptList.ItemsSource = records;
        transcriptCount.Text = $"{records.Count} enregistrement(s) sur {transcriptRecords.Count} chargé(s) · recherche sur les noms et identifiants";
        transcriptMore.Visibility = transcriptNext == null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ClearTranscriptReader()
    {
        transcriptGeneration++;
        transcriptBatches.Clear(); transcriptActivities.Clear();
        transcriptRaw.Text = transcriptMetadata.Text = "";
        conversationTitle.Text = "Sélectionnez une conversation"; conversationInfo.Text = "";
        conversationMessages.Children.Clear();
        foreach (var button in transcriptDetailActions) button.IsEnabled = false;
    }

    private void ClearTranscripts()
    {
        transcriptRecords.Clear(); transcriptNext = null;
        FilterTranscripts(); ClearTranscriptReader(); RefreshTranscriptAgents();
        transcriptStatus.Text = "Connectez-vous, puis cliquez sur Récupérer.";
    }

    private async Task OpenTranscriptAsync(TranscriptRecord? record)
    {
        ClearTranscriptReader();
        if (record == null) return;
        var generation = transcriptGeneration;
        conversationTitle.Text = record.AgentName;
        conversationInfo.Text = record.StartLabel;
        conversationMessages.Children.Add(Label("Chargement de la conversation…", 13, "#687080"));
        try
        {
            var batch = await client.TranscriptAsync(record.Id);
            if (generation != transcriptGeneration) return;
            SetTranscriptContent([batch]);
        }
        catch (Exception ex)
        {
            if (generation != transcriptGeneration) return;
            conversationMessages.Children.Clear(); conversationMessages.Children.Add(Label(DescribeError(ex), 13));
        }
    }

    private void SetTranscriptContent(List<TranscriptBatch> batches)
    {
        transcriptBatches = batches.OrderBy(TranscriptParser.BatchNumber).ToList();
        transcriptRaw.Text = JsonSerializer.Serialize(transcriptBatches, new JsonSerializerOptions { WriteIndented = true });
        transcriptMetadata.Text = "Identifiant : " + (transcriptList.SelectedItem as TranscriptRecord)?.Id + "\n\n" + string.Join("\n\n", transcriptBatches.Select(x => x.Metadata));
        foreach (var button in transcriptDetailActions) button.IsEnabled = true;
        try { transcriptActivities = TranscriptParser.Parse(transcriptBatches); var messages=transcriptActivities.Where(a=>a.Type.Equals("message",StringComparison.OrdinalIgnoreCase)).ToList();var times=messages.Where(a=>a.Timestamp.HasValue).Select(a=>a.Timestamp!.Value).OrderBy(x=>x).ToArray();var duration=times.Length>=2?" · "+(times[^1]-times[0]).ToString(@"hh\:mm\:ss"):" · durée non disponible";conversationInfo.Text=(transcriptList.SelectedItem as TranscriptRecord)?.StartLabel+" · "+messages.Count+" messages"+duration;RenderTranscriptMessages(); }
        catch (JsonException)
        {
            transcriptActivities = [];
            conversationMessages.Children.Clear();
            conversationMessages.Children.Add(Label("Ce format n’est pas reconnu par le lecteur. Le contenu intégral reste disponible dans JSON et à l’export.", 13));
        }
    }

    private void RenderTranscriptMessages()
    {
        conversationMessages.Children.Clear();
        var query = messageSearch.Text.Trim();
        var activities = transcriptActivities.Where(x => (transcriptEvents.IsChecked == true || x.Type.Equals("message", StringComparison.OrdinalIgnoreCase)) &&
            (x.Text + " " + x.Sender).Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        conversationMessages.Children.Add(Label($"{activities.Count} activité(s) affichée(s) · {transcriptBatches.Count} fragment(s)", 11, "#858895"));
        foreach (var activity in activities)
        {
            var user = activity.Role.Equals("user", StringComparison.OrdinalIgnoreCase);
            var role = user ? "Utilisateur" : activity.Role.Equals("bot", StringComparison.OrdinalIgnoreCase) ? "Agent" : "Participant";
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(Label(role + (activity.Sender.Length > 0 ? " · " + activity.Sender : "") + " · " + (activity.Timestamp?.ToLocalTime().ToString("HH:mm:ss") ?? "heure inconnue"), 11, "#787E8C"));
            var text = Label(activity.Text.Length > 0 ? activity.Text : "[Activité sans texte : " + activity.Type + "]", 14);
            text.IsTextSelectionEnabled = true;
            panel.Children.Add(text);
            conversationMessages.Children.Add(new Border { Child = panel, Padding = new Thickness(14), CornerRadius = new CornerRadius(12), Background = Brush(user ? "#EEEBFC" : "#F4F5F8"), HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Stretch });
        }
        if (activities.Count == 0) conversationMessages.Children.Add(Label("Aucun message correspondant. Activez les événements pour consulter les autres activités.", 12, "#687080"));
    }

    private string TranscriptText() => string.Join("\n\n", transcriptActivities.Select(x => $"[{x.Timestamp?.ToString("O") ?? "heure inconnue"}] {x.Role} {x.Sender} ({x.Type})\n{x.Text}"));
    private async Task ExportTranscriptAsync()
    {
        if (transcriptBatches.Count == 0) throw new InvalidOperationException("Sélectionnez et chargez une transcription.");
        var batches = transcriptBatches.ToList();
        var text = TranscriptText();
        var picker = new FileSavePicker { SuggestedFileName = "transcription-" + (transcriptList.SelectedItem as TranscriptRecord)?.Id };
        picker.FileTypeChoices.Add("JSON intégral", new List<string> { ".json" });
        picker.FileTypeChoices.Add("Texte lisible", new List<string> { ".txt" });
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        var file = await picker.PickSaveFileAsync();
        if (file == null) { notice.IsOpen = false; return; }
        if (file.FileType.Equals(".txt", StringComparison.OrdinalIgnoreCase) && text.Length == 0)
            throw new InvalidOperationException("Aucune activité lisible n’a été reconnue. Exportez au format JSON pour conserver le contenu intégral.");
        await System.IO.File.WriteAllTextAsync(file.Path, file.FileType.Equals(".txt", StringComparison.OrdinalIgnoreCase) ? text : JsonSerializer.Serialize(batches, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true));
        notice.Severity = InfoBarSeverity.Success; notice.Message = "Transcription exportée.";
    }
}
