using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LumasharpChecker.viewmodel;
using LumasharpChecker.view;

namespace LumasharpChecker;

public partial class DatabaseSetupWindow : Window
{
    public DatabaseSetupWindow(Window owner)
    {
        Resources.MergedDictionaries.Add(owner.Resources);
        InitializeComponent();
        Owner = owner;
        DataContext = owner.DataContext;
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    private void ConfigureOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = false;
        if (DataContext is not mainViewModel vm) return;
        try { vm.ConfigureWorkDatabaseOutput(this); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "輸出資料夾設定失敗：\n" + ex.Message,
                "作業圖庫設定", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ConfigureSharedReportSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = false;
        if (DataContext is not mainViewModel vm) return;
        try { vm.ConfigureSharedReportSourceFolder(this); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "作業圖庫來源路徑設定失敗：\n" + ex.Message,
                "作業圖庫設定", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ChooseOkFolder_Click(object sender, RoutedEventArgs e)
        => ChooseStationFolder(sender, true);

    private void ChooseNgFolder_Click(object sender, RoutedEventArgs e)
        => ChooseStationFolder(sender, false);

    private async void ClearFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not StationDatabasePathViewModel row
            || DataContext is not mainViewModel vm || button.Tag is not string category) return;
        e.Handled = true;
        try { await vm.ClearStationDatabaseAsync(row.Number, category); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"第 {row.Number:00} 站圖庫路徑無法清除：\n{ex.Message}",
                "清除圖庫路徑", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ChooseAllFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not StationDatabasePathViewModel row
            || DataContext is not mainViewModel vm) return;
        try
        {
            string? selected = ExplorerFolderPicker.PickFolder(this,
                $"第 {row.Number:00} 站 · 選擇 ALL 圖庫資料夾",
                string.IsNullOrWhiteSpace(row.AllPath) ? row.OkPath : row.AllPath);
            if (selected == null) return;
            await vm.SetStationAllDatabaseFolderAsync(row.Number, selected);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"第 {row.Number:00} 站 ALL 作業圖庫無法儲存：\n{ex.Message}",
                "作業圖庫設定", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ChooseStationFolder(object sender, bool isOk)
    {
        if (sender is not Button button || button.DataContext is not StationDatabasePathViewModel row
            || DataContext is not mainViewModel vm) return;
        string category = isOk ? "OK" : "NG";
        try
        {
            string? selected = ExplorerFolderPicker.PickFolder(this,
                $"第 {row.Number:00} 站 · 選擇 {category} 作業圖庫資料夾",
                isOk ? (string.IsNullOrWhiteSpace(row.OkPath) ? row.NgPath : row.OkPath)
                     : (string.IsNullOrWhiteSpace(row.NgPath) ? row.OkPath : row.NgPath));
            if (selected == null) return;
            string folderName = Path.GetFileName(selected.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.Equals(folderName, isOk ? "NG" : "OK", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show($"你選取的是 {folderName} 資料夾，與目前的 {category} 分類相反，請重新選取。",
                    "作業圖庫分類不符", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            await vm.SetStationDatabaseFolderAsync(row.Number, isOk, selected);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"第 {row.Number:00} 站 {category} 作業圖庫無法儲存：\n{ex.Message}",
                "作業圖庫設定", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
