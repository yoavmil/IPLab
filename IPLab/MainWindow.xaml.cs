using Nodify;
using System.Windows;
using IPLab.ViewModels;

namespace IPLab;

public partial class MainWindow : Window
{
    static MainWindow()
    {
        NodifyEditor.AutoRegisterConnectionsLayer = false;
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
