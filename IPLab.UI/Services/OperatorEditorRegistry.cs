using IPLab.UI.Dialogs;
using IPLab.UI.ViewModels;

namespace IPLab.UI.Services;

public sealed record OperatorEditorContext(
    OperatorNodeViewModel Node,
    Func<string, object?> ResolveParameter,
    System.Windows.Window Owner);

/// <summary>
/// Registers operator-specific editor actions and creates the commands displayed for a selected node.
/// See <see cref="CreateDefault"/> for registration examples and <see cref="MainViewModel"/> for usage.
/// </summary>
public sealed class OperatorEditorRegistry
{
    private sealed record Registration(string Label, Action<OperatorEditorContext> Execute);
    private readonly Dictionary<string, List<Registration>> _registrations = [];

    public void Register(string operatorType, string label, Action<OperatorEditorContext> execute)
    {
        if (!_registrations.TryGetValue(operatorType, out var registrations))
            _registrations[operatorType] = registrations = [];
        registrations.Add(new Registration(label, execute));
    }

    public IReadOnlyList<OperatorActionViewModel> CreateActions(OperatorEditorContext context)
    {
        if (!_registrations.TryGetValue(context.Node.TypeName, out var registrations))
            return [];
        return registrations
            .Select(r => new OperatorActionViewModel(r.Label, () => r.Execute(context)))
            .ToList();
    }

    public static OperatorEditorRegistry CreateDefault()
    {
        var registry = new OperatorEditorRegistry();
        registry.Register("CSharpScript", "Browse / Create Script...",
            context => CSharpScriptService.BrowseScript(context.Node.Parameters));
        registry.Register("CSharpScript", "Scaffold Debug Project",
            context => CSharpScriptService.ScaffoldDebugProject(context.Node.Parameters));
        registry.Register("TemplateMatch", "Edit Template...", TemplateEditorWindow.TryOpen);
        return registry;
    }
}
