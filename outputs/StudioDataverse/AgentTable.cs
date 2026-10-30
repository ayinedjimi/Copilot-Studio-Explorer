using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
namespace StudioDataverse;
public sealed partial class MainWindow
{
    private Agent? SelectedAgentOrNull() => (agents.SelectedItem as ListViewItem)?.Tag as Agent;
    private void SelectAgent(Agent? agent)
    {
        var row = agent == null ? null : agents.Items.OfType<ListViewItem>().FirstOrDefault(item => (item.Tag as Agent)?.Id == agent.Id);
        if (agent != null && row == null && !rebindingAgents)
        {
            changingFilters = true;
            foreach (var box in new[] { stateFilter, publishedFilter, dateFilter, languageFilter, managedFilter, sortAgents, personalView }) box.SelectedIndex = 0;
            agentSearch.Text = ""; changingFilters = false; FilterAgents();
            row = agents.Items.OfType<ListViewItem>().FirstOrDefault(item => (item.Tag as Agent)?.Id == agent.Id);
        }
        agents.SelectedItem = row;
    }
    private static Grid AgentColumns()
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star), MinWidth = 280 });
        foreach (var width in new[] { 30, 125, 135, 80, 140, 140, 115, 160, 145 }) grid.ColumnDefinitions.Add(new() { Width = new GridLength(width) });
        return grid;
    }
    private Grid BuildAgentTable()
    {
        var table = new Grid { Background = Brush("#FFFFFF"), MinWidth = 1450 };
        table.RowDefinitions.Add(new() { Height = new GridLength(48) });
        table.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var headers = AgentColumns(); headers.Padding = new Thickness(20, 0, 20, 0); headers.Background = Brush("#FFFFFF");headers.BorderBrush=Brush("#EDEDED");headers.BorderThickness=new Thickness(0,0,0,1);
        Button Header(string title, int column)
        {
            var button = new Button { Content = title + "  ⌄", FontSize = 12, Foreground = Brush("#5E6675"), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, BorderThickness = new Thickness(0), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), Padding = new Thickness(4, 10, 4, 10) };
            Place(headers, button, 0, column); return button;
        }
        var name = Header("Nom", 0); var sorts = new MenuFlyout();
        foreach (var option in new[] { ("Nom croissant A → Z", 2), ("Nom décroissant Z → A", 3), ("Création récente", 4) })
        {
            var item = new MenuFlyoutItem { Text = option.Item1 }; item.Click += (_, _) => sortAgents.SelectedIndex = option.Item2; sorts.Items.Add(item);
        }
        name.Flyout = sorts;
        void Filter(Button header, ComboBox box)
        {
            box.Width = 230; header.Flyout = new Flyout { Content = box, Placement = FlyoutPlacementMode.Bottom };
            var title = header.Content.ToString()!;
            box.SelectionChanged += (_, _) => { header.Content = box.SelectedIndex > 0 ? title.Replace("⌄", "●") : title; header.Foreground = Brush(box.SelectedIndex > 0 ? "#6357D5" : "#5E6675"); };
        }
        Filter(Header("État", 2), stateFilter);
        foreach(var heading in new[]{("Plateforme",3),("Risques ⓘ",4),("Utilisateurs actifs",5),("Conversations",6),("Partagé avec",7),("Dernière utilisation",9)}){var label=Label(heading.Item1,12);label.VerticalAlignment=VerticalAlignment.Center;Place(headers,label,0,heading.Item2);}
        var modified = Header("Dernière mise à jour", 8);
        var dates = new StackPanel { Spacing = 10, Width = 240 }; dates.Children.Add(dateFilter);
        var newest = new Button { Content = "↓ Modifications récentes", HorizontalAlignment = HorizontalAlignment.Stretch }; newest.Click += (_, _) => sortAgents.SelectedIndex = 0;
        var oldest = new Button { Content = "↑ Modifications anciennes", HorizontalAlignment = HorizontalAlignment.Stretch }; oldest.Click += (_, _) => sortAgents.SelectedIndex = 1;
        dates.Children.Add(newest); dates.Children.Add(oldest); modified.Flyout = new Flyout { Content = dates };
        sortAgents.SelectionChanged += (_, _) => { name.Content = sortAgents.SelectedIndex is 2 or 3 ? "Nom " + (sortAgents.SelectedIndex == 2 ? "↑" : "↓") : "Nom  ⌄"; modified.Content = "Dernière mise à jour " + (sortAgents.SelectedIndex == 1 ? "↑" : "↓"); };
        table.Children.Add(headers);
        agents.MinHeight = 0; agents.MaxHeight = double.PositiveInfinity; agents.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Place(table, agents, 1, 0);
        emptyAgents.HorizontalAlignment = HorizontalAlignment.Center; emptyAgents.VerticalAlignment = VerticalAlignment.Center; emptyAgents.Margin = new Thickness(32);
        Place(table, emptyAgents, 1, 0);
        return table;
    }
    private ListViewItem CreateAgentRow(Agent agent)
    {
        var grid = AgentColumns(); grid.Padding = new Thickness(12,10,12,10); grid.MinHeight = 56; grid.BorderBrush = Brush("#F1F1F1"); grid.BorderThickness = new Thickness(0, 0, 0, 1);
        var identity = new Grid { ColumnSpacing = 12 }; identity.ColumnDefinitions.Add(new() { Width = new GridLength(36) }); identity.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        identity.Children.Add(new AgentAvatar { Initial = agent.Initial, IconBase64 = agent.IconBase64,Width=28,Height=28, IsHitTestVisible = false });
        var names = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        var title = Label(agent.Name, 12); title.TextWrapping = TextWrapping.NoWrap; title.TextTrimming = TextTrimming.CharacterEllipsis;
        var schema = Label(agent.Schema, 11, "#858895"); schema.TextWrapping = TextWrapping.NoWrap; schema.TextTrimming = TextTrimming.CharacterEllipsis;
        names.Children.Add(title);ToolTipService.SetToolTip(identity,agent.Name+"\n"+agent.Schema);Place(identity, names, 0, 1); grid.Children.Add(identity);
        var sample=agentAnalyticsCache.GetValueOrDefault(agent.Id);
        string[] values = [agent.StatusLabel,"Copilot Studio","—",sample is {IdentifiedUsers:>0}?sample.IdentifiedUsers.ToString():"—",sample?.Conversations.ToString()??"—","—",agent.ModifiedOn?.ToLocalTime().ToString("dd MMMM yyyy",System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))??"—","—"];
        for (var i = 0; i < values.Length; i++)
        {
            var cell = Label(i==0?"● "+values[i]:values[i], 11, i==0&&agent.State==0?"#168A45":"#536277"); cell.VerticalAlignment = VerticalAlignment.Center; cell.TextWrapping = TextWrapping.NoWrap; cell.TextTrimming = TextTrimming.CharacterEllipsis; cell.IsHitTestVisible = false; Place(grid, cell, 0, i + 2);
        }
        var row = new ListViewItem { Tag = agent, Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(8, 0, 8, 0) };
        var menu = CreateAgentMenu(agent); row.ContextFlyout = menu;
        grid.RightTapped += (_, args) => { args.Handled = true; menu.ShowAt(grid, new FlyoutShowOptions { Position = args.GetPosition(grid) }); };
        var more = new Button { Content = new FontIcon { Glyph = "\uE712", FontSize = 14 }, Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(6) };
        ToolTipService.SetToolTip(more, "Actions de l’agent"); more.Click += (_, _) => menu.ShowAt(more); Place(grid, more, 0, 1);
        return row;
    }
}
