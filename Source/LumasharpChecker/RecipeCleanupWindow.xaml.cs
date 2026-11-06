using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LumasharpChecker.model;
using LumasharpChecker.viewmodel;

namespace LumasharpChecker;

public partial class RecipeCleanupWindow : Window
{
    private readonly mainViewModel _vm;
    private List<RecipeChoice> _choices = new();

    private sealed class RecipeChoice
    {
        public RecipeManager.RecipeManifest Recipe { get; set; } = null!;
        public bool IsChecked { get; set; }
        public string Name => Recipe.Name;
        public string Details => Recipe.StationCount + " 站  ·  " + Recipe.Id.Substring(0, 8);
    }

    internal RecipeCleanupWindow(Window owner, mainViewModel vm)
    {
        InitializeComponent();
        Owner = owner;
        _vm = vm;
        RefreshLists();
    }

    private void RefreshLists()
    {
        _choices = _vm.Recipes.OrderBy(r => r.Name).Select(recipe => new RecipeChoice
        {
            Recipe = recipe
        }).ToList();
        ActiveRecipeList.ItemsSource = _choices;
        ArchivedRecipeList.ItemsSource = _vm.ListCleanedRecipes();
        UpdateCount();
        RestoreButton.IsEnabled = false;
    }

    private void UpdateCount()
    {
        int count = _choices.Count(choice => choice.IsChecked);
        SelectedCountText.Text = "已選取 " + count + " / " + _choices.Count + " 筆";
        CleanupButton.IsEnabled = count > 0;
    }

    private void ActiveRecipe_Click(object sender, RoutedEventArgs e) => UpdateCount();

    private void ArchivedRecipe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => RestoreButton.IsEnabled = ArchivedRecipeList.SelectedItem is RecipeManager.CleanedRecipeInfo;

    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private bool ConfirmTypedCleanup(int count)
    {
        string phrase = "清理 " + count + " 筆";
        var window = new Window
        {
            Title = "確認清理配方",
            Owner = this,
            Width = 465,
            Height = 250,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.White,
            FontFamily = FontFamily
        };
        var stack = new StackPanel { Margin = new Thickness(24) };
        stack.Children.Add(new TextBlock
        {
            Text = "將 " + count + " 份配方移出目前清單",
            FontSize = 17, FontWeight = FontWeights.SemiBold
        });
        stack.Children.Add(new TextBlock
        {
            Text = "所有本地配方檔案會移到可復原保留區；外部來源不變。請輸入「" + phrase + "」才可執行。",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 11, 0, 12)
        });
        var input = new TextBox { Height = 34, FontSize = 14, Padding = new Thickness(7, 5, 7, 5) };
        stack.Children.Add(input);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        var cancel = new Button { Content = "取消", Width = 85, Height = 34, Margin = new Thickness(0, 0, 9, 0) };
        var accept = new Button { Content = "確認清理", Width = 100, Height = 34, IsEnabled = false };
        input.TextChanged += (_, _) => accept.IsEnabled =
            string.Equals(input.Text.Trim(), phrase, StringComparison.Ordinal);
        cancel.Click += (_, _) => window.DialogResult = false;
        accept.Click += (_, _) => window.DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(accept);
        stack.Children.Add(buttons);
        window.Content = stack;
        window.Loaded += (_, _) => input.Focus();
        return window.ShowDialog() == true;
    }

    private void Cleanup_Click(object sender, RoutedEventArgs e)
    {
        var selected = _choices.Where(item => item.IsChecked).Select(item => item.Recipe).ToList();
        if (selected.Count == 0 || !ConfirmTypedCleanup(selected.Count)) return;
        int moved = 0;
        var errors = new List<string>();
        foreach (var recipe in selected)
        {
            try
            {
                _vm.CleanupRecipeToArchive(recipe);
                moved++;
            }
            catch (Exception ex)
            {
                errors.Add(recipe.Name + "：" + ex.GetBaseException().Message);
            }
        }
        RefreshLists();
        StatusText.Text = "已清理 " + moved + " / " + selected.Count + " 份。"
            + (errors.Count > 0 ? " 有 " + errors.Count + " 份失敗。" : " 可隨時由右側還原。");
        if (errors.Count > 0)
            MessageBox.Show(this, string.Join("\n", errors), "部分配方未清理",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (ArchivedRecipeList.SelectedItem is not RecipeManager.CleanedRecipeInfo selected) return;
        if (MessageBox.Show(this, "確定還原「" + selected.Name + "」至本地配方清單？",
                "還原配方", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        try
        {
            _vm.RestoreCleanedRecipe(selected);
            RefreshLists();
            StatusText.Text = "已還原「" + selected.Name + "」，可從配方管理載入。";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "無法還原：" + ex.GetBaseException().Message, "配方還原失敗",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
