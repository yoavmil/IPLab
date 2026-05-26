using IPLab.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace IPLab.UI.Controls;

public partial class PipelineEditorControl : UserControl
{
    private MainViewModel? _vm;
    private int _closeSeq;

    private const double OpenDurationMs  = 250;
    private const double CloseDurationMs = 250;
    private const double SlideDistance   = 15; // px the panel drifts vertically while fading

    public PipelineEditorControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        => WireViewModel(e.NewValue as MainViewModel);

    private void WireViewModel(MainViewModel? vm)
    {
        if (_vm is not null)
            _vm.EditingNodeChanged -= OnEditingNodeChanged;
        _vm = vm;
        if (_vm is not null)
            _vm.EditingNodeChanged += OnEditingNodeChanged;
    }

    private void OnEditingNodeChanged(OperatorNodeViewModel? newNode)
    {
        if (newNode is null)
        {
            // Close: keep DisplayingNode unchanged until panel is fully hidden.
            if (SettingsPanel.Visibility == Visibility.Visible)
                AnimateClose(onComplete: () => _vm!.DisplayingNode = null);
        }
        else if (SettingsPanel.Visibility != Visibility.Visible)
        {
            // Open fresh: set content first, then fade in.
            _vm!.DisplayingNode = newNode;
            AnimateOpen();
        }
        else
        {
            // Switch: fade out the old content, swap, then fade in the new content.
            AnimateClose(onComplete: () =>
            {
                _vm!.DisplayingNode = newNode;
                AnimateOpen();
            });
        }
    }

    private void AnimateOpen()
    {
        // Clear any animation that may be holding the properties so local writes take effect.
        SettingsPanel.BeginAnimation(OpacityProperty, null);
        PanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);

        SettingsPanel.Opacity    = 0;
        PanelTranslate.Y         = SlideDistance;
        SettingsPanel.Visibility = Visibility.Visible;

        SettingsPanel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1,
            new Duration(TimeSpan.FromMilliseconds(OpenDurationMs)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior   = FillBehavior.HoldEnd
        });
        PanelTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(SlideDistance, 0,
            new Duration(TimeSpan.FromMilliseconds(OpenDurationMs)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior   = FillBehavior.HoldEnd
        });
    }

    private void AnimateClose(Action? onComplete = null)
    {
        // Sequence number guards against a stale Completed callback firing after
        // a newer AnimateClose call has already superseded this animation.
        var seq = ++_closeSeq;

        // Read current animated values so mid-flight reversals start from the right position.
        var fromOpacity = SettingsPanel.Opacity;
        var fromY       = PanelTranslate.Y;

        var opacityAnim = new DoubleAnimation(fromOpacity, 0,
            new Duration(TimeSpan.FromMilliseconds(CloseDurationMs)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior   = FillBehavior.HoldEnd
        };
        opacityAnim.Completed += (_, _) =>
        {
            if (_closeSeq != seq) return;
            // Release the animation holds so AnimateOpen can write local values directly.
            SettingsPanel.BeginAnimation(OpacityProperty, null);
            PanelTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            SettingsPanel.Visibility = Visibility.Collapsed;
            onComplete?.Invoke();
        };

        SettingsPanel.BeginAnimation(OpacityProperty, opacityAnim);
        PanelTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(fromY, SlideDistance,
                new Duration(TimeSpan.FromMilliseconds(CloseDurationMs)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
                FillBehavior   = FillBehavior.HoldEnd
            });
    }
}
