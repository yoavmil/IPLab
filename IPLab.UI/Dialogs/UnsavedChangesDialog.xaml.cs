using System.Windows;

namespace IPLab.UI.Dialogs;

public enum UnsavedChangesResult { Save, SaveAs, Discard, Cancel }

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesResult Result { get; private set; } = UnsavedChangesResult.Cancel;

    public UnsavedChangesDialog(bool hasSavedPath)
    {
        InitializeComponent();
        // "Save" is only meaningful when a file path is already known.
        SaveButton.Visibility = hasSavedPath ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Save_Click(object sender, RoutedEventArgs e)    { Result = UnsavedChangesResult.Save;    Close(); }
    private void SaveAs_Click(object sender, RoutedEventArgs e)  { Result = UnsavedChangesResult.SaveAs;  Close(); }
    private void Discard_Click(object sender, RoutedEventArgs e) { Result = UnsavedChangesResult.Discard; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e)  { Result = UnsavedChangesResult.Cancel;  Close(); }
}
