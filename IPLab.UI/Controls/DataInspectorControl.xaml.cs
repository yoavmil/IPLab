using IPLab.UI.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IPLab.UI.Controls;

public partial class DataInspectorControl : UserControl
{
    public DataInspectorControl() => InitializeComponent();

    // ── Multi-select ──────────────────────────────────────────────────────────

    private void OnTreeMouseDown(object sender, MouseButtonEventArgs e)
    {
        var node = HitTestNode(e.OriginalSource as DependencyObject);
        if (node is null) return;

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            node.IsSelected = !node.IsSelected;
            e.Handled = true; // prevent WPF from clearing selection via its own mechanism
        }
        else
        {
            ClearAllSelections();
            node.IsSelected = true;
        }
    }

    // ── Ctrl+C ────────────────────────────────────────────────────────────────

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl)))
            return;

        var selected = AllMaterializedNodes().Where(n => n.IsSelected).ToList();
        if (selected.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var n in selected)
            sb.AppendLine(n.ValueText);

        Clipboard.SetText(sb.ToString().TrimEnd());
        e.Handled = true;
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    private void OnCopyValue(object sender, RoutedEventArgs e)
    {
        if (GetContextNode(sender) is { } node)
            Clipboard.SetText(node.ValueText);
    }

    private void OnCopyAllItems(object sender, RoutedEventArgs e)
    {
        if (GetContextNode(sender) is not { } node) return;

        // Ensure children are materialised before copying.
        node.IsExpanded = true;

        var sb = new StringBuilder();
        foreach (var child in node.Children)
            if (child != DataNodeViewModel.Placeholder)
                sb.AppendLine(child.ValueText);

        if (sb.Length > 0)
            Clipboard.SetText(sb.ToString().TrimEnd());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DataNodeViewModel? GetContextNode(object menuItemSender) =>
        menuItemSender is MenuItem { Parent: ContextMenu { PlacementTarget: FrameworkElement { Tag: DataNodeViewModel node } } }
            ? node : null;

    private static DataNodeViewModel? HitTestNode(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TreeViewItem { DataContext: DataNodeViewModel node } &&
                node != DataNodeViewModel.Placeholder)
                return node;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void ClearAllSelections()
    {
        foreach (var node in AllMaterializedNodes())
            if (node.IsSelected) node.IsSelected = false;
    }

    private IEnumerable<DataNodeViewModel> AllMaterializedNodes()
    {
        if (DataContext is not MainViewModel vm) yield break;
        foreach (var node in Flatten(vm.DataNodes))
            yield return node;
    }

    private static IEnumerable<DataNodeViewModel> Flatten(IEnumerable<DataNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node == DataNodeViewModel.Placeholder) continue;
            yield return node;
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }
}
