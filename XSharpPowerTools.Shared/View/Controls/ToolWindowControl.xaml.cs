using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using XSharpPowerTools.Helpers;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.View.Controls
{
    public class Results : List<XSModelResultItem> //required for DataBinding for grouping
    { }

    /// <summary>
    /// Interaction logic for ToolWindowControl.xaml
    /// </summary>
    public partial class ToolWindowControl : UserControl, IResultsDataGridParent
    {
        const string FileReference = "vs/XSharpPowerTools/ToolWindowControl/";
        private XSModel XSModel;
        private XSModelResultType DisplayedResultType;
        private string SearchTerm;
        private string SolutionDirectory;
        private readonly MenuItem GroupingMenuItem;
        private readonly MenuItem MethodFilterMenuItem;
        private readonly MenuItem FunctionFilterMenuItem;
        private readonly MenuItem PropertyFilterMenuItem;
        private readonly MenuItem VariableFilterMenuItem;
        private readonly MenuItem DefineFilterMenuItem;
        volatile bool SearchActive = false;
        volatile bool ReDoSearch = false;

        public ToolWindowControl()
        {
            InitializeComponent();
            ResultsDataGrid.Parent = this;

            MethodFilterMenuItem = new MenuItem { Header = "Methods", IsCheckable = true };
            FunctionFilterMenuItem = new MenuItem { Header = "Properties", IsCheckable = true };
            PropertyFilterMenuItem = new MenuItem { Header = "Functions", IsCheckable = true };
            VariableFilterMenuItem = new MenuItem { Header = "Variables", IsCheckable = true };
            DefineFilterMenuItem = new MenuItem { Header = "Defines", IsCheckable = true };

            GroupingMenuItem = new MenuItem { Header = "Toggle grouping", IsCheckable = true, IsChecked = true };
            GroupingMenuItem.Checked += Grouping_ContextMenu_Click;
            GroupingMenuItem.Unchecked += Grouping_ContextMenu_Click;

            var refreshResults = new MenuItem { Header = "Refresh results" };
            refreshResults.Click += RefreshResults_ContextMenu_Click;

            ResultsDataGrid.ContextMenu = new ContextMenu();

            ResultsDataGrid.ContextMenu.Items.Add(GroupingMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(new Separator());
            ResultsDataGrid.ContextMenu.Items.Add(MethodFilterMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(FunctionFilterMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(PropertyFilterMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(VariableFilterMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(DefineFilterMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(new Separator());
            ResultsDataGrid.ContextMenu.Items.Add(refreshResults);
        }

        public void OnReturn(object selectedItem)
        {
            if (selectedItem == null)
                return;
            var item = selectedItem as XSModelResultItem;
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DocumentHelper.OpenProjectItemAtAsync(item.ContainingFile, item.Line)).FileAndForget($"{FileReference}OnReturn");
        }

        public async Task UpdateToolWindowContentsAsync(XSModel xsModel, List<FilterableKind> filters, string searchTerm, string solutionDirectory, List<XSModelResultItem> results, XSModelResultType resultType)
        {
            XSModel = xsModel;
            SearchTerm = searchTerm;
            SolutionDirectory = solutionDirectory;
            SetFilters(filters);

            if (results == null || results.Count >= 100 || results.Count < 1) 
            {
                if (SearchTerm.StartsWith("..") || SearchTerm.StartsWith("::"))
                {
                    var currentFile = await DocumentHelper.GetCurrentFileAsync();
                    var caretPosition = await DocumentHelper.GetCaretPositionAsync();
                    (results, resultType) = await XSModel.GetSearchTermMatchesAsync(searchTerm, filters, solutionDirectory, currentFile, caretPosition, 2000); //aus DB, max 2000
                }
                else
                {
                    (results, resultType) = await XSModel.GetSearchTermMatchesAsync(searchTerm, filters, solutionDirectory, 2000); //aus DB, max 2000
                }
            }

            DisplayedResultType = resultType;
            SetTableColumns(resultType);

            var _results = Resources["Results"] as Results;
            _results.Clear();
            _results.AddRange(results);

            if (GroupingMenuItem.IsChecked)
            {
                var cvResults = CollectionViewSource.GetDefaultView(ResultsDataGrid.ItemsSource);
                if (cvResults != null && cvResults.CanGroup)
                {
                    cvResults.GroupDescriptions.Clear();
                    cvResults.GroupDescriptions.Add(new PropertyGroupDescription("Project"));
                }
            }
        }

        private void SetTableColumns(XSModelResultType resultType)
        {
            ResultsDataGrid.Columns[0].Visibility = resultType == XSModelResultType.Procedure
                ? Visibility.Collapsed
                : Visibility.Visible;

            var memberSpecificColumnsVisibility = resultType == XSModelResultType.Type
                ? Visibility.Collapsed
                : Visibility.Visible;

            ResultsDataGrid.Columns[1].Visibility = memberSpecificColumnsVisibility;
            ResultsDataGrid.Columns[2].Visibility = memberSpecificColumnsVisibility;

            ResultsDataGrid.Columns[0].Width = 0;
            ResultsDataGrid.Columns[1].Width = 0;
            ResultsDataGrid.Columns[2].Width = 0;
            ResultsDataGrid.Columns[3].Width = 0;
            ResultsDataGrid.Columns[4].Width = 0;
            ResultsDataGrid.UpdateLayout();
            ResultsDataGrid.Columns[0].Width = new DataGridLength(4, DataGridLengthUnitType.Star);
            ResultsDataGrid.Columns[1].Width = new DataGridLength(4, DataGridLengthUnitType.Star);
            ResultsDataGrid.Columns[2].Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
            ResultsDataGrid.Columns[3].Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
            ResultsDataGrid.Columns[4].Width = new DataGridLength(7, DataGridLengthUnitType.Star);
        }

        public void SolutionEvents_OnBeforeCloseSolution()
        {
            SetTableColumns(XSModelResultType.Member);

            var _results = Resources["Results"] as Results;
            _results.Clear();
        }

        public void OnSort(ResultsDataGrid sender, DataGridSortingEventArgs e)
        {
            var column = e.Column;

            var direction = (column.SortDirection != ListSortDirection.Ascending) ? ListSortDirection.Ascending : ListSortDirection.Descending;
            var lcv = (ListCollectionView)CollectionViewSource.GetDefaultView(sender.ItemsSource);
            var comparer = new CodeBrowserResultComparer(direction, column, DisplayedResultType);

            if (lcv.Count < 2000)
            {
                lcv.CustomSort = comparer;
                column.SortDirection = direction;
            }
            else
            {
                XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await SearchAsync(direction, comparer.SqlOrderBy)).FileAndForget($"{FileReference}OnSort");
                column.SortDirection = direction;
            }
            e.Handled = true;
        }

        protected async Task SearchAsync(ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null)
        {
            if (string.IsNullOrWhiteSpace(SearchTerm))
                return;

            if (SearchActive)
            {
                ReDoSearch = SearchActive;
                return;
            }

            using var waitCursor = new WithWaitCursor();
            SearchActive = true;
            try
            {
                do
                {
                    ReDoSearch = false;

                    List<XSModelResultItem> results;
                    XSModelResultType _;
                    if (SearchTerm.StartsWith("..") || SearchTerm.StartsWith("::"))
                    {
                        var currentFile = await DocumentHelper.GetCurrentFileAsync();
                        var caretPosition = await DocumentHelper.GetCaretPositionAsync();
                        (results, _) = await XSModel.GetSearchTermMatchesAsync(SearchTerm, GetFilters(), SolutionDirectory, currentFile, caretPosition, 2000, direction, orderBy); //aus DB, max 2000
                    }
                    else
                    {
                        (results, _) = await XSModel.GetSearchTermMatchesAsync(SearchTerm, GetFilters(), SolutionDirectory, 2000, direction, orderBy);
                    }

                    var _results = Resources["Results"] as Results;
                    _results.Clear();
                    _results.AddRange(results);

                    if (GroupingMenuItem.IsChecked)
                    {
                        var cvResults = CollectionViewSource.GetDefaultView(ResultsDataGrid.ItemsSource);
                        if (cvResults != null && cvResults.CanGroup)
                        {
                            cvResults.GroupDescriptions.Clear();
                            cvResults.GroupDescriptions.Add(new PropertyGroupDescription("Project"));
                        }
                    }

                    ResultsDataGrid.SelectedItem = results.FirstOrDefault();
                    
                } while (ReDoSearch);
            }
            finally
            {
                SearchActive = false;
            }
        }

        public void Grouping_ContextMenu_Click(object sender, RoutedEventArgs e) 
        {
            var cvResults = CollectionViewSource.GetDefaultView(ResultsDataGrid.ItemsSource);
            if (cvResults != null && cvResults.CanGroup)
            {
                cvResults.GroupDescriptions.Clear();
                if (GroupingMenuItem.IsChecked)
                    cvResults.GroupDescriptions.Add(new PropertyGroupDescription("Project"));
            }
        }

        public void RefreshResults_ContextMenu_Click(object sender, RoutedEventArgs e) => 
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await SearchAsync()).FileAndForget($"{FileReference}RefreshResults_ContextMenu_Click");

        private List<FilterableKind> GetFilters()
        {
            var filters = new List<FilterableKind>();

            if (MethodFilterMenuItem.IsChecked)
                filters.Add(FilterableKind.Method);
            if (PropertyFilterMenuItem.IsChecked)
                filters.Add(FilterableKind.Property);
            if (FunctionFilterMenuItem.IsChecked)
                filters.Add(FilterableKind.Function);
            if (VariableFilterMenuItem.IsChecked)
                filters.Add(FilterableKind.Variable);
            if (DefineFilterMenuItem.IsChecked)
                filters.Add(FilterableKind.Define);

            if (filters.Count < 1)
            {
                filters = new List<FilterableKind>
                    {
                        FilterableKind.Method,
                        FilterableKind.Property,
                        FilterableKind.Function,
                        FilterableKind.Variable,
                        FilterableKind.Define
                    };
            }

            return filters;
        }

        private void SetFilters(List<FilterableKind> filters) 
        { 
            if (filters.Count < 1) 
            {
                MethodFilterMenuItem.IsChecked = true;
                PropertyFilterMenuItem.IsChecked = true;
                FunctionFilterMenuItem.IsChecked = true;
                VariableFilterMenuItem.IsChecked = true;
                DefineFilterMenuItem.IsChecked = true;
            }
            else 
            {
                MethodFilterMenuItem.IsChecked = false;
                PropertyFilterMenuItem.IsChecked = false;
                FunctionFilterMenuItem.IsChecked = false;
                VariableFilterMenuItem.IsChecked = false;
                DefineFilterMenuItem.IsChecked = false;

                foreach ( var filter in filters) 
                {
                    if (filter == FilterableKind.Method)
                        MethodFilterMenuItem.IsChecked = true;
                    else if (filter == FilterableKind.Property)
                        PropertyFilterMenuItem.IsChecked = true;
                    else if (filter == FilterableKind.Function)
                        FunctionFilterMenuItem.IsChecked = true;
                    else if (filter == FilterableKind.Variable)
                        VariableFilterMenuItem.IsChecked = true;
                    else if (filter == FilterableKind.Define)
                        DefineFilterMenuItem.IsChecked = true;
                }
            }
        }
    }
}
