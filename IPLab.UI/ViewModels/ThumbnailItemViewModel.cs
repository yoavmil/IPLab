using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IPLab.UI.ViewModels;

public class ThumbnailItemViewModel : ViewModelBase
{
    public string        FilePath  { get; }
    public BitmapSource? Thumbnail { get; }
    public ICommand      SelectCommand { get; }
    public ICommand      RemoveCommand { get; }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; RaisePropertyChanged(); }
    }

    public ThumbnailItemViewModel(string filePath, BitmapSource? thumbnail,
        Action onSelect, Action onRemove)
    {
        FilePath      = filePath;
        Thumbnail     = thumbnail;
        SelectCommand = new RelayCommand(onSelect);
        RemoveCommand = new RelayCommand(onRemove);
    }
}
