namespace IPLab.UI.ViewModels;

public class SourceRefViewModel(string operatorId, string displayName, string port, Type dataType)
{
    public string OperatorId   { get; } = operatorId;
    public string DisplayName  { get; } = displayName;
    public string Port         { get; } = port;
    public Type   DataType     { get; } = dataType;
    public string DisplayLabel => $"{OperatorId}  {DisplayName}  ›  {Port}";
}
