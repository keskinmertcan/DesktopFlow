using DesktopFlow.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DesktopFlow.App.Views;

public sealed partial class OrganizerPage : Page
{
    public OrganizerPage(OrganizerViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadAsync(CancellationToken.None);
    }

    public OrganizerViewModel ViewModel { get; }
}
