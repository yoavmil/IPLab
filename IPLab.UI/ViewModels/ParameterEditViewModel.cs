using IPLab.Core.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace IPLab.UI.ViewModels;

public class ParameterEditViewModel : ViewModelBase
{
    public string                     Name        { get; }
    public string                     Label       { get; }
    public ParameterType              Type        { get; }
    public IReadOnlyList<string>      Options     { get; }
    public bool                       CanBeWired  { get; }
    public bool                       IsEnum       => Type == ParameterType.Enum;
    public bool                       IsStringList => Type == ParameterType.StringList;

    public ObservableCollection<string> FileList { get; } = [];
    public string FileListSummary => $"{FileList.Count} file(s)";

    private readonly double? _min;
    private readonly double? _max;

    public ObservableCollection<SourceRefViewModel> AvailableSources { get; }

    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        set { _isVisible = value; RaisePropertyChanged(); }
    }

    private bool _isWired;
    public bool IsWired
    {
        get => _isWired;
        set { _isWired = value; RaisePropertyChanged(); }
    }

    private SourceRefViewModel? _selectedSource;
    public SourceRefViewModel? SelectedSource
    {
        get => _selectedSource;
        set { _selectedSource = value; RaisePropertyChanged(); }
    }

    private string _valueText = string.Empty;
    public string ValueText
    {
        get => _valueText;
        set
        {
            _valueText = ClampText(value);
            RaisePropertyChanged();
        }
    }

    private string _selectedOption = string.Empty;
    public string SelectedOption
    {
        get => _selectedOption;
        set { _selectedOption = value; RaisePropertyChanged(); }
    }

    public ParameterEditViewModel(ParameterDescriptor schema, ParameterValue? value,
                                   IEnumerable<SourceRefViewModel> availableSources)
    {
        Name             = schema.Name;
        Label            = schema.Label;
        Type             = schema.Type;
        Options          = schema.Options ?? [];
        CanBeWired       = schema.IsConnectable;
        _isVisible       = !schema.IsHidden;
        AvailableSources = new ObservableCollection<SourceRefViewModel>(availableSources);

        if (schema.Min is not null && TryParseDouble(schema.Min.ToString(), out var mn)) _min = mn;
        if (schema.Max is not null && TryParseDouble(schema.Max.ToString(), out var mx)) _max = mx;

        if (value?.Source is { } src)
        {
            _isWired        = true;
            _selectedSource = AvailableSources.FirstOrDefault(s => s.OperatorId == src.OperatorId && s.Port == src.Port);
        }
        else
        {
            var raw = value?.Value ?? schema.DefaultValue;

            if (Type == ParameterType.StringList)
            {
                if (raw is string[] arr)
                    foreach (var path in arr) FileList.Add(path);
            }
            else
            {
                _valueText      = ClampText(raw?.ToString() ?? string.Empty);
                _selectedOption = Type == ParameterType.Enum
                    ? (raw?.ToString() ?? Options.FirstOrDefault() ?? string.Empty)
                    : string.Empty;
            }
        }
    }

    public ParameterValue ToParameterValue()
    {
        if (IsWired && SelectedSource is { } src)
            return new ParameterValue { Name = Name, Source = new SourceRef(src.OperatorId, src.Port) };

        if (IsStringList)
            return new ParameterValue { Name = Name, Value = FileList.ToArray() };

        return new ParameterValue { Name = Name, Value = CoercedValue() };
    }

    private string ClampText(string raw)
    {
        if ((_min is null && _max is null) || !TryParseDouble(raw, out var d))
            return raw;

        if (_min is { } lo && d < lo) d = lo;
        if (_max is { } hi && d > hi) d = hi;

        return (Type == ParameterType.Int)
            ? ((int)d).ToString()
            : d.ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryParseDouble(string? s, out double d) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out d);

    private object? CoercedValue() => Type switch
    {
        ParameterType.Enum   => SelectedOption,
        ParameterType.Double => double.TryParse(ValueText, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.0,
        ParameterType.Int    => int.TryParse(ValueText, out var i) ? i : 0,
        ParameterType.Bool   => bool.TryParse(ValueText, out var b) ? b : false,
        _                    => ValueText
    };
}
