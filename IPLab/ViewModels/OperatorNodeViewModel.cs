using System.Collections.ObjectModel;
using System.Windows;
using IPLab.Core.Interfaces;

namespace IPLab.ViewModels;

public class OperatorNodeViewModel : ViewModelBase
{
    public string Id          { get; }
    public string DisplayName { get; }
    public string TypeName    { get; }

    private Point _location;
    public Point Location
    {
        get => _location;
        set { _location = value; RaisePropertyChanged(); }
    }

    public ObservableCollection<ConnectorViewModel> Inputs  { get; } = [];
    public ObservableCollection<ConnectorViewModel> Outputs { get; } = [];

    public OperatorNodeViewModel(IOperator op)
    {
        Id          = op.Id;
        DisplayName = op.DisplayName;
        TypeName    = op.Type.TypeName;

        foreach (var p in op.Type.ParameterSchema.Where(p => p.IsConnectable))
            Inputs.Add(new ConnectorViewModel(p.Name, p.Label));

        foreach (var port in op.Type.OutputPorts)
            Outputs.Add(new ConnectorViewModel(port, port));
    }
}
