using System.Windows.Controls;
using DocPoint.App.ViewModels;

namespace DocPoint.App.Views.Pages;

public partial class HomePage : Page
{
    public HomePage(HomeViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
