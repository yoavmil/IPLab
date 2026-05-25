using System.Windows.Media.Imaging;

namespace IPLab.UI.ViewModels;

public class LayerViewModel : ViewModelBase
{
    public string OperatorId  { get; }
    public string Port        { get; }
    public string DisplayName { get; }
    public bool   IsOwnLayer  { get; }

    private BitmapSource? _image;
    public BitmapSource? Image
    {
        get => _image;
        set { _image = value; RaisePropertyChanged(); }
    }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { _isEnabled = value; RaisePropertyChanged(); }
    }

    private double _opacity = 1.0;
    public double Opacity
    {
        get => _opacity;
        set { _opacity = value; RaisePropertyChanged(); }
    }

    public LayerViewModel(string operatorId, string port, string displayName, bool isOwnLayer = false)
    {
        OperatorId  = operatorId;
        Port        = port;
        DisplayName = displayName;
        IsOwnLayer  = isOwnLayer;
        _isEnabled  = isOwnLayer; // predecessors off by default; own image on
    }
}
