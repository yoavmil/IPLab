namespace IPLab.UI.ViewModels;

public class SourceRefViewModel(string operatorId, string displayName, string port)
{
    public string OperatorId   { get; } = operatorId;
    public string DisplayName  { get; } = displayName;
    public string Port         { get; } = port;
    public string DisplayLabel => $"{DisplayName}  ›  {Port}";
}
