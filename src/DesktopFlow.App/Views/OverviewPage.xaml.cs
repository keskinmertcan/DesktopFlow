using DesktopFlow.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DesktopFlow.App.Views;

public sealed partial class OverviewPage : Page
{
    private CancellationTokenSource? _load;

    public OverviewPage(OverviewViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public OverviewViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _load = new CancellationTokenSource();
        await ViewModel.LoadAsync(_load.Token);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _load?.Cancel();
    }
}
