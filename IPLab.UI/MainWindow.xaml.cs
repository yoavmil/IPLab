using IPLab.UI.ViewModels;
using Nodify;
using Nodify.Interactivity;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;

namespace IPLab.UI;

public partial class MainWindow : Window
{
    private MainViewModel? _vm;
    private bool _panelCurrentlyOpen = false;

    private const double PanelOpenHeight   = 240;
    private const double OpenDurationMs    = 150;
    private const double CloseDurationMs   = 100;

    static MainWindow()
    {
        NodifyEditor.AutoRegisterConnectionsLayer = false;
        EditorGestures.Mappings.Connection.Disconnect.Value =
            new System.Windows.Input.MouseGesture(System.Windows.Input.MouseAction.RightClick);
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        DataContextChanged += OnDataContextChanged;
        // DataContext is set above, so fire manually for initial wiring.
        WireViewModel(DataContext as MainViewModel);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        WireViewModel(e.NewValue as MainViewModel);
    }

    private void WireViewModel(MainViewModel? vm)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm = vm;
        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.EditingNode))
        {
            // Node switched while panel is open → close then reopen with new content.
            if (_vm!.IsSettingsPanelOpen && _panelCurrentlyOpen)
                AnimateCloseAndReopen();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsSettingsPanelOpen))
        {
            if (_vm!.IsSettingsPanelOpen && !_panelCurrentlyOpen)
                AnimateOpen();
            else if (!_vm!.IsSettingsPanelOpen && _panelCurrentlyOpen)
                AnimateClose();
        }
    }

    private void AnimateOpen()
    {
        _panelCurrentlyOpen = true;
        var anim = new DoubleAnimation
        {
            To               = PanelOpenHeight,
            Duration         = TimeSpan.FromMilliseconds(OpenDurationMs),
            EasingFunction   = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior     = FillBehavior.HoldEnd
        };
        SettingsPanel.BeginAnimation(MaxHeightProperty, anim);
    }

    private void AnimateClose(Action? onComplete = null)
    {
        _panelCurrentlyOpen = false;
        var anim = new DoubleAnimation
        {
            To               = 0,
            Duration         = TimeSpan.FromMilliseconds(CloseDurationMs),
            EasingFunction   = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior     = FillBehavior.HoldEnd
        };
        if (onComplete is not null)
            anim.Completed += (_, _) => onComplete();
        SettingsPanel.BeginAnimation(MaxHeightProperty, anim);
    }

    private void AnimateCloseAndReopen()
    {
        _panelCurrentlyOpen = false;
        AnimateClose(onComplete: () =>
        {
            _panelCurrentlyOpen = true;
            AnimateOpen();
        });
    }
}
