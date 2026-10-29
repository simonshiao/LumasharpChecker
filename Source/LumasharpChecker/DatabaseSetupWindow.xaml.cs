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

    private void ChooseOkFolder_Click(object sender, RoutedEventArgs e)
        => ChooseStationFolder(sender, true);

    private void ChooseNgFolder_Click(object sender, RoutedEventArgs e)
        => ChooseStationFolder(sender, false);

    private void ChooseStationFolder(object sender, bool isOk)
    {
        if (sender is not Button button || button.DataContext is not StationDatabasePathViewModel row
            || DataContext is not mainViewModel vm) return;
        string category = isOk ? "OK" : "NG";
        try
        {
            string? selected = ExplorerFolderPicker.PickFolder(this,
                $"第 {row.Number:00} 站 · 選擇 {category} 圖庫資料夾",
                isOk ? (string.IsNullOrWhiteSpace(row.OkPath) ? row.NgPath : row.OkPath)
                     : (string.IsNullOrWhiteSpace(row.NgPath) ? row.OkPath : row.NgPath));
            if (selected == null) return;
            string folderName = Path.GetFileName(selected.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.Equals(folderName, isOk ? "NG" : "OK", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show($"你選取的是 {folderName} 資料夾，與目前的 {category} 分類相反，請重新選取。",
                    "圖庫分類不符", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            vm.SetStationDatabaseFolder(row.Number, isOk, selected);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"第 {row.Number:00} 站 {category} 圖庫無法儲存：\n{ex.Message}",
                "圖庫設定失敗", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
