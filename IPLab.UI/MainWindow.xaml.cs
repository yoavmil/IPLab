using Nodify;
using Nodify.Interactivity;
using System.Windows;
using IPLab.UI.ViewModels;

namespace IPLab.UI;

public partial class MainWindow : Window
{
    static MainWindow()
    {
        NodifyEditor.AutoRegisterConnectionsLayer = false;
        EditorGestures.Mappings.Connection.Disconnect.Value = new System.Windows.Input.MouseGesture(System.Windows.Input.MouseAction.RightClick);
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
