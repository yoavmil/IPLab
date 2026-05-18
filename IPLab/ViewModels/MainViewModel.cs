using IPLab.Core.Models;
using IPLab.Core.Operators;
using IPLab.Core.Runtime;
using IPLab.Core.Utilities;
using Microsoft.Win32;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IPLab.ViewModels;

public class MainViewModel : ViewModelBase
{
    public FlowViewModel Flow { get; }

    private string _status = "Ready";
    public string Status
    {
        get => _status;
        private set { _status = value; RaisePropertyChanged(); }
    }

    private OperatorNodeViewModel? _selectedNode;
    public OperatorNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set { _selectedNode = value; RaisePropertyChanged(); UpdateSelectedImage(); }
    }

    private BitmapSource? _selectedImage;
    public BitmapSource? SelectedImage
    {
        get => _selectedImage;
        private set { _selectedImage = value; RaisePropertyChanged(); }
    }

    private FlowEx? _executor;
    private string _imagePath = string.Empty;

    public ICommand RunAllCommand { get; }
    public ICommand ClearResultsCommand { get; }

    public MainViewModel()
    {
        Flow = new FlowViewModel(BuildSampleFlow());
        RunAllCommand = new RelayCommand(RunAll);
        ClearResultsCommand = new RelayCommand(ClearResults);
    }

    private async void RunAll()
    {
        if (string.IsNullOrEmpty(_imagePath))
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Select an image to process",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|All files|*.*"
            };
            if (dialog.ShowDialog() != true) return;
            _imagePath = dialog.FileName;
        }

        Status = "Running…";
        try
        {
            var flow = BuildSampleFlow(_imagePath);
            _executor = new FlowEx(flow);
            await _executor.RunAllAsync();
            Status = $"Done  |  {Path.GetFileName(_imagePath)}";
            UpdateSelectedImage();
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
    }

    private void ClearResults()
    {
        _executor     = null;
        _imagePath    = string.Empty;
        SelectedImage = null;
        Status        = "Ready";
    }

    private void UpdateSelectedImage()
    {
        if (_selectedNode is null || _executor is null)
        {
            SelectedImage = null;
            return;
        }

        _executor.IntermediateResults.TryGetValue(_selectedNode.Id, out var result);
        var bytes = ImageHelper.TryGetPngBytes(result);
        SelectedImage = bytes is not null ? BytesToBitmapSource(bytes) : null;
    }

    private static BitmapSource BytesToBitmapSource(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.CacheOption  = BitmapCacheOption.OnLoad;
        bi.StreamSource = ms;
        bi.EndInit();
        bi.Freeze();
        return bi;
    }

    private static FlowDef BuildSampleFlow(string imagePath = "") => new(
    [
        new Operator
        {
            Id           = "O1",
            DisplayName  = "Load Image",
            Type         = new LoadImageOperator(),
            Parameters   = [new ParameterValue { Name = "FilePath", Value = imagePath }],
            Dependencies = []
        },
        new Operator
        {
            Id           = "O2",
            DisplayName  = "To Grayscale",
            Type         = new ConvertToGrayscaleOperator(),
            Parameters   =
            [
                new ParameterValue { Name = "Image",  Source = new SourceRef("O1", "Image") },
                new ParameterValue { Name = "Method", Value  = "HsvValue" }
            ],
            Dependencies = [new Dependency("D1", "O1")]
        },
        new Operator
        {
            Id           = "O3",
            DisplayName  = "Threshold",
            Type         = new ThresholdOperator(),
            Parameters   =
            [
                new ParameterValue { Name = "Image",  Source = new SourceRef("O2", "Image") },
                new ParameterValue { Name = "Thresh", Value  = 128.0 },
                new ParameterValue { Name = "MaxVal", Value  = 255.0 }
            ],
            Dependencies = [new Dependency("D2", "O2")]
        },
        new Operator
        {
            Id           = "O4",
            DisplayName  = "Detect Circles",
            Type         = new DetectCirclesOperator(),
            Parameters   =
            [
                new ParameterValue { Name = "Image",     Source = new SourceRef("O3", "Image") },
                new ParameterValue { Name = "MinDist",   Value  = 50.0  },
                new ParameterValue { Name = "Param1",    Value  = 150.0 },
                new ParameterValue { Name = "Param2",    Value  = 10.0  },
                new ParameterValue { Name = "MinRadius", Value  = 10    },
                new ParameterValue { Name = "MaxRadius", Value  = 100   }
            ],
            Dependencies = [new Dependency("D3", "O3")]
        }
    ]);
}
