# Nodify.Workflow Implementation Guide

This document contains everything needed to implement the Nodify.Workflow designer in a new WPF project. It is derived from the `Examples/Nodify.Workflow` example in the Nodify repository.

---

## 1. Project Setup

### Target framework and project file

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentIcons.Wpf" Version="2.0.320" />
    <PackageReference Include="ObservableCollections" Version="3.3.4" />
    <PackageReference Include="ObservableCollections.R3" Version="3.3.4" />
    <PackageReference Include="R3Extensions.WPF" Version="1.3.0" />
  </ItemGroup>

  <!-- Add Nodify.csproj and Nodify.Shared.csproj as project refs,
       or replace with their NuGet equivalents if available -->
  <ItemGroup>
    <ProjectReference Include="path\to\Nodify\Nodify.csproj" />
    <ProjectReference Include="path\to\Nodify.Shared\Nodify.Shared.csproj" />
  </ItemGroup>
</Project>
```

**Key NuGet packages:**
- `FluentIcons.Wpf` — fluent icon set for WPF
- `ObservableCollections` — `ObservableList<T>` with rich change events
- `ObservableCollections.R3` — R3 integration for observable collections (`ObserveAdd`, `ObserveRemove`, `ObserveClear`)
- `R3Extensions.WPF` — R3 reactive extensions for WPF, provides `BindableReactiveProperty<T>` and `ReactiveCommand`

### App.xaml — dark theme + resource setup

```xml
<Application x:Class="YourApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ThemeMode="Dark"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="/Common/ControlStyles.xaml" />
                <ResourceDictionary Source="/Common/CommonDataTemplates.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

### Critical static constructor in MainWindow.xaml.cs

```csharp
static MainWindow()
{
    // Must be set BEFORE InitializeComponent() — disables auto-registration
    // of the connections layer so the template controls it instead.
    NodifyEditor.AutoRegisterConnectionsLayer = false;
}
```

---

## 2. Folder Structure

```
YourApp/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Common/
│   ├── CommandViewModel.cs          ← command/toggle wrappers
│   ├── CommonDataTemplates.xaml     ← DataTemplates for CommandViewModel
│   ├── ControlStyles.xaml          ← button/toggle button styles
│   └── AnimationExtensions.cs      ← Task-based animation helpers
├── Designer/
│   ├── WorkflowDesignerViewModel.cs ← base + main + sub VM
│   ├── WorkflowStepViewModel.cs     ← single node model
│   ├── WorkflowStepConnectionViewModel.cs
│   ├── WorkflowDataTemplates.xaml  ← NodifyEditor templates
│   ├── WorkflowToolBar.xaml        ← bottom toolbar overlay
│   ├── DesignerGesturesExtensions.cs
│   └── ClampedViewportProperty.cs  ← viewport constraint
└── Shell/
    ├── ApplicationViewModel.cs      ← root VM
    ├── TitleBar.xaml
    └── WorkflowsPanel.xaml
```

---

## 3. ViewModels

### 3.1 WorkflowStepViewModel.cs

Represents one node on the canvas.

```csharp
using FluentIcons.Common;
using R3;
using System.Windows;
using System.Windows.Media;

namespace YourApp.Designer;

internal sealed class WorkflowStepViewModel(string name)
{
    public BindableReactiveProperty<string> Name { get; } = new(name);
    public BindableReactiveProperty<Icon?> Icon { get; } = new(null);
    public BindableReactiveProperty<Color> IconColor { get; } = new(Colors.White);

    public BindableReactiveProperty<Point> Position { get; } = new();
    public BindableReactiveProperty<Size> Size { get; } = new();

    // These are written to by the Connector.Anchor binding (OneWayToSource)
    public BindableReactiveProperty<Point> InAnchorPosition { get; } = new();
    public BindableReactiveProperty<Point> OutAnchorPosition { get; } = new();
}
```

### 3.2 WorkflowStepConnectionViewModel.cs

Represents one edge between two nodes.

```csharp
namespace YourApp.Designer;

internal class WorkflowStepConnectionViewModel(WorkflowStepViewModel from, WorkflowStepViewModel to)
{
    public WorkflowStepViewModel From { get; } = from;
    public WorkflowStepViewModel To { get; } = to;
}
```

### 3.3 WorkflowDesignerViewModel.cs

The base class and two concrete variants. The `IViewportSizeAware` interface allows the generic `ClampedViewportProperty<T>` to read the static `ViewportSize` without reflection.

```csharp
using FluentIcons.Common;
using Nodify.Interactivity;
using ObservableCollections;
using R3;
using System.Windows;
using System.Windows.Input;

namespace YourApp.Designer;

public interface IViewportSizeAware
{
    // Each concrete class must declare this as a static property
    abstract static BindableReactiveProperty<Size> ViewportSize { get; }
}

internal abstract class WorkflowDesignerViewModel<T>
    where T : IViewportSizeAware
{
    public BindableReactiveProperty<string> Name { get; }
    public ClampedViewportProperty<T> ViewportPosition { get; }

    public ObservableList<WorkflowStepViewModel> Steps { get; } = [];
    public ObservableList<WorkflowStepConnectionViewModel> Connections { get; } = [];

    public BindableReactiveProperty<WorkflowStepViewModel?> SelectedStep { get; } = new(null);
    public EditorGestures EditorGestures { get; } = new();

    public WorkflowDesignerViewModel()
    {
        Name = new BindableReactiveProperty<string>(string.Empty);
        ViewportPosition = new ClampedViewportProperty<T>(Steps);
        ConfigureDefaultGestures();
    }

    // Call this after adding initial Steps so ClampedViewportProperty subscribes
    public virtual void OnPostInitialize()
    {
        ViewportPosition.ObserveStepChanges();
    }

    protected virtual void ConfigureDefaultGestures()
    {
        EditorGestures.Editor.PanWithMouseWheel = true;
        EditorGestures.Editor.ZoomModifierKey = ModifierKeys.Control;
    }
}

// Read-only top-level overview workflow
internal sealed class MainWorkflowDesignerViewModel
    : WorkflowDesignerViewModel<MainWorkflowDesignerViewModel>, IViewportSizeAware
{
    public static BindableReactiveProperty<Size> ViewportSize { get; } = new();

    protected override void ConfigureDefaultGestures()
    {
        base.ConfigureDefaultGestures();
        EditorGestures.LockEditing();   // see DesignerGesturesExtensions below
    }
}

// Fully editable sub-workflow with lock/unlock toggle
internal sealed class SubWorkflowDesignerViewModel
    : WorkflowDesignerViewModel<SubWorkflowDesignerViewModel>, IViewportSizeAware
{
    private readonly EditorGestures _backupGestures = new();

    public static BindableReactiveProperty<Size> ViewportSize { get; } = new();

    public ToggleCommandViewModel LockViewCommand { get; }

    public SubWorkflowDesignerViewModel()
    {
        LockViewCommand = new ToggleCommandViewModel(new ReactiveCommand())
        {
            Icon = { Value = Icon.LockOpen },
            IconChecked = { Value = Icon.LockClosed },
        };

        LockViewCommand.IsChecked.Subscribe(value =>
        {
            if (value)
            {
                LockViewCommand.ToolTip.Value = "Unlock editing";
                _backupGestures.Apply(EditorGestures);
                EditorGestures.LockEditing();
            }
            else
            {
                LockViewCommand.ToolTip.Value = "Lock editing";
                EditorGestures.Apply(_backupGestures);
            }
        });

        ConfigureDefaultGestures();
    }

    public override void OnPostInitialize()
    {
        base.OnPostInitialize();
        ViewportPosition.EdgePadding = 200;  // extra padding around content bounds
    }

    protected override void ConfigureDefaultGestures()
    {
        base.ConfigureDefaultGestures();
        // Pan horizontally with no modifier, pan vertically with Shift
        EditorGestures.Editor.PanVerticalModifierKey = ModifierKeys.Shift;
        EditorGestures.Editor.PanHorizontalModifierKey = ModifierKeys.None;
        _backupGestures.Apply(EditorGestures);  // save for restore after unlock
    }
}
```

### 3.4 CommandViewModel.cs

Wrappers used together with `CommonDataTemplates.xaml` to render icon buttons and toggle buttons automatically via `ContentControl`.

```csharp
using FluentIcons.Common;
using R3;
using System.Windows.Media;

namespace YourApp.Common;

internal class CommandViewModel(ReactiveCommand command)
{
    public ReactiveCommand Command { get; } = command;
    public BindableReactiveProperty<string?> Label { get; } = new(null);
    public BindableReactiveProperty<string?> ToolTip { get; } = new(null);
    public BindableReactiveProperty<Icon?> Icon { get; } = new(null);
    public BindableReactiveProperty<IconVariant> IconVariant { get; } = new();
    public BindableReactiveProperty<Color?> IconColor { get; } = new();
}

internal class ToggleCommandViewModel(ReactiveCommand command) : CommandViewModel(command)
{
    public BindableReactiveProperty<bool> IsChecked { get; } = new();
    public BindableReactiveProperty<Icon?> IconChecked { get; } = new(null);
}
```

### 3.5 ApplicationViewModel.cs

Root ViewModel instantiated in MainWindow's XAML as DataContext.

```csharp
using FluentIcons.Common;
using ObservableCollections;
using R3;
using System.Windows;
using System.Windows.Media;

namespace YourApp.Shell;

internal sealed class ApplicationViewModel
{
    public string Title { get; } = "Workflow Designer";

    public ObservableList<SubWorkflowDesignerViewModel> Workflows { get; } = [];
    public BindableReactiveProperty<MainWorkflowDesignerViewModel> MainWorkflow { get; }
    public BindableReactiveProperty<SubWorkflowDesignerViewModel?> SelectedWorkflow { get; }
    public BindableReactiveProperty<bool> IsZenMode { get; } = new(false);

    public CommandViewModel RunWorkflowCommand { get; }
    public CommandViewModel SaveChangesCommand { get; }
    public ToggleCommandViewModel ToggleZenModeCommand { get; }
    public CommandViewModel OpenSettingsCommand { get; }

    public ApplicationViewModel()
    {
        MainWorkflow = new(new MainWorkflowDesignerViewModel());

        CreateDefaultWorkflows();
        SelectedWorkflow = new(Workflows[0]);

        RunWorkflowCommand = new(new ReactiveCommand())
        {
            Label = { Value = "Run" },
            ToolTip = { Value = "Run the main workflow" },
            Icon = { Value = Icon.Play },
            IconVariant = { Value = IconVariant.Filled },
            IconColor = { Value = Colors.LawnGreen }
        };

        SaveChangesCommand = new(new ReactiveCommand())
        {
            Label = { Value = "Save" },
            ToolTip = { Value = "Save all changes" },
            Icon = { Value = Icon.Save },
            IconColor = { Value = Colors.LightSkyBlue }
        };

        ToggleZenModeCommand = new(new ReactiveCommand())
        {
            Label = { Value = "Zen" },
            ToolTip = { Value = "Toggle zen mode" },
            Icon = { Value = Icon.ArrowMaximize },
            IconChecked = { Value = Icon.ArrowMinimize }
        };

        ToggleZenModeCommand.IsChecked.Subscribe(value => IsZenMode.Value = value);

        OpenSettingsCommand = new(new ReactiveCommand())
        {
            Icon = { Value = Icon.Settings },
            IconVariant = { Value = IconVariant.Filled },
            ToolTip = { Value = "Open application settings" },
        };
    }

    private void CreateDefaultWorkflows()
    {
        var runTests = CreateWorkflow("Run tests");
        Workflows.Add(runTests);
        runTests.OnPostInitialize();

        // Add steps and connections to MainWorkflow
        var step1 = new WorkflowStepViewModel("Run tests") { Position = { Value = new Point(130, 150) } };
        MainWorkflow.Value.Steps.Add(step1);
        MainWorkflow.Value.Name.Value = "Release pipeline";
        MainWorkflow.Value.OnPostInitialize();
    }

    private static SubWorkflowDesignerViewModel CreateWorkflow(string name)
    {
        var wf = new SubWorkflowDesignerViewModel
        {
            Name = { Value = name },
            ViewportPosition = { Value = new Point(-100, -200) }
        };

        var step1 = new WorkflowStepViewModel("Step 1")
        {
            Icon = { Value = Icon.BranchFork },
            IconColor = { Value = Colors.Orange },
            Position = { Value = new Point(50, 50) }
        };
        var step2 = new WorkflowStepViewModel("Step 2")
        {
            Icon = { Value = Icon.Settings },
            IconColor = { Value = Colors.MediumPurple },
            Position = { Value = new Point(250, 120) }
        };

        wf.Steps.Add(step1);
        wf.Steps.Add(step2);
        wf.Connections.Add(new WorkflowStepConnectionViewModel(step1, step2));

        return wf;
    }
}
```

---

## 4. ClampedViewportProperty.cs

Prevents the user from panning the viewport beyond the bounding box of all steps. Derives from `BindableReactiveProperty<Point>` and overrides `OnValueChanging` to clamp the incoming value.

```csharp
using ObservableCollections;
using R3;
using System.Windows;

namespace YourApp.Designer;

internal sealed class ClampedViewportProperty<T>(ObservableList<WorkflowStepViewModel> steps)
    : BindableReactiveProperty<Point>
    where T : IViewportSizeAware
{
    private double _minX = double.MaxValue, _minY = double.MaxValue;
    private double _maxX = double.MinValue, _maxY = double.MinValue;

    private readonly Dictionary<WorkflowStepViewModel, IDisposable> _stepSubscriptions = [];

    public int EdgePadding { get; set; } = 50;

    public void ObserveStepChanges()
    {
        foreach (var step in steps)
            SubscribeToStep(step);

        steps.ObserveAdd().Subscribe(e => SubscribeToStep(e.Value));
        steps.ObserveRemove().Subscribe(e => UnsubscribeFromStep(e.Value));
        steps.ObserveClear().Subscribe(_ => ClearSubscriptions());
    }

    private void SubscribeToStep(WorkflowStepViewModel step)
    {
        var subscription = new CompositeDisposable
        {
            step.Position.Subscribe(_ => RecalculateBounds()),
            step.Size.Subscribe(_ => RecalculateBounds())
        };
        _stepSubscriptions[step] = subscription;
        RecalculateBounds();
    }

    private void UnsubscribeFromStep(WorkflowStepViewModel step)
    {
        if (_stepSubscriptions.Remove(step, out var sub))
            sub.Dispose();
        RecalculateBounds();
    }

    private void ClearSubscriptions()
    {
        foreach (var sub in _stepSubscriptions.Values)
            sub.Dispose();
        _stepSubscriptions.Clear();
        RecalculateBounds();
    }

    private void RecalculateBounds()
    {
        if (steps.Count == 0)
        {
            _minX = double.MaxValue; _minY = double.MaxValue;
            _maxX = double.MinValue; _maxY = double.MinValue;
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var step in steps)
        {
            var pos = step.Position.Value;
            var size = step.Size.Value;
            minX = Math.Min(minX, pos.X);
            minY = Math.Min(minY, pos.Y);
            maxX = Math.Max(maxX, pos.X + size.Width);
            maxY = Math.Max(maxY, pos.Y + size.Height);
        }

        _minX = minX; _minY = minY;
        _maxX = maxX; _maxY = maxY;
    }

    protected override void OnValueChanging(ref Point value)
    {
        var viewportSize = T.ViewportSize.Value;
        if (viewportSize.Width == 0 || viewportSize.Height == 0) return;

        var (min, max) = GetViewportBounds(viewportSize);
        value = new Point(Math.Clamp(value.X, min.X, max.X), Math.Clamp(value.Y, min.Y, max.Y));
    }

    private (Point min, Point max) GetViewportBounds(Size viewportSize)
    {
        if (steps.Count == 0)
            return (new Point(double.MinValue, double.MinValue), new Point(double.MaxValue, double.MaxValue));

        double minBoundX = _minX - EdgePadding;
        double minBoundY = _minY - EdgePadding;
        double maxBoundX = _maxX + EdgePadding - viewportSize.Width;
        double maxBoundY = _maxY + EdgePadding - viewportSize.Height;

        return (
            new Point(Math.Min(minBoundX, maxBoundX), Math.Min(minBoundY, maxBoundY)),
            new Point(Math.Max(minBoundX, maxBoundX), Math.Max(minBoundY, maxBoundY))
        );
    }
}
```

---

## 5. DesignerGesturesExtensions.cs

Makes an `EditorGestures` read-only by unbinding all editing-related gestures, then re-binds pan to left/right/middle mouse button so the user can still navigate.

```csharp
using Nodify.Interactivity;
using System.Windows.Input;

namespace YourApp.Designer;

internal static class DesignerGesturesExtensions
{
    public static void LockEditing(this EditorGestures gestures)
    {
        gestures.Editor.Selection.Unbind();
        gestures.Editor.SelectAll.Unbind();
        gestures.Editor.Cutting.Unbind();
        gestures.Editor.PushItems.Unbind();

        gestures.Editor.Keyboard.ToggleSelected.Unbind();
        gestures.Editor.Keyboard.DragSelection.Unbind();
        gestures.Editor.Keyboard.DeselectAll.Unbind();

        gestures.ItemContainer.Selection.Unbind();
        gestures.ItemContainer.Drag.Unbind();

        gestures.Connection.Disconnect.Unbind();
        gestures.Connection.Split.Unbind();
        gestures.Connection.Selection.Unbind();

        gestures.Connector.Connect.Unbind();
        gestures.Connector.Disconnect.Unbind();

        // Allow panning with any mouse button while locked
        gestures.Editor.Pan.Value = new AnyGesture(
            new Interactivity.MouseGesture(MouseAction.LeftClick),
            new Interactivity.MouseGesture(MouseAction.RightClick),
            new Interactivity.MouseGesture(MouseAction.MiddleClick));
    }
}
```

---

## 6. XAML Templates

### 6.1 WorkflowDataTemplates.xaml

Place this in `Designer/WorkflowDataTemplates.xaml` and merge it into MainWindow's resources.

Key points:
- `ItemContainerStyle` binds `Location` ↔ `Position.Value` (TwoWay) and `ActualSize` → `Size.Value` (OneWayToSource)
- `Connector.Anchor` is bound `OneWayToSource` to write back to `InAnchorPosition.Value` / `OutAnchorPosition.Value`
- Sub-workflow uses `StepConnection`; main workflow uses `LineConnection` with `SourceOrientation="Vertical"`
- `ViewportSize` is bound `OneWayToSource` to the static property on each designer VM type

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="clr-namespace:YourApp.Designer"
                    xmlns:nodify="https://miroiu.github.io/nodify"
                    xmlns:ic="clr-namespace:FluentIcons.Wpf;assembly=FluentIcons.Wpf"
                    xmlns:shared="clr-namespace:Nodify;assembly=Nodify.Shared">

    <!-- ItemContainer base style -->
    <Style x:Key="ItemContainerStyle" TargetType="{x:Type nodify:ItemContainer}">
        <Setter Property="BorderThickness" Value="1" />
        <Setter Property="SelectedBorderThickness" Value="1" />
        <Setter Property="Background" Value="{DynamicResource ControlFillColorDefaultBrush}" />
        <Setter Property="BorderBrush" Value="{DynamicResource ControlElevationBorderBrush}" />
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{DynamicResource ControlFillColorSecondaryBrush}" />
            </Trigger>
            <MultiTrigger>
                <MultiTrigger.Conditions>
                    <Condition Property="IsSelected" Value="True" />
                    <Condition Property="IsPreviewingSelection" Value="{x:Null}" />
                </MultiTrigger.Conditions>
                <MultiTrigger.Setters>
                    <Setter Property="Background" Value="{DynamicResource AccentFillColorDefaultBrush}" />
                    <Setter Property="Foreground" Value="{DynamicResource TextOnAccentFillColorPrimaryBrush}" />
                </MultiTrigger.Setters>
            </MultiTrigger>
            <Trigger Property="IsPreviewingSelection" Value="True">
                <Setter Property="Background" Value="{DynamicResource AccentFillColorDefaultBrush}" />
                <Setter Property="Foreground" Value="{DynamicResource TextOnAccentFillColorPrimaryBrush}" />
            </Trigger>
        </Style.Triggers>
    </Style>

    <!-- Cutting line -->
    <Style x:Key="CuttingLineStyle" TargetType="{x:Type nodify:CuttingLine}"
           BasedOn="{StaticResource {x:Type nodify:CuttingLine}}">
        <Setter Property="StrokeDashArray" Value="1 5" />
        <Setter Property="StrokeThickness" Value="2" />
        <Setter Property="Stroke" Value="{DynamicResource AccentFillColorDefaultBrush}" />
        <Setter Property="Fill" Value="{DynamicResource AccentFillColorTertiaryBrush}" />
    </Style>

    <!-- Pushed area -->
    <Style x:Key="PushedAreaStyle" TargetType="Rectangle">
        <Setter Property="StrokeThickness" Value="1" />
        <Setter Property="Stroke" Value="{DynamicResource AccentFillColorTertiaryBrush}" />
        <Setter Property="Fill" Value="{DynamicResource ControlFillColorDefaultBrush}" />
    </Style>

    <!-- Shared connection style -->
    <Style x:Key="ConnectionStyle" TargetType="{x:Type nodify:BaseConnection}">
        <Setter Property="SourceOffset" Value="8 0" />
        <Setter Property="TargetOffset" Value="8 0" />
        <Setter Property="Stroke" Value="{DynamicResource AccentFillColorTertiaryBrush}" />
        <Setter Property="Fill" Value="{DynamicResource AccentFillColorTertiaryBrush}" />
    </Style>

    <!-- ==================== SUB WORKFLOW ==================== -->

    <!-- Horizontal connector (left/right pill) -->
    <Style x:Key="SubStepConnectorStyle" TargetType="{x:Type nodify:Connector}">
        <Setter Property="Height" Value="16" />
        <Setter Property="Background" Value="{DynamicResource AccentFillColorDefaultBrush}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type nodify:Connector}">
                    <Border Background="Transparent" Width="12">
                        <Border x:Name="PART_Connector"
                                Background="{TemplateBinding Background}"
                                HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                Width="3" CornerRadius="1" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Node template for sub-workflow: [In] Icon Name [Out] -->
    <DataTemplate x:Key="SubWorkflowStepTemplate" DataType="{x:Type local:WorkflowStepViewModel}">
        <Border CornerRadius="{DynamicResource ControlCornerRadius}">
            <StackPanel Orientation="Horizontal">
                <!-- Left (input) connector — writes anchor position back to VM -->
                <nodify:Connector Anchor="{Binding InAnchorPosition.Value, Mode=OneWayToSource}"
                                  HorizontalContentAlignment="Left"
                                  IsConnected="True"
                                  Style="{StaticResource SubStepConnectorStyle}" />

                <Grid Margin="8">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto" />
                        <ColumnDefinition Width="*" />
                    </Grid.ColumnDefinitions>

                    <!-- Icon badge -->
                    <Border Background="{DynamicResource SolidBackgroundFillColorBaseBrush}"
                            CornerRadius="{DynamicResource OverlayCornerRadius}"
                            Padding="6" Margin="0 0 8 0">
                        <ic:FluentIcon Icon="{Binding Icon.Value}"
                                       IconSize="Size20" FontSize="16"
                                       VerticalAlignment="Center"
                                       Foreground="{Binding IconColor.Value, Converter={shared:ColorToSolidColorBrushConverter}}"
                                       Visibility="{Binding Icon.Value, Converter={shared:NullToVisibilityConverter}}" />
                    </Border>

                    <TextBlock Grid.Column="1" Text="{Binding Name.Value}"
                               VerticalAlignment="Center" Margin="0 0 0 4"
                               Style="{StaticResource BodyStrongTextBlockStyle}" />
                </Grid>

                <!-- Right (output) connector -->
                <nodify:Connector Anchor="{Binding OutAnchorPosition.Value, Mode=OneWayToSource}"
                                  HorizontalContentAlignment="Right"
                                  IsConnected="True"
                                  Style="{StaticResource SubStepConnectorStyle}" />
            </StackPanel>
        </Border>
    </DataTemplate>

    <!-- Editor template for SubWorkflowDesignerViewModel -->
    <DataTemplate DataType="{x:Type local:SubWorkflowDesignerViewModel}">
        <Grid>
            <ScrollViewer CanContentScroll="True"
                          HorizontalScrollBarVisibility="Auto"
                          VerticalScrollBarVisibility="Auto">
                <nodify:NodifyEditor x:Name="Editor"
                                     ItemsSource="{Binding Steps}"
                                     SelectedItem="{Binding SelectedStep.Value, Mode=TwoWay}"
                                     Connections="{Binding Connections}"
                                     InputGestures="{Binding EditorGestures}"
                                     ViewportLocation="{Binding ViewportPosition.Value, Mode=TwoWay}"
                                     ViewportSize="{Binding Value, Source={x:Static local:SubWorkflowDesignerViewModel.ViewportSize}, Mode=OneWayToSource}"
                                     ItemTemplate="{StaticResource SubWorkflowStepTemplate}"
                                     CuttingLineStyle="{StaticResource CuttingLineStyle}"
                                     PushedAreaStyle="{StaticResource PushedAreaStyle}"
                                     Background="Transparent">
                    <nodify:NodifyEditor.ItemContainerStyle>
                        <Style BasedOn="{StaticResource ItemContainerStyle}"
                               TargetType="{x:Type nodify:ItemContainer}">
                            <!-- TwoWay: editor moves item, VM stores position -->
                            <Setter Property="Location" Value="{Binding Position.Value, Mode=TwoWay}" />
                            <!-- OneWayToSource: VM gets rendered size from editor -->
                            <Setter Property="ActualSize" Value="{Binding Size.Value, Mode=OneWayToSource}" />
                        </Style>
                    </nodify:NodifyEditor.ItemContainerStyle>

                    <nodify:NodifyEditor.ConnectionTemplate>
                        <DataTemplate DataType="{x:Type local:WorkflowStepConnectionViewModel}">
                            <!-- StepConnection = horizontal step-curve -->
                            <nodify:StepConnection Source="{Binding From.OutAnchorPosition.Value}"
                                                   Target="{Binding To.InAnchorPosition.Value}"
                                                   Style="{StaticResource ConnectionStyle}" />
                        </DataTemplate>
                    </nodify:NodifyEditor.ConnectionTemplate>

                    <nodify:NodifyEditor.Resources>
                        <Style TargetType="{x:Type nodify:PendingConnection}"
                               BasedOn="{StaticResource {x:Type nodify:PendingConnection}}">
                            <Setter Property="Stroke" Value="{DynamicResource AccentFillColorTertiaryBrush}" />
                        </Style>
                    </nodify:NodifyEditor.Resources>
                </nodify:NodifyEditor>
            </ScrollViewer>

            <!-- Toolbar overlay — positioned bottom center -->
            <local:WorkflowToolBar Editor="{Binding ., ElementName=Editor}"
                                   VerticalAlignment="Bottom"
                                   HorizontalAlignment="Center"
                                   Margin="32" />
        </Grid>
    </DataTemplate>

    <!-- ==================== MAIN WORKFLOW ==================== -->

    <!-- Vertical connector (top/bottom pill) -->
    <Style x:Key="MainStepConnectorStyle" TargetType="{x:Type nodify:Connector}">
        <Setter Property="Width" Value="16" />
        <Setter Property="Background" Value="{DynamicResource AccentFillColorDefaultBrush}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type nodify:Connector}">
                    <Border Background="Transparent" Height="12">
                        <Border x:Name="PART_Connector"
                                Background="{TemplateBinding Background}"
                                VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                Height="3" CornerRadius="1" />
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- Node template for main workflow: vertical layout [In / Name / Out] -->
    <DataTemplate x:Key="MainWorkflowStepTemplate" DataType="{x:Type local:WorkflowStepViewModel}">
        <Border CornerRadius="{DynamicResource ControlCornerRadius}">
            <StackPanel Orientation="Vertical">
                <nodify:Connector Anchor="{Binding InAnchorPosition.Value, Mode=OneWayToSource}"
                                  VerticalContentAlignment="Top"
                                  IsConnected="True"
                                  Style="{StaticResource MainStepConnectorStyle}" />
                <TextBlock Margin="12 0" Text="{Binding Name.Value}"
                           VerticalAlignment="Center"
                           Style="{StaticResource BodyStrongTextBlockStyle}" />
                <nodify:Connector Anchor="{Binding OutAnchorPosition.Value, Mode=OneWayToSource}"
                                  VerticalContentAlignment="Bottom"
                                  IsConnected="True"
                                  Style="{StaticResource MainStepConnectorStyle}" />
            </StackPanel>
        </Border>
    </DataTemplate>

    <!-- Editor template for MainWorkflowDesignerViewModel (read-only) -->
    <DataTemplate DataType="{x:Type local:MainWorkflowDesignerViewModel}">
        <ScrollViewer CanContentScroll="True"
                      HorizontalScrollBarVisibility="Auto"
                      VerticalScrollBarVisibility="Auto">
            <nodify:NodifyEditor ItemsSource="{Binding Steps}"
                                 SelectedItem="{Binding SelectedStep.Value, Mode=TwoWay}"
                                 Connections="{Binding Connections}"
                                 InputGestures="{Binding EditorGestures}"
                                 ViewportLocation="{Binding ViewportPosition.Value, Mode=TwoWay}"
                                 ViewportSize="{Binding Value, Source={x:Static local:MainWorkflowDesignerViewModel.ViewportSize}, Mode=OneWayToSource}"
                                 ItemTemplate="{StaticResource MainWorkflowStepTemplate}"
                                 Background="Transparent">
                <nodify:NodifyEditor.ItemContainerStyle>
                    <Style BasedOn="{StaticResource ItemContainerStyle}"
                           TargetType="{x:Type nodify:ItemContainer}">
                        <Setter Property="Location" Value="{Binding Position.Value, Mode=TwoWay}" />
                        <Setter Property="ActualSize" Value="{Binding Size.Value, Mode=OneWayToSource}" />
                    </Style>
                </nodify:NodifyEditor.ItemContainerStyle>

                <nodify:NodifyEditor.ConnectionTemplate>
                    <DataTemplate DataType="{x:Type local:WorkflowStepConnectionViewModel}">
                        <!-- LineConnection with Vertical orientation = top-to-bottom flow -->
                        <nodify:LineConnection Source="{Binding From.OutAnchorPosition.Value}"
                                               Target="{Binding To.InAnchorPosition.Value}"
                                               SourceOrientation="Vertical"
                                               TargetOrientation="Vertical"
                                               Style="{StaticResource ConnectionStyle}" />
                    </DataTemplate>
                </nodify:NodifyEditor.ConnectionTemplate>
            </nodify:NodifyEditor>
        </ScrollViewer>
    </DataTemplate>

</ResourceDictionary>
```

### 6.2 CommonDataTemplates.xaml

Renders `CommandViewModel` as a borderless `Button` and `ToggleCommandViewModel` as a borderless `ToggleButton`. With these templates, you can drop `<ContentControl Content="{Binding SomeCommand}" />` anywhere.

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:local="clr-namespace:YourApp.Common"
                    xmlns:ic="clr-namespace:FluentIcons.Wpf;assembly=FluentIcons.Wpf"
                    xmlns:shared="clr-namespace:Nodify;assembly=Nodify.Shared">

    <DataTemplate DataType="{x:Type local:CommandViewModel}">
        <Button Style="{StaticResource ButtonStyle.Borderless}"
                ToolTip="{Binding ToolTip.Value}"
                Command="{Binding Command}">
            <StackPanel Orientation="Horizontal">
                <ic:FluentIcon IsHitTestVisible="False"
                               Icon="{Binding Icon.Value}"
                               IconSize="Size16" FontSize="14"
                               IconVariant="{Binding IconVariant.Value}"
                               VerticalAlignment="Center"
                               Visibility="{Binding Icon.Value, Converter={shared:NullToVisibilityConverter}}">
                    <ic:FluentIcon.Style>
                        <Style TargetType="ic:FluentIcon">
                            <Setter Property="Foreground"
                                    Value="{Binding IconColor.Value, Converter={shared:ColorToSolidColorBrushConverter}}" />
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IconColor.Value}" Value="{x:Null}">
                                    <Setter Property="Foreground"
                                            Value="{DynamicResource TextFillColorSecondaryBrush}" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </ic:FluentIcon.Style>
                </ic:FluentIcon>
                <TextBlock Text="{Binding Label.Value}" FontSize="14"
                           Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                           Visibility="{Binding Label.Value, Converter={shared:NullToVisibilityConverter}}">
                    <TextBlock.Style>
                        <Style TargetType="TextBlock" BasedOn="{StaticResource BodyTextBlockStyle}">
                            <Setter Property="Margin" Value="8 0 0 0" />
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding Icon.Value}" Value="{x:Null}">
                                    <Setter Property="Margin" Value="0" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </TextBlock.Style>
                </TextBlock>
            </StackPanel>
        </Button>
    </DataTemplate>

    <DataTemplate DataType="{x:Type local:ToggleCommandViewModel}">
        <ToggleButton Style="{StaticResource ToggleButtonStyle.Borderless}"
                      ToolTip="{Binding ToolTip.Value}"
                      Command="{Binding Command}"
                      IsChecked="{Binding IsChecked.Value, Mode=TwoWay}">
            <StackPanel Orientation="Horizontal">
                <ic:FluentIcon IsHitTestVisible="False"
                               IconSize="Size16" FontSize="14"
                               IconVariant="{Binding IconVariant.Value}"
                               VerticalAlignment="Center"
                               Visibility="{Binding Icon.Value, Converter={shared:NullToVisibilityConverter}}">
                    <ic:FluentIcon.Style>
                        <Style TargetType="ic:FluentIcon">
                            <Setter Property="Foreground"
                                    Value="{Binding IconColor.Value, Converter={shared:ColorToSolidColorBrushConverter}}" />
                            <Setter Property="Icon" Value="{Binding Icon.Value}" />
                            <Style.Triggers>
                                <DataTrigger Binding="{Binding IconColor.Value}" Value="{x:Null}">
                                    <Setter Property="Foreground"
                                            Value="{DynamicResource TextFillColorSecondaryBrush}" />
                                </DataTrigger>
                                <DataTrigger Binding="{Binding IsChecked.Value}" Value="True">
                                    <Setter Property="Icon" Value="{Binding IconChecked.Value}" />
                                    <Setter Property="Foreground"
                                            Value="{DynamicResource TextOnAccentFillColorPrimaryBrush}" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </ic:FluentIcon.Style>
                </ic:FluentIcon>
                <TextBlock Text="{Binding Label.Value}" FontSize="14"
                           Visibility="{Binding Label.Value, Converter={shared:NullToVisibilityConverter}}">
                    <TextBlock.Style>
                        <Style TargetType="TextBlock" BasedOn="{StaticResource BodyTextBlockStyle}">
                            <Setter Property="Margin" Value="8 0 0 0" />
                            <Setter Property="Foreground"
                                    Value="{DynamicResource TextFillColorSecondaryBrush}" />
                            <Style.Triggers>
                                <MultiDataTrigger>
                                    <MultiDataTrigger.Conditions>
                                        <Condition Binding="{Binding Icon.Value}" Value="{x:Null}" />
                                        <Condition Binding="{Binding IconChecked.Value}" Value="{x:Null}" />
                                    </MultiDataTrigger.Conditions>
                                    <MultiDataTrigger.Setters>
                                        <Setter Property="Margin" Value="0" />
                                    </MultiDataTrigger.Setters>
                                </MultiDataTrigger>
                                <DataTrigger Binding="{Binding IsChecked.Value}" Value="True">
                                    <Setter Property="Foreground"
                                            Value="{DynamicResource TextOnAccentFillColorPrimaryBrush}" />
                                </DataTrigger>
                            </Style.Triggers>
                        </Style>
                    </TextBlock.Style>
                </TextBlock>
            </StackPanel>
        </ToggleButton>
    </DataTemplate>
</ResourceDictionary>
```

### 6.3 WorkflowToolBar.xaml

A floating toolbar overlaid at the bottom-center of the sub-workflow editor. The `Editor` dependency property is set from the `DataTemplate` via `ElementName`.

The toolbar code-behind declares a `NodifyEditor Editor` dependency property:

```csharp
// WorkflowToolBar.xaml.cs
public partial class WorkflowToolBar : UserControl
{
    public static readonly DependencyProperty EditorProperty =
        DependencyProperty.Register(nameof(Editor), typeof(NodifyEditor), typeof(WorkflowToolBar));

    public NodifyEditor Editor
    {
        get => (NodifyEditor)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    public WorkflowToolBar() => InitializeComponent();
}
```

```xml
<UserControl x:Class="YourApp.Designer.WorkflowToolBar"
             ... x:Name="Root">
    <Border Padding="8" CornerRadius="{DynamicResource OverlayCornerRadius}"
            Background="{DynamicResource LayerOnAcrylicFillColorDefaultBrush}"
            BorderBrush="{DynamicResource AccentControlElevationBorderBrush}"
            BorderThickness="1">
        <StackPanel Orientation="Horizontal">
            <!-- Zoom in -->
            <Button Command="{x:Static nodify:EditorCommands.ZoomIn}"
                    CommandTarget="{Binding Editor, ElementName=Root}"
                    ToolTip="Zoom in" Style="{StaticResource ButtonStyle.Borderless}">
                <ic:FluentIcon IsHitTestVisible="False" Icon="ZoomIn" IconSize="Size16" FontSize="14" />
            </Button>

            <!-- Zoom percentage -->
            <TextBlock Text="{Binding Editor.ViewportZoom, ElementName=Root, StringFormat={}{0:P0}}"
                       Margin="4 0 4 4" VerticalAlignment="Center" />

            <!-- Zoom out -->
            <Button Command="{x:Static nodify:EditorCommands.ZoomOut}"
                    CommandTarget="{Binding Editor, ElementName=Root}"
                    ToolTip="Zoom out" Style="{StaticResource ButtonStyle.Borderless}">
                <ic:FluentIcon IsHitTestVisible="False" Icon="ZoomOut" IconSize="Size16" FontSize="14" />
            </Button>

            <Border Style="{StaticResource SeparatorStyle.Default}" />

            <!-- Fit to screen -->
            <Button Command="{x:Static nodify:EditorCommands.FitToScreen}"
                    CommandTarget="{Binding Editor, ElementName=Root}"
                    ToolTip="Fit to view" Style="{StaticResource ButtonStyle.Borderless}">
                <ic:FluentIcon IsHitTestVisible="False" Icon="FullScreenMaximize" IconSize="Size16" FontSize="14" />
            </Button>

            <!-- Lock/unlock toggle — DataContext here is SubWorkflowDesignerViewModel -->
            <ContentControl Content="{Binding LockViewCommand}" Focusable="False" />

            <Border Style="{StaticResource SeparatorStyle.Default}" />

            <Button Style="{StaticResource ButtonStyle.Borderless}" ToolTip="More actions">
                <ic:FluentIcon IsHitTestVisible="False" Icon="MoreHorizontal" IconSize="Size16" FontSize="14" />
            </Button>
        </StackPanel>
    </Border>
</UserControl>
```

### 6.4 MainWindow.xaml (simplified)

```xml
<Window x:Class="YourApp.MainWindow"
        xmlns:shell="clr-namespace:YourApp.Shell"
        xmlns:shared="clr-namespace:Nodify;assembly=Nodify.Shared"
        Title="{Binding Title}" Height="720" Width="1280">

    <Window.DataContext>
        <shell:ApplicationViewModel />
    </Window.DataContext>

    <Window.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="/Designer/WorkflowDataTemplates.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!-- Title bar -->
        <shell:TitleBar Grid.Row="0" />

        <Grid Margin="12" Grid.Row="1">
            <Grid.ColumnDefinitions>
                <!-- Workflows list sidebar -->
                <ColumnDefinition x:Name="WorkflowsPanelColumn" Width="220" />
                <!-- Selected sub-workflow editor -->
                <ColumnDefinition x:Name="SelectedWorkflowColumn" Width="0" />
                <ColumnDefinition Width="Auto" />
                <!-- Main workflow overview -->
                <ColumnDefinition x:Name="MainWorkflowColumn" Width="*" />
            </Grid.ColumnDefinitions>

            <shell:WorkflowsPanel Grid.Column="0"
                IsEnabled="{Binding IsZenMode.Value, Converter={shared:InverseBooleanConverter}}"
                Margin="0 0 8 0" />

            <Border Grid.Column="1"
                    Visibility="{Binding SelectedWorkflow.Value, Converter={shared:NullToVisibilityConverter}}"
                    CornerRadius="{StaticResource ControlCornerRadius}"
                    Background="{DynamicResource CardBackgroundFillColorDefaultBrush}">
                <ContentControl Content="{Binding SelectedWorkflow.Value}" />
            </Border>

            <GridSplitter x:Name="ColumnSplitter" Grid.Column="2" Visibility="Collapsed" />

            <Border Grid.Column="3"
                    CornerRadius="{StaticResource ControlCornerRadius}"
                    Background="{DynamicResource CardBackgroundFillColorDefaultBrush}">
                <ContentControl Content="{Binding MainWorkflow.Value}" />
            </Border>
        </Grid>
    </Grid>
</Window>
```

### 6.5 WorkflowsPanel.xaml (sidebar list)

```xml
<UserControl ...>
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <ListView ItemsSource="{Binding Workflows}"
                  SelectedValue="{Binding SelectedWorkflow.Value, Mode=TwoWay}"
                  SelectionMode="Single"
                  ScrollViewer.HorizontalScrollBarVisibility="Hidden">
            <ListView.ItemTemplate>
                <DataTemplate DataType="{x:Type wf:SubWorkflowDesignerViewModel}">
                    <TextBlock Margin="8 4" Text="{Binding Name.Value}" />
                </DataTemplate>
            </ListView.ItemTemplate>
        </ListView>

        <Button Grid.Row="1" HorizontalAlignment="Stretch">
            <StackPanel Orientation="Horizontal">
                <ic:FluentIcon Icon="Add" />
                <TextBlock Text="New Workflow" Margin="4 0 0 0" />
            </StackPanel>
        </Button>
    </Grid>
</UserControl>
```

---

## 7. AnimationExtensions.cs (optional but used by MainWindow)

Used to animate `GridLength` column widths for the zen-mode transition and panel show/hide. Copy this file wholesale.

```csharp
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace YourApp.Common;

public static class AnimationDefaults
{
    public static readonly IEasingFunction DefaultEase = new CubicEase { EasingMode = EasingMode.EaseOut };
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromMilliseconds(200);
}

public sealed class AnimationOptions<T> where T : struct
{
    public T? From { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(200);
    public IEasingFunction? Easing { get; set; } = AnimationDefaults.DefaultEase;
}

public static class AnimationExtensions
{
    // --- Core (tracks and cancels running animations on same target+property) ---

    private readonly struct AnimationKey(DependencyObject target, DependencyProperty property) : IEquatable<AnimationKey>
    {
        public readonly DependencyObject Target = target;
        public readonly DependencyProperty Property = property;
        public bool Equals(AnimationKey other) => ReferenceEquals(Target, other.Target) && Property == other.Property;
        public override bool Equals(object? obj) => obj is AnimationKey key && Equals(key);
        public override int GetHashCode() => HashCode.Combine(Target, Property);
    }

    private sealed class RunningAnimation(DependencyObject target, DependencyProperty property)
    {
        private static int _nextId;
        private readonly TaskCompletionSource _tcs = new();
        public int Id { get; } = Interlocked.Increment(ref _nextId);
        public void Cancel()
        {
            var key = new AnimationKey(target, property);
            if (_runningAnimations.TryGetValue(key, out var current) && current.Id == Id)
            {
                _runningAnimations.TryRemove(key, out _);
                BeginAnimationOn(target, property, null);
                End();
            }
        }
        public void End() => _tcs.TrySetResult();
        public Task CompletionTask => _tcs.Task;
    }

    private sealed class AnimationCompletionHandler(AnimationKey key, RunningAnimation animation)
    {
        public void OnCompleted(object? sender, EventArgs e)
        {
            if (!_runningAnimations.TryGetValue(key, out var running) || running.Id != animation.Id) return;
            _runningAnimations.TryRemove(key, out _);
            running.End();
        }
    }

    private static readonly ConcurrentDictionary<AnimationKey, RunningAnimation> _runningAnimations = new();

    public static Task Animate(this DependencyObject target, DependencyProperty property, AnimationTimeline timeline)
    {
        var key = new AnimationKey(target, property);
        var newAnimation = new RunningAnimation(target, property);
        if (_runningAnimations.TryRemove(key, out var previous)) previous.Cancel();
        _runningAnimations[key] = newAnimation;
        var handler = new AnimationCompletionHandler(key, newAnimation);
        timeline.Completed += handler.OnCompleted;
        BeginAnimationOn(target, property, timeline);
        return newAnimation.CompletionTask;
    }

    private static void BeginAnimationOn(DependencyObject target, DependencyProperty property, AnimationTimeline? animation)
    {
        switch (target)
        {
            case UIElement ui: ui.BeginAnimation(property, animation); break;
            case Animatable anim: anim.BeginAnimation(property, animation); break;
            case ContentElement content: content.BeginAnimation(property, animation); break;
            default: throw new InvalidOperationException($"{target.GetType()} does not support animation.");
        }
    }

    // --- Typed overloads ---

    public static Task Animate(this DependencyObject target, DependencyProperty property, double to, in AnimationOptions<double>? options = null)
        => target.Animate(property, new DoubleAnimation
        {
            From = options?.From ?? (double)target.GetValue(property),
            To = to,
            Duration = options?.Duration ?? AnimationDefaults.DefaultDuration,
            EasingFunction = options?.Easing ?? AnimationDefaults.DefaultEase
        });

    public static Task Animate(this DependencyObject target, DependencyProperty property, GridLength to, AnimationOptions<GridLength>? options = null)
    {
        var from = (GridLength)target.GetValue(property);
        var wrapperOptions = options != null
            ? new AnimationOptions<double> { Duration = options.Duration, From = options.From?.Value, Easing = options.Easing }
            : null;
        var wrapper = new AnimatableDouble(from.Value, v => target.SetCurrentValue(property, new GridLength(v)));
        return wrapper.Animate(AnimatableDouble.ValueProperty, to.Value, wrapperOptions);
    }

    private class AnimatableDouble : Animatable
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(AnimatableDouble),
                new PropertyMetadata(0.0d, (d, e) => ((AnimatableDouble)d)._update((double)e.NewValue)));

        private readonly Action<double> _update;

        public AnimatableDouble(double from, Action<double> update)
        {
            _update = update;
            SetCurrentValue(ValueProperty, from);
        }
        private AnimatableDouble() { _update = _ => { }; }
        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        protected override Freezable CreateInstanceCore() => new AnimatableDouble();
    }

    // --- High-level helpers ---

    public static Task FadeIn(this UIElement el, AnimationOptions<double>? options = null)
        => el.Animate(UIElement.OpacityProperty, 1d, options);

    public static Task FadeOut(this UIElement el, AnimationOptions<double>? options = null)
        => el.Animate(UIElement.OpacityProperty, 0d, options);
}
```

**Usage in MainWindow.xaml.cs:**
```csharp
private static void AnimateColumnWidth(ColumnDefinition column, double to)
{
    column.Animate(ColumnDefinition.WidthProperty, new GridLength(to), new AnimationOptions<GridLength>
    {
        Duration = TimeSpan.FromMilliseconds(250),
        Easing = new CubicEase { EasingMode = to == 0 ? EasingMode.EaseIn : EasingMode.EaseOut }
    });
}
```

---

## 8. MainWindow.xaml.cs — column animation + zen mode

```csharp
public partial class MainWindow
{
    private const double _defaultMainWorkflowWidth = 350;
    private const double _defaultWorkflowsPanelWidth = 220;

    private double _lastMainWorkflowWidth = _defaultMainWorkflowWidth;
    private bool _isZenMode;
    private bool _hasSelectedWorkflow;

    static MainWindow()
    {
        NodifyEditor.AutoRegisterConnectionsLayer = false;
    }

    public MainWindow()
    {
        InitializeComponent();
        StateChanged += UpdateMainWindowVisuals;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ApplicationViewModel viewModel)
        {
            viewModel.IsZenMode.Subscribe(OnZenModeChanged);
            viewModel.SelectedWorkflow.Subscribe(OnSelectedWorkflowChanged);
        }
    }

    private void OnZenModeChanged(bool isZenMode)
    {
        _isZenMode = isZenMode;
        UpdateSplitterVisibility();
        if (isZenMode)
        {
            _lastMainWorkflowWidth = MainWorkflowColumn.Width.Value;
            if (_hasSelectedWorkflow) AnimateColumnWidth(MainWorkflowColumn, 0);
            AnimateColumnWidth(WorkflowsPanelColumn, 0);
        }
        else
        {
            if (_hasSelectedWorkflow) AnimateColumnWidth(MainWorkflowColumn, _lastMainWorkflowWidth);
            AnimateColumnWidth(WorkflowsPanelColumn, _defaultWorkflowsPanelWidth);
        }
    }

    private void OnSelectedWorkflowChanged(object? selectedWorkflow)
    {
        _hasSelectedWorkflow = selectedWorkflow is not null;
        UpdateSplitterVisibility();
        if (_hasSelectedWorkflow)
        {
            SelectedWorkflowColumn.Width = new GridLength(1, GridUnitType.Star);
            MainWorkflowColumn.Width = new GridLength(_defaultMainWorkflowWidth);
        }
        else
        {
            if (MainWorkflowColumn.Width.IsAbsolute)
                _lastMainWorkflowWidth = MainWorkflowColumn.Width.Value;
            SelectedWorkflowColumn.Width = new GridLength(0);
            MainWorkflowColumn.Width = new GridLength(1, GridUnitType.Star);
        }
    }

    private void UpdateSplitterVisibility()
    {
        ColumnSplitter.Visibility = _hasSelectedWorkflow && !_isZenMode
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void AnimateColumnWidth(ColumnDefinition column, double to)
    {
        column.Animate(ColumnDefinition.WidthProperty, new GridLength(to), new AnimationOptions<GridLength>
        {
            Duration = TimeSpan.FromMilliseconds(250),
            Easing = new CubicEase { EasingMode = to == 0 ? EasingMode.EaseIn : EasingMode.EaseOut }
        });
    }

    private void UpdateMainWindowVisuals(object? sender, EventArgs args)
    {
        BorderThickness = WindowState is WindowState.Maximized ? new Thickness(8) : new Thickness(0);
    }
}
```

---

## 9. Key Concepts & Gotchas

### Anchor position flow

The `Connector.Anchor` property in Nodify reports the screen-space position of the connector's centre. Binding it `Mode=OneWayToSource` writes this back into `WorkflowStepViewModel.InAnchorPosition.Value` / `OutAnchorPosition.Value`. The connection `DataTemplate` then reads `From.OutAnchorPosition.Value` and `To.InAnchorPosition.Value` as the source and target points of the line. This is how connections automatically follow when you drag nodes.

### ViewportSize static property

`ViewportSize` is declared **static** on each concrete designer VM (not the base class) because `ClampedViewportProperty<T>` accesses it via the generic type parameter as `T.ViewportSize.Value`. The XAML binds to it using `Source={x:Static local:SubWorkflowDesignerViewModel.ViewportSize}`.

### OnPostInitialize must be called after adding steps

`ClampedViewportProperty.ObserveStepChanges()` subscribes to the `Steps` list. Call it **after** populating the initial steps, not before, so existing steps get subscriptions.

### `NodifyEditor.AutoRegisterConnectionsLayer = false`

Must be set in the **static constructor** of MainWindow before `InitializeComponent()` runs. If omitted, the default connections layer registration interferes with the template-defined connection layer.

### Connection types

| Type | Shape | Use case |
|---|---|---|
| `StepConnection` | S-curve (horizontal) | Left→right flow in sub-workflows |
| `LineConnection` | Bezier/straight | Top→bottom flow; set `SourceOrientation="Vertical"` |

### Gesture modifier keys

| Action | Modifier |
|---|---|
| Zoom | `Ctrl` + mouse wheel |
| Pan vertically | `Shift` + drag (sub-workflow) |
| Pan horizontally | No modifier (sub-workflow) |
| Pan (read-only) | Left, Right, or Middle button drag |

---

## 10. Implementation Order

1. Add NuGet packages to `.csproj`
2. Create `WorkflowStepViewModel`, `WorkflowStepConnectionViewModel`
3. Create `IViewportSizeAware` interface and `ClampedViewportProperty<T>`
4. Create `DesignerGesturesExtensions` (LockEditing)
5. Create `WorkflowDesignerViewModel<T>`, `MainWorkflowDesignerViewModel`, `SubWorkflowDesignerViewModel`
6. Create `CommandViewModel` and `ToggleCommandViewModel`
7. Create `ApplicationViewModel` with sample data, call `OnPostInitialize()` on each workflow
8. Add `static MainWindow()` with `NodifyEditor.AutoRegisterConnectionsLayer = false`
9. Write `WorkflowDataTemplates.xaml` — item containers, connectors, editor templates
10. Write `CommonDataTemplates.xaml` — command/toggle-command templates
11. Merge resource dictionaries in `App.xaml` and `MainWindow.xaml`
12. Wire up `MainWindow.xaml.cs` subscriptions for zen mode and selected workflow
13. (Optional) Add `AnimationExtensions.cs` and `WorkflowToolBar`
