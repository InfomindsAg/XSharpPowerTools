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
        private List<FilterableKind> Filters;
        volatile bool SearchActive = false;
        volatile bool ReDoSearch = false;
        volatile bool ShouldGroup = true;

        public ToolWindowControl()
        {
            InitializeComponent();
            ResultsDataGrid.Parent = this;
        }

        public void OnReturn(object selectedItem)
        {
            if (selectedItem == null)
                return;
            var item = selectedItem as XSModelResultItem;
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DocumentHelper.OpenProjectItemAtAsync(item.ContainingFile, item.Line)).FileAndForget($"{FileReference}OnReturn");
        }

        public void UpdateToolWindowContents(XSModelResultType resultType, List<XSModelResultItem> results)
        {
            SetTableColumns(resultType);

            var _results = Resources["Results"] as Results;
            _results.Clear();
            _results.AddRange(results);

            if (ShouldGroup)
            {
                var cvResults = CollectionViewSource.GetDefaultView(ResultsDataGrid.ItemsSource);
                if (cvResults != null && cvResults.CanGroup)
                {
                    cvResults.GroupDescriptions.Clear();
                    cvResults.GroupDescriptions.Add(new PropertyGroupDescription("Project"));
                }
            }
        }

        public async Task UpdateToolWindowContentsAsync(XSModel xsModel, List<FilterableKind> filters, string searchTerm, string solutionDirectory)
        {
            XSModel = xsModel;
            SearchTerm = searchTerm;
            SolutionDirectory = solutionDirectory;
            Filters = filters;

            List<XSModelResultItem> results;
            XSModelResultType resultType;
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
            
            DisplayedResultType = resultType;
            SetTableColumns(resultType);

            var _results = Resources["Results"] as Results;
            _results.Clear();
            _results.AddRange(results);

            if (ShouldGroup)
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

        public void SolutionEvents_OnBeforeCloseSolution() =>
            UpdateToolWindowContents(XSModelResultType.Member, new List<XSModelResultItem>());

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
                        (results, _) = await XSModel.GetSearchTermMatchesAsync(SearchTerm, Filters, SolutionDirectory, currentFile, caretPosition, 2000, direction, orderBy); //aus DB, max 2000
                    }
                    else
                    {
                        (results, _) = await XSModel.GetSearchTermMatchesAsync(SearchTerm, Filters, SolutionDirectory, 2000, direction, orderBy);
                    }

                    ResultsDataGrid.ItemsSource = results;

                    var _results = Resources["Results"] as Results;
                    _results.Clear();
                    _results.AddRange(results);

                    if (ShouldGroup)
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

        public void ContextMenu_Click(object sender, RoutedEventArgs e) 
        {
            ShouldGroup = !ShouldGroup;
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await SearchAsync()).FileAndForget($"{FileReference}ContextMenu_Click");
        }
    }
}
