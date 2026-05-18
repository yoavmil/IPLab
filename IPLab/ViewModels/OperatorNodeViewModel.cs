using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using IPLab.Core.Interfaces;

namespace IPLab.ViewModels;

public class OperatorNodeViewModel : ViewModelBase
{
    public string     Id          { get; }
    public string     DisplayName { get; }
    public string     TypeName    { get; }
    public IOperator  Operator    { get; }

    private Point _location;
    public Point Location
    {
        get => _location;
        set { _location = value; RaisePropertyChanged(); }
    }

    public ObservableCollection<ConnectorViewModel>    Inputs     { get; } = [];
    public ObservableCollection<ConnectorViewModel>    Outputs    { get; } = [];
    public IReadOnlyList<ParameterEditViewModel>       Parameters { get; }
    public ICommand                                    OpenSettingsCommand { get; }

    public IEnumerable<ConnectorViewModel> TopConnectors    => Inputs.Concat(Outputs).Where(c => c.Side == ConnectionSide.Top);
    public IEnumerable<ConnectorViewModel> BottomConnectors => Inputs.Concat(Outputs).Where(c => c.Side == ConnectionSide.Bottom);
    public IEnumerable<ConnectorViewModel> LeftConnectors   => Inputs.Concat(Outputs).Where(c => c.Side == ConnectionSide.Left);
    public IEnumerable<ConnectorViewModel> RightConnectors  => Inputs.Concat(Outputs).Where(c => c.Side == ConnectionSide.Right);

    public OperatorNodeViewModel(IOperator op,
                                  IEnumerable<SourceRefViewModel> availableSources,
                                  Func<string, ConnectionSide>? getInputSide  = null,
                                  Func<string, ConnectionSide>? getOutputSide = null,
                                  Action<OperatorNodeViewModel>? onOpenSettings = null)
    {
        Operator    = op;
        Id          = op.Id;
        DisplayName = op.DisplayName;
        TypeName    = op.Type.TypeName;

        foreach (var p in op.Type.ParameterSchema.Where(p => p.IsConnectable))
            Inputs.Add(new ConnectorViewModel(p.Name, p.Label, getInputSide?.Invoke(p.Name) ?? ConnectionSide.Top));

        foreach (var port in op.Type.OutputPorts)
            Outputs.Add(new ConnectorViewModel(port, port, getOutputSide?.Invoke(port) ?? ConnectionSide.Bottom));

        var sources = availableSources.ToList();
        Parameters = op.Type.ParameterSchema
            .Select(schema =>
            {
                var value = op.Parameters.FirstOrDefault(p => p.Name == schema.Name);
                return new ParameterEditViewModel(schema, value, sources);
            })
            .ToList();

        OpenSettingsCommand = new RelayCommand(() => onOpenSettings?.Invoke(this));
    }
}
