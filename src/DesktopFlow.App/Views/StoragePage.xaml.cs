using DesktopFlow.App.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace DesktopFlow.App.Views;

public sealed partial class StoragePage : Page
{
    public StoragePage(StorageViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => ViewModel.Load();
    }

    public StorageViewModel ViewModel { get; }
}
