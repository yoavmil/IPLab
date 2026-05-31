using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace IPLab.UI.ViewModels;

public class ThumbnailStripViewModel : ViewModelBase
{
    private FlowViewModel? _flow;
    private readonly Action       _clearResults;
    private readonly Func<Task>   _runAsync;
    private readonly Action<string> _setStatus;

    public ObservableCollection<ThumbnailItemViewModel> Thumbnails { get; } = [];

    private bool _hasImageStrip;
    public bool HasImageStrip
    {
        get => _hasImageStrip;
        private set { _hasImageStrip = value; RaisePropertyChanged(); }
    }

    public ICommand AddImageCommand { get; }

    public bool IsEmpty => GetFilePathsParam()?.FileList.Count == 0;

    public string? ActiveFilePath
    {
        get
        {
            var fp = GetFilePathsParam();
            if (fp is null || fp.FileList.Count == 0) return null;
            int idx = int.TryParse(GetActiveIndexParam()?.ValueText, out var i) ? i : 0;
            return fp.FileList[Math.Clamp(idx, 0, fp.FileList.Count - 1)];
        }
    }

    public ThumbnailStripViewModel(Action clearResults, Func<Task> runAsync, Action<string> setStatus)
    {
        _clearResults   = clearResults;
        _runAsync       = runAsync;
        _setStatus      = setStatus;
        AddImageCommand = new RelayCommand(AddImages);
    }

    public void AttachToFlow(FlowViewModel flow)
    {
        if (_flow is not null)
            _flow.Nodes.CollectionChanged -= OnNodesChanged;
        _flow = flow;
        _flow.Nodes.CollectionChanged += OnNodesChanged;
        RebuildThumbnails();
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var fp = GetFilePathsParam();
        if (fp is null) return;
        bool wasEmpty = fp.FileList.Count == 0;
        foreach (var path in paths)
        {
            fp.FileList.Add(path);
            AddThumbnailItem(path, isActive: false);
        }
        if (wasEmpty && Thumbnails.Count > 0)
        {
            Thumbnails[0].IsActive = true;
            GetActiveIndexParam()!.ValueText = "0";
        }
    }

    private void OnNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => RebuildThumbnails();

    private OperatorNodeViewModel? GetLoadImageNode() =>
        _flow?.Nodes.FirstOrDefault(n => n.Parameters.Any(p => p.Name == "FilePaths" && p.IsStringList));

    private ParameterEditViewModel? GetFilePathsParam() =>
        GetLoadImageNode()?.Parameters.FirstOrDefault(p => p.Name == "FilePaths" && p.IsStringList);

    private ParameterEditViewModel? GetActiveIndexParam() =>
        GetLoadImageNode()?.Parameters.FirstOrDefault(p => p.Name == "ActiveIndex");

    private void RebuildThumbnails()
    {
        Thumbnails.Clear();
        var fp = GetFilePathsParam();
        if (fp is null) { HasImageStrip = false; return; }
        HasImageStrip = true;
        int active = int.TryParse(GetActiveIndexParam()?.ValueText, out var ai) ? ai : 0;
        for (int i = 0; i < fp.FileList.Count; i++)
            AddThumbnailItem(fp.FileList[i], i == active);
    }

    private void AddThumbnailItem(string path, bool isActive)
    {
        ThumbnailItemViewModel? item = null;
        item = new ThumbnailItemViewModel(
            path,
            LoadThumbnail(path),
            onSelect: () => SelectThumbnailAsync(item!),
            onRemove: () => RemoveThumbnail(item!));
        item.IsActive = isActive;
        Thumbnails.Add(item);
    }

    private void AddImages()
    {
        if (GetFilePathsParam() is null) return;
        var dialog = new OpenFileDialog
        {
            Title       = "Add Images",
            Filter      = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() != true) return;
        AddPaths(dialog.FileNames);
    }

    private async void SelectThumbnailAsync(ThumbnailItemViewModel item)
    {
        if (item.IsActive) return;
        foreach (var t in Thumbnails) t.IsActive = false;
        item.IsActive = true;
        var aip = GetActiveIndexParam();
        if (aip is not null) aip.ValueText = Thumbnails.IndexOf(item).ToString();
        _setStatus(item.FilePath);
        _clearResults();
        await _runAsync();
    }

    private void RemoveThumbnail(ThumbnailItemViewModel item)
    {
        var idx = Thumbnails.IndexOf(item);
        if (idx < 0) return;

        var fp  = GetFilePathsParam();
        var aip = GetActiveIndexParam();
        int activeIndex = int.TryParse(aip?.ValueText, out var ai) ? ai : 0;

        if (fp is not null && idx < fp.FileList.Count)
            fp.FileList.RemoveAt(idx);
        Thumbnails.RemoveAt(idx);

        if (Thumbnails.Count == 0)
        {
            if (aip is not null) aip.ValueText = "0";
            _clearResults();
            return;
        }

        if (item.IsActive)
        {
            int newActive = Math.Min(idx, Thumbnails.Count - 1);
            if (aip is not null) aip.ValueText = newActive.ToString();
            Thumbnails[newActive].IsActive = true;
            _ = _runAsync();
        }
        else if (idx < activeIndex && aip is not null)
            aip.ValueText = (activeIndex - 1).ToString();
    }

    private static BitmapSource? LoadThumbnail(string path)
    {
        try
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource        = new Uri(path, UriKind.Absolute);
            bi.DecodePixelWidth = 120;
            bi.CacheOption      = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
        catch { return null; }
    }
}
