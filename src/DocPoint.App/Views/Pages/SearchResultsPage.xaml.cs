using System.Windows.Controls;
using DocPoint.App.ViewModels;

namespace DocPoint.App.Views.Pages;

public partial class SearchResultsPage : Page
{
    public SearchResultsPage(SearchResultsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
