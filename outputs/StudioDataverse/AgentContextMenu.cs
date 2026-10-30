using Microsoft.UI.Xaml.Controls;
namespace StudioDataverse;
public sealed partial class MainWindow
{
    private MenuFlyout CreateAgentMenu(Agent agent)
    {
        var menu = new MenuFlyout();
        var transcripts = new MenuFlyoutItem { Text = "Voir les transcriptions", Icon = new FontIcon { Glyph = "\uE8F2" } };
        transcripts.Click += async (_, _) => await Run(async () => { ShowTranscripts(agent.Id); await LoadTranscriptsAsync(false); });
        menu.Items.Add(transcripts);
        var components = new MenuFlyoutItem { Text = "Voir les composants", Icon = new FontIcon { Glyph = "\uE8A5" } };
        components.Click += async (_, _) => { ShowAgents(); SelectAgent(agent); await Run(async () => await LoadComponentsAsync(agent)); };
        menu.Items.Add(components); menu.Items.Add(new MenuFlyoutSeparator());
        var details = new MenuFlyoutItem { Text = "Afficher les détails" };
        details.Click += (_, _) => { ShowAgents(); SelectAgent(agent); agentWorkspace.IsPaneOpen = true; }; menu.Items.Add(details);
        var favorite = new MenuFlyoutItem { Text = "Favori" };
        favorite.Click += async (_, _) => { preferences.Toggle(agent.Id); FilterAgents(); if (SelectedAgentOrNull()?.Id == agent.Id) favoriteButton.Content = preferences.Favorites.Contains(agent.Id) ? "★ Retirer le favori" : "☆ Favori"; await SavePreferencesAsync(); };
        menu.Items.Add(favorite);
        var copyId = new MenuFlyoutItem { Text = "Copier l’identifiant" };
        copyId.Click += (_, _) => CopyText(agent.Id.ToString()); menu.Items.Add(copyId);
        var copySchema = new MenuFlyoutItem { Text = "Copier le nom de schéma" };
        copySchema.Click += (_, _) => CopyText(agent.Schema); menu.Items.Add(copySchema);
        var reference = new MenuFlyoutItem { Text = "Choisir comme référence de comparaison" };
        reference.Click += (_, _) => { comparisonAnchor = agent; comparisonLabel.Text = "Référence : " + agent.Name; }; menu.Items.Add(reference);
        menu.Opening += (_, _) => { transcripts.IsEnabled = components.IsEnabled = details.IsEnabled = favorite.IsEnabled = reference.IsEnabled = !busy; favorite.Text = preferences.Favorites.Contains(agent.Id) ? "Retirer des favoris" : "Ajouter aux favoris"; };
        return menu;
    }
}
