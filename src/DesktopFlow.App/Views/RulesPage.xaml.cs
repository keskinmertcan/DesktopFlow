using DesktopFlow.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DesktopFlow.App.Views;

public sealed partial class RulesPage : Page
{
    public RulesPage(RulesViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadAsync(CancellationToken.None);
    }

    public RulesViewModel ViewModel { get; }
}
