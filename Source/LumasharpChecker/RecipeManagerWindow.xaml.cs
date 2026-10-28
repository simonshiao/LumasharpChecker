using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LumasharpChecker.model;
using LumasharpChecker.viewmodel;

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

    private void RecipeItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: RecipeManager.RecipeManifest recipe }
            && DataContext is mainViewModel viewModel)
        {
            viewModel.SelectedRecipe = recipe;
            if (viewModel.LoadSelectedRecipeCommand.CanExecute(null))
                viewModel.LoadSelectedRecipeCommand.Execute(null);
            e.Handled = true;
        }
    }
}
