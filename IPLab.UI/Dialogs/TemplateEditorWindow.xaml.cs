using IPLab.UI.Services;
using Microsoft.Win32;
using OpenCvSharp;
using RControls;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace IPLab.UI.Dialogs;

public partial class TemplateEditorWindow : System.Windows.Window
{
    public static void TryOpen(OperatorEditorContext context)
    {
        if (context.ResolveParameter("Image") is not Mat image || image.Empty())
        {
            MessageBox.Show(context.Owner,
                "Run the flow first so the TemplateMatch image input is available.",
                "Template Editor", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (image.Type() != MatType.CV_8UC1 && image.Type() != MatType.CV_8UC3)
        {
            MessageBox.Show(context.Owner,
                $"The template editor requires an 8-bit grayscale or 8-bit, 3-channel image. " +
                $"The resolved input is {image.Type()}.",
                "Template Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var pathParameter = context.Node.Parameters.First(p => p.Name == "TemplatePath");
        var dialog = new TemplateEditorWindow(image, pathParameter.ValueText)
        {
            Owner = context.Owner,
        };
        if (dialog.ShowDialog() == true && dialog.SavedPath is { } savedPath)
            pathParameter.ValueText = savedPath;
    }


    private readonly Mat _sourceImage;
    private Mat? _templateImage;
    private Mat? _baseAlpha;
    private string? _path;

    public string? SavedPath { get; private set; }

    public TemplateEditorWindow(Mat sourceImage, string? templatePath)
    {
        InitializeComponent();
        _sourceImage = sourceImage.Clone();
        SourceViewer.SourceImage = ToBitmapSource(_sourceImage, ".bmp");
        Closed += (_, _) => DisposeImages();

        if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
            LoadExistingTemplate(templatePath);
        UpdatePathText();
    }

    private void OnSelectTemplate(object sender, RoutedEventArgs e)
    {
        SourceViewer.RemoveRegion(string.Empty, ShapeMode.Rectangle);
        SourceViewer.OperationMode = OpMode.Draw;
        SourceViewer.DrawShape = ShapeMode.Rectangle;
    }

    private void OnApplySelection(object sender, RoutedEventArgs e)
    {
        var item = SourceViewer.GetRegions(string.Empty, ShapeMode.Rectangle).LastOrDefault();
        if (item is null)
        {
            ShowWarning("Draw a template rectangle on the source image first.");
            return;
        }

        var rect = GetClampedRect(item, _sourceImage.Width, _sourceImage.Height);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            ShowWarning("The selected template rectangle is empty.");
            return;
        }

        _templateImage?.Dispose();
        _baseAlpha?.Dispose();
        using var crop = new Mat(_sourceImage, rect);
        _templateImage = crop.Clone();
        _baseAlpha = new Mat(rect.Height, rect.Width, MatType.CV_8UC1, Scalar.White);
        TemplateViewer.RemoveRegion(string.Empty, ShapeMode.None);
        RefreshTemplatePreview();
    }

    private void OnAddMask(object sender, RoutedEventArgs e)
    {
        if (_templateImage is null)
        {
            ShowWarning("Apply a template selection first.");
            return;
        }
        TemplateViewer.OperationMode = OpMode.Draw;
        TemplateViewer.DrawShape = ShapeMode.Rectangle;
    }

    private void OnDeleteSelected(object sender, RoutedEventArgs e)
        => ApplicationCommands.Delete.Execute(null, TemplateViewer);

    private void OnClearMasks(object sender, RoutedEventArgs e)
    {
        if (_templateImage is null) return;
        TemplateViewer.RemoveRegion(string.Empty, ShapeMode.Rectangle);
        _baseAlpha?.SetTo(Scalar.White);
        RefreshTemplatePreview();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_path))
            SaveAs();
        else
            SaveTo(_path);
    }

    private void OnSaveAs(object sender, RoutedEventArgs e) => SaveAs();

    private void SaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save Template",
            Filter = "PNG image (*.png)|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = string.IsNullOrWhiteSpace(_path) ? "template.png" : Path.GetFileName(_path),
            InitialDirectory = string.IsNullOrWhiteSpace(_path) ? null : Path.GetDirectoryName(_path),
        };
        if (dialog.ShowDialog(this) == true)
            SaveTo(dialog.FileName);
    }

    private void SaveTo(string path)
    {
        if (_templateImage is null || _baseAlpha is null)
        {
            ShowWarning("Select and apply a template before saving.");
            return;
        }

        using var alpha = BuildAlphaMask();
        if (Cv2.CountNonZero(alpha) == 0)
        {
            ShowWarning("The mask excludes every template pixel.");
            return;
        }

        var channels = CreatePngColorChannels(_templateImage);
        channels.Add(alpha.Clone());
        try
        {
            using var rgba = new Mat();
            Cv2.Merge(channels.ToArray(), rgba);
            if (!Cv2.ImWrite(path, rgba))
                throw new IOException($"OpenCV could not write '{path}'.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Save Template", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        finally
        {
            foreach (var channel in channels) channel.Dispose();
        }

        _path = Path.GetFullPath(path);
        SavedPath = _path;
        UpdatePathText();
        DialogResult = true;
    }

    private Mat BuildAlphaMask()
    {
        var alpha = _baseAlpha!.Clone();
        foreach (var item in TemplateViewer.GetRegions(string.Empty, ShapeMode.Rectangle))
        {
            var rect = GetClampedRect(item, alpha.Width, alpha.Height);
            if (rect.Width <= 0 || rect.Height <= 0) continue;
            using var excluded = new Mat(alpha, rect);
            excluded.SetTo(Scalar.Black);
        }
        return alpha;
    }

    private void LoadExistingTemplate(string path)
    {
        using var png = Cv2.ImRead(path, ImreadModes.Unchanged);
        if (png.Empty() || png.Type() != MatType.CV_8UC4)
            return;

        var channels = Cv2.Split(png);
        try
        {
            if (_sourceImage.Channels() == 1)
            {
                if (!AreColorChannelsEqual(channels))
                {
                    ShowWarning("The existing template contains color data and cannot be used with the grayscale input image.");
                    return;
                }
                _templateImage = channels[0].Clone();
            }
            else
            {
                _templateImage = MergeColorChannels(channels);
            }
            _baseAlpha = channels[3].Clone();
            _path = Path.GetFullPath(path);
            RefreshTemplatePreview();
        }
        finally
        {
            foreach (var channel in channels) channel.Dispose();
        }
    }

    private void RefreshTemplatePreview()
    {
        if (_templateImage is null || _baseAlpha is null) return;
        var channels = CreatePngColorChannels(_templateImage);
        channels.Add(_baseAlpha.Clone());
        try
        {
            using var rgba = new Mat();
            Cv2.Merge(channels.ToArray(), rgba);
            TemplateViewer.SourceImage = ToBitmapSource(rgba, ".png");
        }
        finally
        {
            foreach (var channel in channels) channel.Dispose();
        }
    }

    private static OpenCvSharp.Rect GetClampedRect(ImageItem item, int width, int height)
    {
        int left = Math.Clamp((int)Math.Round(Canvas.GetLeft(item)), 0, width);
        int top = Math.Clamp((int)Math.Round(Canvas.GetTop(item)), 0, height);
        int right = Math.Clamp((int)Math.Round(Canvas.GetLeft(item) + item.ActualWidth), 0, width);
        int bottom = Math.Clamp((int)Math.Round(Canvas.GetTop(item) + item.ActualHeight), 0, height);
        return new OpenCvSharp.Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private static List<Mat> CreatePngColorChannels(Mat image)
    {
        if (image.Channels() == 3)
            return Cv2.Split(image).ToList();

        if (image.Channels() == 1)
            return [image.Clone(), image.Clone(), image.Clone()];

        throw new ArgumentException($"Unsupported template image type: {image.Type()}.");
    }

    private static Mat MergeColorChannels(IReadOnlyList<Mat> channels)
    {
        var image = new Mat();
        Cv2.Merge(channels.Take(3).ToArray(), image);
        return image;
    }

    private static bool AreColorChannelsEqual(IReadOnlyList<Mat> channels)
    {
        using var bgDiff = new Mat();
        using var brDiff = new Mat();
        Cv2.Absdiff(channels[0], channels[1], bgDiff);
        Cv2.Absdiff(channels[0], channels[2], brDiff);
        return Cv2.CountNonZero(bgDiff) == 0 && Cv2.CountNonZero(brDiff) == 0;
    }

    private static BitmapSource ToBitmapSource(Mat image, string extension)
    {
        var bytes = image.ToBytes(extension);
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void UpdatePathText() => PathText.Text = _path ?? "Template has not been saved.";
    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, "Template Editor", MessageBoxButton.OK, MessageBoxImage.Warning);
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void DisposeImages()
    {
        _sourceImage.Dispose();
        _templateImage?.Dispose();
        _baseAlpha?.Dispose();
    }
}
