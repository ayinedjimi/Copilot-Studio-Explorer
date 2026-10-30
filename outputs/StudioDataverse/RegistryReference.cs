using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace StudioDataverse;
public sealed partial class MainWindow
{
    private UIElement BuildRegistryTabs()
    {
        var tabs=new StackPanel{Orientation=Orientation.Horizontal,Spacing=20};
        var current=new StackPanel{Spacing=10};current.Children.Add(Label("Registre",12));current.Children.Add(new Border{Height=3,CornerRadius=new CornerRadius(2),Background=Brush("#0078D4")});tabs.Children.Add(current);
        var requests=Label("Requêtes",12,"#787E8C");ToolTipService.SetToolTip(requests,"Les demandes Agent 365 ne sont pas exposées par cette connexion Dataverse.");tabs.Children.Add(requests);
        return tabs;
    }
    private UIElement BuildFilterChips()
    {
        var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=7,VerticalAlignment=VerticalAlignment.Center};
        foreach(var entry in new[]{("État",stateFilter),("Type de solution",managedFilter),("Langue",languageFilter),("Publication",publishedFilter)})
        {
            var box=entry.Item2;var button=new Button{Content=entry.Item1,FontSize=11,Padding=new Thickness(10,4,10,4),CornerRadius=new CornerRadius(12),BorderThickness=new Thickness(0),Background=Brush("#F3F3F3")};
            var menu=new MenuFlyout();menu.Opening+=(_,_)=>
            {
                menu.Items.Clear();for(var i=0;i<box.Items.Count;i++){var index=i;var choice=(ComboBoxItem)box.Items[i];var item=new ToggleMenuFlyoutItem{Text=choice.Content?.ToString()??"",IsChecked=i==box.SelectedIndex};item.Click+=(_,_)=>box.SelectedIndex=index;menu.Items.Add(item);}
            };
            button.Flyout=menu;box.SelectionChanged+=(_,_)=>{button.Background=Brush(box.SelectedIndex>0?"#EAF3FF":"#F3F3F3");button.Foreground=Brush(box.SelectedIndex>0?"#006BC7":"#242424");};row.Children.Add(button);
        }
        return row;
    }
}
