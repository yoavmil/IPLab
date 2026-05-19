using Nodify;
using Nodify.Interactivity;
using System.Windows;
using IPLab.ViewModels;

namespace IPLab;

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
