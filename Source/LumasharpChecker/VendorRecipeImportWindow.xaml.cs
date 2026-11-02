using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using LumasharpChecker.model;

namespace LumasharpChecker;

public partial class VendorRecipeImportWindow : Window, INotifyPropertyChanged
{
    private readonly RecipeManager _recipeManager;
    private readonly IReadOnlyList<RecipeManager.RecipeManifest> _existingRecipes;
    private bool _isPreviewReady;
    private string _previewText = "正在比對原廠配方資料…";
    private string _statusMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<VendorRecipeImportCandidate> Candidates { get; } = new();
    public string SourceRoot { get; }
    public bool IsPreviewReady
    {
        get => _isPreviewReady;
        private set { _isPreviewReady = value; OnPropertyChanged(); }
    }
    public string PreviewText
    {
        get => _previewText;
        private set { _previewText = value; OnPropertyChanged(); }
    }
    public string StatusMessage
    {
        get => _statusMessage;
        private set { _statusMessage = value; OnPropertyChanged(); }
    }

    public IReadOnlyList<RecipeManager.VendorRecipeFolder> SelectedFolders =>
        Candidates.Where(candidate => candidate.IsSelected).Select(candidate => candidate.Folder).ToList();

    public VendorRecipeImportWindow(Window owner, RecipeManager recipeManager,
        IReadOnlyList<RecipeManager.RecipeManifest> existingRecipes, string sourceRoot)
    {
        Resources.MergedDictionaries.Add(owner.Resources);
        InitializeComponent();
        Owner = owner;
        DataContext = this;
        _recipeManager = recipeManager;
        _existingRecipes = existingRecipes;
        SourceRoot = Path.GetFullPath(sourceRoot);

        foreach (var folder in recipeManager.DiscoverVendorRecipeFolders(SourceRoot))
        {
            var existing = FindExisting(folder.Name);
            var candidate = new VendorRecipeImportCandidate(folder, existing != null);
            candidate.PropertyChanged += Candidate_PropertyChanged;
            Candidates.Add(candidate);
        }
        StatusMessage = Candidates.Count == 0
            ? "找不到含原廠設定檔的配方資料夾。"
            : $"找到 {Candidates.Count} 個配方資料夾，正在自動比對。";
        CompareSelectedCandidates();
    }

    private RecipeManager.RecipeManifest? FindExisting(string name) =>
        _existingRecipes.FirstOrDefault(recipe =>
            string.Equals(recipe.Name, name, StringComparison.OrdinalIgnoreCase));

    private void Candidate_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VendorRecipeImportCandidate.IsSelected))
            CompareSelectedCandidates();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var candidate in Candidates) candidate.IsSelected = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var candidate in Candidates) candidate.IsSelected = false;
    }

    private void CompareSelectedCandidates()
    {
        var selected = Candidates.Where(candidate => candidate.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "請至少選擇一個資料夾。";
            IsPreviewReady = false;
            return;
        }

        var output = new StringBuilder();
        int addedFolders = 0, removedFolders = 0, addedFiles = 0, modifiedFiles = 0, removedFiles = 0;
        int createdRecipes = 0, updatedRecipes = 0;
        bool hasErrors = false;
        int shownLines = 0;
        foreach (var candidate in selected)
        {
            var existing = FindExisting(candidate.Folder.Name);
            try
            {
                var diff = _recipeManager.CompareVendorSnapshot(candidate.Folder.SourcePath, existing);
                candidate.SetPreview(existing != null, diff);
                if (existing == null) createdRecipes++;
                else updatedRecipes++;
                addedFolders += diff.AddedFolders.Count;
                removedFolders += diff.RemovedFolders.Count;
                addedFiles += diff.Added.Count;
                modifiedFiles += diff.Modified.Count;
                removedFiles += diff.Removed.Count;
                output.AppendLine($"{candidate.Folder.Name}：{(existing == null ? "新建" : diff.HasChanges ? "更新" : "已是最新")}");
                output.AppendLine($"  資料夾 +{diff.AddedFolders.Count}／-{diff.RemovedFolders.Count}；檔案 +{diff.Added.Count}／~{diff.Modified.Count}／-{diff.Removed.Count}");
                AppendPaths(output, diff.AddedFolders, "+ 資料夾 ", ref shownLines);
                AppendPaths(output, diff.RemovedFolders, "- 資料夾 ", ref shownLines);
                AppendPaths(output, diff.Added, "+", ref shownLines);
                AppendPaths(output, diff.Modified, "~", ref shownLines);
                AppendPaths(output, diff.Removed, "-", ref shownLines);
                output.AppendLine();
            }
            catch (Exception ex)
            {
                candidate.SetError(ex.Message);
                output.AppendLine($"{candidate.Folder.Name}：無法比對，{ex.Message}");
                output.AppendLine();
                hasErrors = true;
            }
        }

        if (shownLines >= 200) output.AppendLine("差異明細超過 200 筆，以上僅顯示前 200 筆。");
        output.Insert(0, $"配方：新建 {createdRecipes} 個、比對更新 {updatedRecipes} 個；資料夾：新增 {addedFolders}、移除 {removedFolders}；檔案：新增 {addedFiles}、修改 {modifiedFiles}、移除 {removedFiles}。\r\n\r\n");
        PreviewText = output.ToString();
        IsPreviewReady = !hasErrors;
        StatusMessage = hasErrors ? "部分資料夾無法比對，請檢查明細後重新選取。" : $"比對完成，共 {selected.Count} 個配方。";
    }

    private static void AppendPaths(StringBuilder output, IEnumerable<string> paths,
        string prefix, ref int shownLines)
    {
        foreach (string path in paths)
        {
            if (shownLines >= 200) return;
            output.Append("  ").Append(prefix).Append(' ').AppendLine(path);
            shownLines++;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (!IsPreviewReady) return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class VendorRecipeImportCandidate : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private string _statusText;
    private string _diffSummary = "尚未比對";

    public event PropertyChangedEventHandler? PropertyChanged;
    public RecipeManager.VendorRecipeFolder Folder { get; }
    public bool ExistsLocally { get; private set; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }
    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }
    public string DiffSummary
    {
        get => _diffSummary;
        private set { _diffSummary = value; OnPropertyChanged(); }
    }

    public VendorRecipeImportCandidate(RecipeManager.VendorRecipeFolder folder, bool existsLocally)
    {
        Folder = folder;
        ExistsLocally = existsLocally;
        _statusText = existsLocally ? "本機已有同名配方" : $"{folder.StationCount} 站設定，將新建";
    }

    public void SetPreview(bool existsLocally, RecipeManager.VendorSnapshotDiff diff)
    {
        ExistsLocally = existsLocally;
        StatusText = existsLocally ? "本機已有同名配方" : $"{Folder.StationCount} 站設定，將新建";
        DiffSummary = existsLocally
            ? diff.HasChanges ? $"目錄 +{diff.AddedFolders.Count}/-{diff.RemovedFolders.Count}；檔案 +{diff.Added.Count}/~{diff.Modified.Count}/-{diff.Removed.Count}" : "已是最新"
            : $"新建（{diff.AddedFolders.Count} 個子資料夾／{diff.Added.Count} 個檔案）";
    }

    public void SetError(string message)
    {
        DiffSummary = "比對失敗";
        StatusText = message;
    }

    public void ClearPreview()
    {
        DiffSummary = "尚未比對";
        StatusText = ExistsLocally ? "本機已有同名配方" : $"{Folder.StationCount} 站設定，將新建";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

