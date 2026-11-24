using DesktopFlow.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DesktopFlow.App.Views;

public sealed partial class FolderPage : Page
{
    private CancellationTokenSource? _load;
    private bool _ready;

    public FolderPage(FolderViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _load?.Cancel();
    }

    public FolderViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _load = new CancellationTokenSource();
        await ViewModel.LoadAsync(_load.Token);
        _ready = true;
    }

    private async void MonitorSwitch_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        await ViewModel.ToggleMonitoringCommand.ExecuteAsync(null);
    }

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SetSelection(FileList.SelectedItems.OfType<FileRowViewModel>());
    }
}
