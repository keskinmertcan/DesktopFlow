using DesktopFlow.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DesktopFlow.App.Views;

public sealed partial class SetupPage : Page
{
    public SetupPage(SetupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadAsync(CancellationToken.None);
    }

    public SetupViewModel ViewModel { get; }
}
