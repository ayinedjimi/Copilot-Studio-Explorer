using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace StudioDataverse;

public sealed partial class MainWindow
{
    private AgentPreferences preferences = new();
    private string? preferencesPath;
    private readonly ComboBox personalView = Options("Vue", "Tous les agents", "Mes favoris", "Consultés récemment");
    private readonly TextBlock comparisonLabel = Label("Aucune référence choisie", 11, "#858895");
    private readonly Button favoriteButton = new() { Content = "☆ Favori" };
    private Agent? comparisonAnchor;
    private readonly SemaphoreSlim preferenceWrite = new(1, 1);

    private UIElement BuildPersonalViews()
    {
        var panel = new Grid { ColumnSpacing = 16 };
        panel.ColumnDefinitions.Add(new() { Width = new GridLength(200) });
        panel.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        personalView.SelectionChanged += (_, _) => FilterAgents();
        panel.Children.Add(personalView);
        var help = Label("Ctrl+F  Rechercher   ·   Ctrl+R  Actualiser   ·   Ctrl+D  Favori", 11, "#858895");
        help.VerticalAlignment = VerticalAlignment.Bottom; help.Margin = new Thickness(0, 0, 0, 10);
        Place(panel, help, 0, 1);
        return panel;
    }

    private UIElement BuildProductivityActions()
    {
        var panel = new StackPanel { Spacing = 10 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        favoriteButton.Click += async (_, _) => await ToggleFavoriteAsync();
        row.Children.Add(favoriteButton);
        var summary = new Button { Content = "Copier la fiche" };
        summary.Click += (_, _) =>
        {
            if (SelectedAgentOrNull() is not Agent agent) return;
            CopyText($"{agent.Name}\nSchéma : {agent.Schema}\nID : {agent.Id}\nÉtat : {agent.StatusLabel}\nLangue : {LanguageName(agent.Language)}\nModifié : {agent.ModifiedLabel}\nEnvironnement : {DataverseClient.Organization}");
            detailStatus.Text = "Fiche copiée dans le presse-papiers";
        };
        row.Children.Add(summary); panel.Children.Add(row);
        var compareRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var pin = new Button { Content = "Choisir comme référence" };
        pin.Click += (_, _) => { if (SelectedAgentOrNull() is Agent selected) { comparisonAnchor = selected; comparisonLabel.Text = "Référence : " + selected.Name; } };
        var compare = new Button { Content = "Comparer" };
        compare.Click += async (_, _) => await Run(CompareAgentsAsync);
        actions.Add(compare);
        compareRow.Children.Add(pin); compareRow.Children.Add(compare);
        panel.Children.Add(compareRow); panel.Children.Add(comparisonLabel);
        return panel;
    }

    private async Task LoadPreferencesAsync(string username)
    {
        comparisonAnchor = null; comparisonLabel.Text = "Aucune référence choisie";
        personalView.SelectedIndex = 0;
        preferences = new();
        preferencesPath = AgentPreferences.PathFor(username);
        try { preferences = await AgentPreferences.LoadAsync(preferencesPath); }
        catch (IOException) { connectionStatus.Text = "Préférences locales indisponibles"; }
        catch (UnauthorizedAccessException) { connectionStatus.Text = "Préférences locales indisponibles"; }
    }

    private async Task SavePreferencesAsync()
    {
        var path = preferencesPath;
        var snapshot = new AgentPreferences { Favorites = [.. preferences.Favorites], Recent = [.. preferences.Recent] };
        if (path == null) return;
        await preferenceWrite.WaitAsync();
        try { await snapshot.SaveAsync(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { detailStatus.Text = "Préférences modifiées pour cette session ; sauvegarde locale indisponible."; }
        finally { preferenceWrite.Release(); }
    }

    private async Task ToggleFavoriteAsync()
    {
        if (SelectedAgentOrNull() is not Agent agent) return;
        var favorite = preferences.Toggle(agent.Id);
        favoriteButton.Content = favorite ? "★ Retirer le favori" : "☆ Favori";
        FilterAgents();
        await SavePreferencesAsync();
    }

    private void InstallShortcuts(FrameworkElement root)
    {
        void Shortcut(VirtualKey key, Action action)
        {
            var shortcut = new KeyboardAccelerator { Key = key, Modifiers = VirtualKeyModifiers.Control };
            shortcut.Invoked += (_, args) => { action(); args.Handled = true; };
            root.KeyboardAccelerators.Add(shortcut);
        }
        Shortcut(VirtualKey.F, () => { ShowAgents(); agentSearch.Focus(FocusState.Programmatic); agentSearch.SelectAll(); });
        Shortcut(VirtualKey.R, async () => await Run(LoadAgents));
        Shortcut(VirtualKey.D, async () => await ToggleFavoriteAsync());
    }

    private async Task CompareAgentsAsync()
    {
        var current = SelectedAgent();
        var reference = comparisonAnchor ?? throw new InvalidOperationException("Choisissez un premier agent comme référence, puis sélectionnez un autre agent et cliquez sur Comparer.");
        if (reference.Id == current.Id) throw new InvalidOperationException("Sélectionnez un autre agent pour le comparer à la référence.");
        var leftJson = await client.DetailsAsync(reference);
        var rightJson = await client.DetailsAsync(current);
        using var leftDoc = JsonDocument.Parse(leftJson);
        using var rightDoc = JsonDocument.Parse(rightJson);
        var left = leftDoc.RootElement.EnumerateObject().Where(p => !p.Name.Contains('@') && p.Name != "iconbase64").ToDictionary(p => p.Name, p => p.Value.GetRawText());
        var right = rightDoc.RootElement.EnumerateObject().Where(p => !p.Name.Contains('@') && p.Name != "iconbase64").ToDictionary(p => p.Name, p => p.Value.GetRawText());
        var rows = new StackPanel { Spacing = 1 };
        var onlyDifferences = new CheckBox { Content = "Afficher uniquement les différences", IsChecked = true };
        var body = new StackPanel { Spacing = 16, MinWidth = 400 };
        body.Children.Add(Label(reference.Name + "  ↔  " + current.Name, 16));
        body.Children.Add(onlyDifferences);
        body.Children.Add(new ScrollViewer { Content = rows, MaxHeight = 450, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        void Render()
        {
            rows.Children.Clear();
            foreach (var key in left.Keys.Union(right.Keys).Order())
            {
                var a = left.GetValueOrDefault(key, "(absent)"); var b = right.GetValueOrDefault(key, "(absent)");
                if (onlyDifferences.IsChecked == true && a == b) continue;
                var grid = new Grid { Padding = new Thickness(10), ColumnSpacing = 12, Background = Brush(a == b ? "#FAFAFC" : "#F2F0FC") };
                grid.ColumnDefinitions.Add(new() { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
                grid.Children.Add(Label(key, 11, "#787E8C"));
                Place(grid, Label(a.Length > 600 ? a[..600] + "…" : a, 12), 0, 1);
                Place(grid, Label(b.Length > 600 ? b[..600] + "…" : b, 12), 0, 2);
                rows.Children.Add(grid);
            }
        }
        onlyDifferences.Checked += (_, _) => Render(); onlyDifferences.Unchecked += (_, _) => Render(); Render();
        var dialog = new ContentDialog { Title = "Comparer les agents", Content = body, CloseButtonText = "Fermer", XamlRoot = ((FrameworkElement)Content).XamlRoot };
        dialog.Resources["ContentDialogMaxWidth"] = 1000d;
        await dialog.ShowAsync();
        notice.IsOpen = false;
    }
}
