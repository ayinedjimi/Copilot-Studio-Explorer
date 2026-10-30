using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Markup;
using Windows.UI;

namespace StudioDataverse;

public sealed partial class MainWindow : Window
{
    private readonly DataverseClient client = new();
    private readonly StackPanel content = new() { Spacing = 24, Margin = new Thickness(32, 64, 32, 32), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel traces = new() { Spacing = 12, Margin = new Thickness(16) };
    private readonly InfoBar notice = new() { IsOpen = false, IsClosable = true, Severity = InfoBarSeverity.Informational };
    private readonly Button signIn = new() { Content = "Se connecter", VerticalAlignment = VerticalAlignment.Center };
    private readonly ListView agents = new() { MinHeight = 130, MaxHeight = 340, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBox agentName = new() { Header = "Nom de l’agent", PlaceholderText = "Sélectionnez un agent" };
    private readonly TextBlock identityLabel = Label("Non connecté", 12, "#605E5C");
    private readonly StackPanel agentPanel = new() { Spacing = 14 };
    private readonly StackPanel homePanel = new() { Spacing = 24 };
    private readonly List<Button> actions = [];
    private bool busy;
    private bool authenticated;
    private int traceCount;
    private readonly TextBlock traceCounter = Label("0 appel", 12, "#605E5C");
    private readonly TextBlock welcome = Label("Créons votre prochain agent", 32);
    private readonly ColumnDefinition apiColumn = new() { Width = new GridLength(0) };
    private Grid apiPane = null!;
    private Button homeNav = null!;
    private Button agentsNav = null!;
    private readonly TextBlock connectionStatus = Label("", 11, "#605E5C");
    private readonly TextBlock accountName = Label("Aucun compte connecté", 12, "#14263B");
    private readonly TextBlock accountState = Label("Non connecté", 11, "#757575");
    private readonly TextBlock avatarText = Label("?", 14, "#FFFFFF");
    private readonly Button cancelSignIn = new() { Content = "Annuler", Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Stretch };
    private CancellationTokenSource? signInCancellation;
    private List<Agent> allAgents = [];
    private readonly TextBox agentSearch = new() { PlaceholderText = "Rechercher un agent", Width = 300 };
    private readonly TextBlock agentCount = Label("Vos agents", 13, "#687080");
    private readonly TextBlock emptyAgents = Label("Connectez-vous pour retrouver les agents de votre environnement.", 14, "#687080");
    private readonly SplitView agentWorkspace = new() { PanePlacement = SplitViewPanePlacement.Right, OpenPaneLength = 320, DisplayMode = SplitViewDisplayMode.Inline, IsPaneOpen = false, Height = 540 };

    public MainWindow()
    {
        Title = ProductIdentity.Name;
        AppWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "StudioDataverse.ico"));
        AppWindow.IsShownInSwitchers = true;
        var root = new Grid { Background = Brush("#FAFAFA"), RequestedTheme = ElementTheme.Light };
        root.ColumnDefinitions.Add(new() { Width = new GridLength(238) });
        root.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(apiColumn);

        var sidebar = new Grid { BorderBrush = Brush("#DEDEDE"), BorderThickness = new Thickness(0, 0, 1, 0) };
        sidebar.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        sidebar.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var nav = new StackPanel { Spacing = 4 };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(4, 12, 12, 18) };
        brand.Children.Add(Artwork("brand", 26, 28));
        var brandText = Label(ProductIdentity.Name, 16, "#14263B");
        brandText.MaxWidth = 172;
        brandText.FontWeight = FontWeights.SemiBold;
        brandText.VerticalAlignment = VerticalAlignment.Center;
        var brandCopy = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        brandCopy.Children.Add(brandText);
        brandCopy.Children.Add(new HyperlinkButton
        {
            Content = ProductIdentity.Author,
            NavigateUri = new Uri("https://ayinedjimi-consultants.fr"),
            FontSize = 11,
            Padding = new Thickness(0),
            MinHeight = 0,
            Foreground = Brush("#536277"),
            HorizontalAlignment = HorizontalAlignment.Left
        });
        brand.Children.Add(brandCopy);
        nav.Children.Add(brand);
        signIn.HorizontalAlignment = HorizontalAlignment.Stretch;
        signIn.Margin = new Thickness(12, 0, 12, 0);
        signIn.Background = Brush("#6357D5");
        signIn.Foreground = Brush("#FFFFFF");
        signIn.Padding = new Thickness(12, 10, 12, 10);
        nav.Children.Add(signIn);
        connectionStatus.Margin = new Thickness(14, 0, 14, 4);
        nav.Children.Add(connectionStatus);
        cancelSignIn.Margin = new Thickness(12, 0, 12, 8);
        cancelSignIn.Click += (_, _) => signInCancellation?.Cancel();
        nav.Children.Add(cancelSignIn);
        homeNav = Nav("\uE80F", "Accueil", true);
        agentsNav = Nav("\uE8D4", "Liste des agents");
        homeNav.Click += (_, _) => ShowHome();
        agentsNav.Click += async (_, _) => { ShowAgents(); if (authenticated) await Run(LoadAgents); };
        nav.Children.Add(homeNav);
        overviewNav = Nav("\uE9D9", "Vue d’ensemble");
        overviewNav.Click += (_, _) => ShowOverview();
        nav.Children.Add(overviewNav);
        nav.Children.Add(new Border { Height = 1, Background = Brush("#DEDEDE"), Margin = new Thickness(0, 8, 16, 4) });
        nav.Children.Add(agentsNav);
        transcriptsNav = Nav("\uE8F2", "Transcriptions");
        transcriptsNav.Click += async (_, _) => { ShowTranscripts(); if (authenticated && transcriptRecords.Count == 0) await Run(async () => await LoadTranscriptsAsync(false)); };
        nav.Children.Add(transcriptsNav);
        mapNav = Nav("\uE707", "Agent Map");
        mapNav.Click += async (_, _) => { ShowAgentMap(); if (authenticated && allAgents.Count == 0) await Run(LoadAgents); };
        nav.Children.Add(mapNav);
        dashboardNav = Nav("\uE9D9", "Analyses");
        dashboardNav.Click += (_, _) => ShowDashboard();
        nav.Children.Add(dashboardNav);
        InstallOperationsNavigation(nav);
        var flows = Nav("\uE8AB", "Flux liés");
        flows.Click += (_, _) => ShowOperations("Dépendances");
        nav.Children.Add(flows);
        sidebar.Children.Add(new ScrollViewer { Content = nav, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var footer = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(4, 0, 12, 12) };
        var apiToggle = Nav("\uE943", "Appels Dataverse");
        apiToggle.Click += (_, _) => ToggleApi();
        footer.Children.Add(apiToggle);
        footer.Children.Add(Label(ProductIdentity.Name, 12, "#536277"));
        footer.Children.Add(Label("Auteur : " + ProductIdentity.Author, 11, "#536277"));
        var about = Nav("\uE946", "À propos");
        about.Click += async (_, _) =>
        {
            var copy = new StackPanel { Spacing = 12, MaxWidth = 460 };
            copy.Children.Add(Label(ProductIdentity.Name, 24));
            copy.Children.Add(Label("Auteur : " + ProductIdentity.Author, 15));
            copy.Children.Add(Label("Explorez les agents, leurs composants, les transcriptions, les audits, les dépendances, les versions et la gouvernance de votre environnement Dataverse.", 14, "#536277"));
            copy.Children.Add(Label("Version " + (typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"), 12, "#536277"));
            var dialog = new ContentDialog { Title = "À propos", Content = copy, CloseButtonText = "Fermer", XamlRoot = ((FrameworkElement)Content).XamlRoot };
            await dialog.ShowAsync();
        };
        footer.Children.Add(about);
        var accountRow = new Grid { ColumnSpacing = 10 };
        accountRow.ColumnDefinitions.Add(new() { Width = new GridLength(34) });
        accountRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        avatarText.HorizontalAlignment = HorizontalAlignment.Center;
        avatarText.VerticalAlignment = VerticalAlignment.Center;
        accountRow.Children.Add(new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Background = Brush("#6357D5"), Child = avatarText });
        accountName.TextWrapping = TextWrapping.NoWrap;
        accountName.TextTrimming = TextTrimming.CharacterEllipsis;
        var accountCopy = new StackPanel { Spacing = 2 };
        accountCopy.Children.Add(accountName); accountCopy.Children.Add(accountState);
        Place(accountRow, accountCopy, 0, 1);
        var accountButton = new Button { Content = accountRow, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(8), CornerRadius = new CornerRadius(8) };
        var accountMenu = new MenuFlyout();
        var switchAccount = new MenuFlyoutItem { Text = "Se connecter / Changer de compte" };
        switchAccount.Click += async (_, _) => await ConnectAsync();
        accountMenu.Items.Add(switchAccount);
        var disconnect = new MenuFlyoutItem { Text = "Se déconnecter" };
        disconnect.Click += async (_, _) =>
        {
            if (busy) return;
            await Run(async () =>
            {
                await client.SignOutAsync(); authenticated = false;ClearOperationsState();
                preferences = new(); preferencesPath = null; comparisonAnchor = null;
                comparisonLabel.Text = "Aucune référence choisie"; personalView.SelectedIndex = 0;
                allAgents.Clear(); agentSearch.Text = ""; FilterAgents(); agentName.Text = "";
                ClearTranscripts(); traces.Children.Clear(); traceCount = 0; traceCounter.Text = "0 appel";
                ClearDashboard();agentAnalyticsCache.Clear();
                accountName.Text = "Aucun compte connecté"; accountState.Text = "Non connecté";
                avatarText.Text = "?"; signIn.Content = "Se connecter";
                connectionStatus.Text = ""; notice.Severity = InfoBarSeverity.Informational;
                notice.Message = "Vous êtes déconnecté de cette application.";
            });
        };
        accountMenu.Items.Add(disconnect);
        accountButton.Flyout = accountMenu;
        footer.Children.Add(accountButton);
        Place(sidebar, footer, 1, 0);
        root.Children.Add(sidebar);

        var main = new Grid();
        main.RowDefinitions.Add(new() { Height = new GridLength(48) });
        main.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var banner = new Grid { Margin = new Thickness(24, 0, 18, 0), ColumnSpacing = 10 };
        banner.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        banner.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var bannerCopy = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        bannerCopy.Children.Add(Label("Bienvenue dans Copilot Studio Explorer · Explorez vos agents, leurs composants et leurs conversations Dataverse.", 14, "#14263B"));
        var more = new HyperlinkButton { Content = "En savoir plus", NavigateUri = new Uri("https://learn.microsoft.com/microsoft-copilot-studio/"), FontSize = 11, Padding = new Thickness(32, 0, 0, 0), Foreground = Brush("#6550FF") };
        bannerCopy.Children.Add(more);
        banner.Children.Add(bannerCopy);
        var dismiss = new Button { Content = "×", BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent), VerticalAlignment = VerticalAlignment.Top };
        dismiss.Click += (_, _) => banner.Visibility = Visibility.Collapsed;
        Place(banner, dismiss, 0, 1);
        main.Children.Add(banner);
        InstallReferenceChrome(main,banner);
        BuildHome();
        content.Children.Add(notice);
        content.Children.Add(homePanel);
        welcome.Visibility = Visibility.Collapsed;
        content.Children.Add(welcome);
        BuildAgents();
        agentPanel.Visibility = Visibility.Collapsed;
        content.Children.Add(agentPanel);
        BuildTranscripts();
        BuildAgentMap();
        BuildAnalyticsDashboard();
        BuildOverview();
        BuildOperations();
        Place(main, new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 1, 0);
        Place(root, main, 0, 1);

        apiPane = new Grid { Visibility = Visibility.Collapsed, Background = Brush("#FFFFFF"), BorderBrush = Brush("#E1E1E8"), BorderThickness = new Thickness(1, 0, 0, 0) };
        apiPane.RowDefinitions.Add(new() { Height = GridLength.Auto });
        apiPane.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var apiHeader = new StackPanel { Spacing = 12, Margin = new Thickness(20) };
        apiHeader.Children.Add(Label("Appels Dataverse", 20));
        apiHeader.Children.Add(Label("Requêtes réelles de cette application · jetons masqués", 12, "#605E5C"));
        apiHeader.Children.Add(traceCounter);
        var close = new Button { Content = "Fermer le panneau" };
        close.Click += (_, _) => ToggleApi();
        apiHeader.Children.Add(close);
        var clear = new Button { Content = "Effacer le journal" };
        clear.Click += (_, _) => { traces.Children.Clear(); traceCount = 0; traceCounter.Text = "0 appel"; };
        apiHeader.Children.Add(clear);
        apiPane.Children.Add(apiHeader);
        Place(apiPane, new ScrollViewer { Content = traces, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 1, 0);
        Place(root, apiPane, 0, 2);
        signIn.Click += async (_, _) => await ConnectAsync();
        Content = root;
        InstallShortcuts(root);
        client.Traced += trace => DispatcherQueue.TryEnqueue(() => {if(trace.Session==client.SessionGeneration)AddTrace(trace);});
        Closed += (_, _) => { operationsCancellation?.Cancel();targetEnvironmentClient?.Dispose();dependencyWeb.Close();reportWeb.Close();signInCancellation?.Cancel(); mapWeb.Close(); client.Dispose(); };
    }

    private void BuildHome()
    {
        homePanel.Spacing = 16;
        var greeting = Label("", 30, "#202020");
        greeting.Inlines.Add(new Run { Text = "Bonjour, prêt à " });
        greeting.Inlines.Add(new Run { Text = "explorer ?", FontFamily = new FontFamily("Georgia"), FontStyle = Windows.UI.Text.FontStyle.Italic, Foreground = Brush("#6650FF") });
        homePanel.Children.Add(greeting);
        var subtitle = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        subtitle.Children.Add(Label("Comprenez et gouvernez vos agents", 18, "#6E6E6E"));
        var credits = Badge("Exploration Dataverse");
        credits.VerticalAlignment = VerticalAlignment.Center;
        subtitle.Children.Add(credits);
        homePanel.Children.Add(subtitle);
        var cards = new Grid { ColumnSpacing = 16, Margin = new Thickness(0, 36, 0, 0) };
        for (var i = 0; i < 3; i++) cards.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        Place(cards, HomeCard("assistant", "Agents", "Consultez les agents, leurs composants et leurs configurations.", () => ShowAgents()), 0, 0);
        Place(cards, HomeCard("workflow", "Audit et dépendances", "Examinez les configurations et les liens entre agents, flux et connexions.", () => ShowOperations("Audit")), 0, 1);
        Place(cards, HomeCard("application", "Incidents et rapports", "Explorez les erreurs observées et produisez vos rapports d’analyse.", () => ShowOperations("Incidents")), 0, 2);
        homePanel.Children.Add(cards);
        var otherContent = new Grid { ColumnSpacing = 16 };
        otherContent.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        otherContent.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var otherText = new StackPanel { Spacing = 4 };
        otherText.Children.Add(Label("Parcourir le registre", 16, "#14263B"));
        otherText.Children.Add(Label("Retrouvez les agents de votre environnement, filtrez leurs états et ouvrez leurs composants, conversations et analyses.", 12, "#536277"));
        otherContent.Children.Add(otherText);
        Place(otherContent, new FontIcon { Glyph = "\uE72A", FontSize = 16, Foreground = Brush("#666666") }, 0, 1);
        var other = new Button { Content = otherContent, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(20, 16, 20, 16), CornerRadius = new CornerRadius(16), BorderBrush = Brush("#DCDCDC"), BorderThickness = new Thickness(1), Background = Brush("#FAFAFA") };
        other.Click += (_, _) => ShowAgents();
        homePanel.Children.Add(other);
    }

    private Button HomeCard(string asset, string title, string description, Action clicked)
    {
        var panel = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Top };
        panel.Children.Add(Artwork(asset, double.NaN, 104));
        var heading = new StackPanel { Spacing = asset == "assistant" ? 7 : 4, Orientation = asset == "assistant" ? Orientation.Horizontal : Orientation.Vertical, Margin = new Thickness(0, 6, 0, 0) };
        heading.Children.Add(Label(title, 14, "#102234"));
        heading.Children.Add(Badge("Dataverse"));
        panel.Children.Add(heading);
        var copy = Label(description, 14, "#536277"); copy.LineHeight = 20;
        panel.Children.Add(copy);
        var button = new Button { Content = panel, MinHeight = 310, VerticalContentAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(16, 22, 16, 20), CornerRadius = new CornerRadius(24), BorderThickness = new Thickness(1), BorderBrush = Brush("#DCDCDC"), Background = Brush("#FFFFFF") };
        button.Click += (_, _) => clicked();
        return button;
    }

    private static Image Artwork(string name, double width, double height) => new() { Source = new SvgImageSource(new Uri("ms-appx:///Assets/" + name + ".svg")), Width = width, Height = height, Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Stretch };
    private static Border Badge(string text) => new() { Child = Label(text, 12, "#536277"), BorderBrush = Brush("#DEDEDE"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(7, 1, 7, 1), Background = Brush("#FFFFFF"), HorizontalAlignment = HorizontalAlignment.Left };
    private void ShowHome()
    {
        HideOverview();SetPageChrome("Accueil",true);
        HideDashboard();
        HideAgentMap();
        viewingTranscripts = false; transcriptPanel.Visibility = Visibility.Collapsed;
        transcriptsNav.Background = new SolidColorBrush(Colors.Transparent);
        content.MaxWidth = 720; content.HorizontalAlignment = HorizontalAlignment.Center;
        content.Margin = new Thickness(32, 64, 32, 32);
        homePanel.Visibility = Visibility.Visible; agentPanel.Visibility = Visibility.Collapsed;
        welcome.Visibility = Visibility.Collapsed; notice.IsOpen = false;
        homeNav.Background = Brush("#E7E7E7"); agentsNav.Background = new SolidColorBrush(Colors.Transparent);
    }
    private void ShowAgents()
    {
        HideOverview();SetPageChrome("Tous les agents");
        HideDashboard();
        HideAgentMap();
        viewingTranscripts = false; transcriptPanel.Visibility = Visibility.Collapsed;
        transcriptsNav.Background = new SolidColorBrush(Colors.Transparent);
        content.MaxWidth = double.PositiveInfinity; content.HorizontalAlignment = HorizontalAlignment.Stretch;
        content.Margin = new Thickness(8, 10, 8, 24);
        homePanel.Visibility = Visibility.Collapsed; agentPanel.Visibility = Visibility.Visible;
        welcome.Text = "Tous les agents"; welcome.Visibility = Visibility.Visible;
        homeNav.Background = new SolidColorBrush(Colors.Transparent); agentsNav.Background = Brush("#E7E7E7");
    }
    private void Explain(string title, string message)
    {
        notice.Title = title; notice.Severity = InfoBarSeverity.Informational; notice.Message = message; notice.IsOpen = true;
    }
    private void ToggleApi()
    {
        var show = apiPane.Visibility != Visibility.Visible;
        apiPane.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        apiColumn.Width = new GridLength(show ? 360 : 0);
    }


    private void BuildAgents()
    {
        agentPanel.Spacing = 20;
        agentPanel.Children.Add(Label("Retrouvez et gérez les agents de votre environnement.", 14, "#687080"));
        agentPanel.Children.Add(BuildRegistryTabs());
        agentPanel.Children.Add(new Border{Child=Label("Les filtres et tris sont intégrés au registre. Les tirets indiquent des données non disponibles ; les métriques de conversation proviennent d’échantillons chargés depuis la fiche de l’agent.",12,"#536277"),Padding=new Thickness(12,8,12,8),Background=Brush("#F4F4F4"),BorderBrush=Brush("#DEDEDE"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3)});
        agentPanel.Children.Add(registrySummary);
        var toolbar = new Grid { ColumnSpacing = 16 };
        toolbar.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        agentCount.VerticalAlignment = VerticalAlignment.Center;
        toolbar.Children.Add(agentCount);
        agentSearch.TextChanged += (_, _) => FilterAgents();
        Place(toolbar, agentSearch, 0, 1);
        var refresh = new Button { Content = new FontIcon { Glyph = "\uE72C", FontSize = 16 }, Width = 36, Height = 36, Background = Brush("#FFFFFF"), BorderBrush = Brush("#DFE1E7"), CornerRadius = new CornerRadius(6) };
        ToolTipService.SetToolTip(refresh, "Actualiser les agents");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(refresh, "Actualiser les agents");
        refresh.Click += async (_, _) => await Run(LoadAgents);
        actions.Add(refresh);
        Place(toolbar, refresh, 0, 2);
        var tableToolbar=new Grid{ColumnSpacing=14};tableToolbar.ColumnDefinitions.Add(new(){Width=GridLength.Auto});tableToolbar.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        tableToolbar.Children.Add((FrameworkElement)BuildFilterChips());Place(tableToolbar,toolbar,0,1);agentPanel.Children.Add(tableToolbar);
        agentPanel.Children.Add(BuildFilterBar());

        var table = BuildAgentTable();
        agentWorkspace.Content = new ScrollViewer { Content = table, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        agentWorkspace.OpenPaneLength = 420;
        agentWorkspace.SizeChanged += (_, _) => agentWorkspace.DisplayMode = agentWorkspace.ActualWidth < 1100 ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.Inline;
        agents.SelectionChanged += async (_, _) =>
        {
            if (rebindingAgents) return;
            var selected = SelectedAgentOrNull();
            agentName.Text = selected?.Name ?? "";
            agentWorkspace.IsPaneOpen = selected != null;
            await LoadSelectedDetailsAsync(selected);
        };
        var editor = new StackPanel { Spacing = 20, Margin = new Thickness(24) };
        var detailsHeader = new Grid();
        detailsHeader.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        detailsHeader.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var detailIdentity = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        detailIdentity.Children.Add(detailAvatar);
        detailIdentity.Children.Add(Label("Détails de l’agent", 20));
        detailsHeader.Children.Add(detailIdentity);
        var closeDetails = new Button { Content = new FontIcon { Glyph = "\uE711", FontSize = 12 }, BorderThickness = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent) };
        ToolTipService.SetToolTip(closeDetails, "Fermer les détails");
        closeDetails.Click += (_, _) => { agents.SelectedItem = null; agentWorkspace.IsPaneOpen = false; };
        Place(detailsHeader, closeDetails, 0, 1);
        editor.Children.Add(detailsHeader);
        editor.Children.Add(BuildProductivityActions());
        editor.Children.Add(agentName);
        editor.Children.Add(ActionButton("Enregistrer", async () =>
        {
            var selected = SelectedAgent();
            var name = agentName.Text.Trim();
            if (name.Length is < 1 or > 100) throw new InvalidOperationException("Le nom doit contenir entre 1 et 100 caractères.");
            await client.RenameAsync(selected, name);
            await LoadAgents(selected.Id);
            notice.IsOpen = true;
            notice.Severity = InfoBarSeverity.Success;
            notice.Message = "Nom enregistré et relu depuis Dataverse. Actualisez Copilot Studio pour vérifier la modification.";
        }));
        var componentsButton = new Button { Content = "Voir les composants", HorizontalAlignment = HorizontalAlignment.Stretch };
        componentsButton.Click += async (_, _) => await Run(async () =>
        {
            await LoadComponentsAsync(SelectedAgent());
        });
        actions.Add(componentsButton);
        editor.Children.Add(componentsButton);
        var conversationsButton = new Button { Content = "Voir les conversations", HorizontalAlignment = HorizontalAlignment.Stretch };
        conversationsButton.Click += async (_, _) => await Run(async () =>
        {
            var selected = SelectedAgent(); ShowTranscripts(selected.Id); await LoadTranscriptsAsync(false);
        });
        actions.Add(conversationsButton); editor.Children.Add(conversationsButton);
        editor.Children.Add(BuildDetailTabs());
        editor.Children.Add(Label("Les modifications sont enregistrées dans votre environnement de formation.", 12, "#858895"));
        agentWorkspace.PaneBackground = Brush("#FFFFFF");
        agentWorkspace.Pane = new Border { BorderBrush = Brush("#E7E8ED"), BorderThickness = new Thickness(1, 0, 0, 0), Child = new ScrollViewer { Content = editor } };
        agentPanel.Children.Add(new Border { CornerRadius = new CornerRadius(10), BorderBrush = Brush("#E1E3E9"), BorderThickness = new Thickness(1), Child = agentWorkspace });
    }

    private void FilterAgents()
    {
        ApplyAgentFilters();
    }

    private Agent SelectedAgent() => SelectedAgentOrNull() ?? throw new InvalidOperationException("Sélectionnez d’abord un agent dans la liste.");
    private async Task ConnectAsync()
    {
        if (busy) return;
        var returnToTranscripts = viewingTranscripts;
        var returnToMap = viewingMap;
        var returnToDashboard = viewingDashboard;
        var returnToOverview = viewingOverview;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        signInCancellation = cancellation;
        cancelSignIn.Visibility = Visibility.Visible;
        connectionStatus.Text = "En attente dans le navigateur…";
        try
        {
            await Run(async () =>
            {
                notice.Title = "Connexion Microsoft";
                notice.Message = "Terminez la connexion dans votre navigateur. Vous pouvez l’annuler dans la barre latérale.";
                var username = await client.SignInAsync(cancellation.Token);
                // Authentication succeeds independently of Dataverse table access.
                authenticated = true;SetOperationsSession(username);
                allAgents.Clear(); FilterAgents();
                ClearTranscripts(); traces.Children.Clear(); traceCount = 0; traceCounter.Text = "0 appel";
                ClearDashboard();agentAnalyticsCache.Clear();
                await LoadPreferencesAsync(username);
                identityLabel.Text = username;
                accountName.Text = username;
                ToolTipService.SetToolTip(accountName, username);
                accountState.Text = "Connecté · Formation";
                avatarText.Text = string.IsNullOrWhiteSpace(username) ? "✓" : username[..1].ToUpperInvariant();
                signIn.Content = "Changer de compte";
                connectionStatus.Text = "Compte connecté";
                if (returnToOverview) ShowOverview(); else if (returnToDashboard) ShowDashboard(); else if (returnToMap) ShowAgentMap(); else if (returnToTranscripts) ShowTranscripts(); else ShowAgents();
                notice.Title = "Connexion réussie";
                notice.Message = "Chargement des agents Dataverse…";
                try { await LoadAgents(); if (returnToTranscripts) await LoadTranscriptsAsync(false); }
                catch (Exception ex)
                {
                    notice.Title = "Compte connecté · accès Dataverse à vérifier";
                    notice.Severity = InfoBarSeverity.Warning;
                    notice.Message = DescribeError(ex);
                    if (apiPane.Visibility != Visibility.Visible) ToggleApi();
                }
            });
        }
        finally
        {
            signInCancellation = null;
            cancelSignIn.Visibility = Visibility.Collapsed;
            connectionStatus.Text = authenticated ? "Compte connecté" : "Non connecté · réessayez";
        }
    }

    private static string DescribeError(Exception ex)
    {
        if(ex is TaskCanceledException{InnerException:TimeoutException})return "Dataverse n’a pas répondu dans le délai réseau. Vérifiez la connexion puis réessayez.";
        if(ex is OperationCanceledException&&ex.Message.StartsWith("La session a changé",StringComparison.Ordinal))return ex.Message;
        if (ex is OperationCanceledException)
            return "Connexion annulée ou délai de trois minutes dépassé. Fermez l’ancien onglet Microsoft puis réessayez.";
        if (ex is Microsoft.Identity.Client.MsalException msal)
        {
            var aad = System.Text.RegularExpressions.Regex.Match(msal.Message, @"AADSTS\d+").Value;
            var detail = aad switch
            {
                "AADSTS500113" or "AADSTS50011" => "Dans Entra > M365-Admin-Automation > Authentification, ajoutez http://localhost dans la plateforme Applications mobiles et de bureau.",
                "AADSTS650057" => "Ajoutez Dynamics CRM > Autorisations déléguées > user_impersonation dans les autorisations de l’application Entra.",
                "AADSTS65001" or "AADSTS90094" => "Le consentement Dataverse doit être accordé par un administrateur du tenant.",
                "AADSTS7000218" => "L’inscription est traitée comme un client confidentiel. Configurez http://localhost dans la plateforme Applications mobiles et de bureau pour cette application publique.",
                _ when msal.ErrorCode is "authentication_canceled" or "access_denied" => "Connexion annulée ou refusée. Cliquez sur Se connecter pour recommencer.",
                _ when msal is Microsoft.Identity.Client.MsalUiRequiredException => "La session Microsoft nécessite une nouvelle connexion. Cliquez sur Changer de compte.",
                _ => "La connexion n’a pas abouti. Vérifiez l’adresse de retour http://localhost, le consentement Dataverse et votre réseau, puis réessayez."
            };
            return $"{detail}\nCode : {(aad.Length > 0 ? aad : msal.ErrorCode)} · Corrélation : {msal.CorrelationId}";
        }
        if (ex is HttpRequestException httpError)
            return httpError.StatusCode switch
            {
                System.Net.HttpStatusCode.Forbidden => "Microsoft vous a connecté, mais Dataverse refuse l’accès (403). Vérifiez les rôles de sécurité de ce compte dans l’environnement Formation. La réponse détaillée est dans Appels Dataverse.",
                System.Net.HttpStatusCode.Unauthorized => "Dataverse a refusé le jeton (401). Reconnectez-vous avec le compte du tenant de formation.",
                _ => httpError.Message
            };
        return ex.Message;
    }

    private Task LoadAgents() => LoadAgents(null);
    private async Task LoadAgents(Guid? selectedId)
    {
        var result = await client.ListAgentsAsync();
        allAgents = result;
        RefreshLanguages();
        FilterAgents();
        if (selectedId.HasValue) SelectAgent(result.FirstOrDefault(x => x.Id == selectedId));
        notice.IsOpen = false;
    }

    private async Task Run(Func<Task> operation)
    {
        if (busy) return;
        busy = true;
        signIn.IsEnabled = false;
        foreach (var button in actions) button.IsEnabled = false;
        notice.IsOpen = true;
        notice.Title = "";
        notice.Severity = InfoBarSeverity.Informational;
        notice.Message = "Opération en cours…";
        try { await operation(); }
        catch (Exception ex)
        {
            notice.Severity = InfoBarSeverity.Error;
            notice.Message = DescribeError(ex);
        }
        finally { busy = false; signIn.IsEnabled = true; foreach (var button in actions) button.IsEnabled = true; }
    }

    private void AddTrace(ApiTrace trace)
    {
        traceCounter.Text = $"{++traceCount} appel(s) · dernier : {DateTime.Now:HH:mm:ss}";
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(Label($"{trace.Method}   {trace.Status?.ToString() ?? "ÉCHEC"}   ·   {trace.Milliseconds} ms", 13, trace.Status is >= 200 and < 300 ? "#107C10" : "#A4262C"));
        stack.Children.Add(Label(trace.Url, 11, "#605E5C"));
        stack.Children.Add(new Expander { Header = "Requête", HorizontalAlignment = HorizontalAlignment.Stretch, Content = Code(trace.Request) });
        stack.Children.Add(new Expander { Header = "Réponse", HorizontalAlignment = HorizontalAlignment.Stretch, IsExpanded = traceCount == 1, Content = Code(trace.Response) });
        traces.Children.Insert(0, Card(stack, "#FAFAFC"));
        // Bound UI memory during a training session.
        if (traces.Children.Count > 40) traces.Children.RemoveAt(40);
    }

    private Button ActionButton(string text, Func<Task> action)
    {
        var button = new Button { Content = text, Background = Brush("#0F6CBD"), Foreground = Brush("#FFFFFF"), Padding = new Thickness(14, 8, 14, 8) };
        button.Click += async (_, _) => await Run(action);
        actions.Add(button);
        return button;
    }
    private static FrameworkElement Code(string text) => new ScrollViewer { MaxHeight = 340, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new TextBlock { Text = text, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 11, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.NoWrap } };
    private static Border Feature(string glyph, string title, string description)
    {
        var grid = new Grid { ColumnSpacing = 18 };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(32) });
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new FontIcon { Glyph = glyph, FontSize = 24, Foreground = Brush("#6264A7") });
        var text = new StackPanel { Spacing = 5 };
        text.Children.Add(Label(title, 15)); text.Children.Add(Label(description, 13, "#605E5C"));
        Place(grid, text, 0, 1);
        return Card(grid);
    }
    private static Button Nav(string glyph, string text, bool selected = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        row.Children.Add(new FontIcon { Glyph = glyph, FontSize = 17 }); row.Children.Add(Label(text, 14));
        return new Button { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 12, 8, 12), Margin = new Thickness(0, 0, 8, 0), CornerRadius = new CornerRadius(17), BorderThickness = new Thickness(0), Background = selected ? Brush("#E7E7E7") : new SolidColorBrush(Colors.Transparent) };
    }
    private static TextBlock Label(string text, double size, string color = "#242424") => new() { Text = text, FontSize = size, Foreground = Brush(color), TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Segoe UI"), IsTextSelectionEnabled = false };
    private static Border Card(UIElement child, string color = "#FFFFFF") => new() { Child = child, Background = Brush(color), CornerRadius = new CornerRadius(10), BorderBrush = Brush("#E5E5EA"), BorderThickness = new Thickness(1), Padding = new Thickness(22) };
    private static SolidColorBrush Brush(string hex) => new(Color.FromArgb(255, Convert.ToByte(hex.Substring(1, 2), 16), Convert.ToByte(hex.Substring(3, 2), 16), Convert.ToByte(hex.Substring(5, 2), 16)));
    private static void Place(Grid grid, FrameworkElement element, int row, int column) { Grid.SetRow(element, row); Grid.SetColumn(element, column); grid.Children.Add(element); }
}
