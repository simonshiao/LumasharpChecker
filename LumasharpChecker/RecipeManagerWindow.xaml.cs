using System.Windows;

namespace LumasharpChecker;

public partial class RecipeManagerWindow : Window
{
    public RecipeManagerWindow(Window owner)
    {
        Resources.MergedDictionaries.Add(owner.Resources);
        InitializeComponent();
        Owner = owner;
        DataContext = owner.DataContext;
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();
}
