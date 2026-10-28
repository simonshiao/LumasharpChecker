using LumasharpChecker.viewmodel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LumasharpChecker;

public partial class TestImagePickerWindow : Window
{
    private TestImageTreeNode? _selectedNode;

    public string SelectedFilePath { get; private set; } = string.Empty;

    public TestImagePickerWindow(ObservableCollection<TestImageTreeNode> imageTree, string currentFilePath)
    {
        InitializeComponent();
        ImageTree.ItemsSource = imageTree;
        _selectedNode = FindNode(imageTree, currentFilePath);
        if (_selectedNode != null)
        {
            SelectionLabel.Text = _selectedNode.Name;
            SelectButton.IsEnabled = true;
        }
    }

    private void ImageTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _selectedNode = e.NewValue as TestImageTreeNode;
        bool canChoose = _selectedNode is { IsFolder: false };
        SelectButton.IsEnabled = canChoose;
        SelectionLabel.Text = canChoose ? _selectedNode!.Name : "請選擇一張圖片";
    }

    private void ImageTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_selectedNode is { IsFolder: false })
            AcceptSelection();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e) => AcceptSelection();

    private void AcceptSelection()
    {
        if (_selectedNode is not { IsFolder: false }) return;
        SelectedFilePath = _selectedNode.FilePath;
        DialogResult = true;
    }

    private static TestImageTreeNode? FindNode(IEnumerable<TestImageTreeNode> nodes, string path)
    {
        foreach (var node in nodes)
        {
            if (!node.IsFolder && string.Equals(node.FilePath, path, StringComparison.OrdinalIgnoreCase))
                return node;
            var child = FindNode(node.Children, path);
            if (child != null) return child;
        }
        return null;
    }
}
