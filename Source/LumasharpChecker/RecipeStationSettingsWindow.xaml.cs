using System.Windows;

namespace LumasharpChecker;

public partial class RecipeStationSettingsWindow : Window
{
    public RecipeStationSettingsWindow(Window owner)
    {
        Resources.MergedDictionaries.Add(owner.Resources);
        InitializeComponent();
        Owner = owner;
        DataContext = owner.DataContext;
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}
