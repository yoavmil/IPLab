using IPLab.UI.ViewModels;
using Nodify;
using Nodify.Interactivity;
using System.ComponentModel;
using System.Windows;

namespace IPLab.UI;

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

    protected override void OnClosing(CancelEventArgs e)
    {
        var vm = (MainViewModel)DataContext;
        if (!vm.ConfirmNavigateAway())
            e.Cancel = true;
        base.OnClosing(e);
    }
}
