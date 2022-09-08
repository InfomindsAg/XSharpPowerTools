using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using XSharpPowerTools.Helpers;
using XSharpPowerTools.View.Controls;
using static Microsoft.VisualStudio.Shell.VsTaskLibraryHelper;

namespace XSharpPowerTools.View.Windows
{
    /// <summary>
    /// Interaction logic for CodeBrowserWindow.xaml
    /// </summary>
    public partial class CodeBrowserWindow : BaseWindow, IResultsDataGridParent
    {
        const string FileReference = "vs/XSharpPowerTools/CodeBrowser/";
        readonly string SolutionDirectory;
        FilterType ActiveFilterGroup;
        XSModelResultType DisplayedResultType;
        string LastSearchTerm;
        volatile bool SearchActive = false;
        volatile bool ReDoSearch = false;

        public override string SearchTerm
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    SearchTextBox.Text = value;
            }
        }

        public CodeBrowserWindow(string solutionDirectory) : base()
        {
            InitializeComponent();
            SolutionDirectory = solutionDirectory;
            ResultsDataGrid.Parent = this;

            SearchTextBox.WhenTextChanged
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Subscribe(x => OnTextChanged());

            ActiveFilterGroup = FilterType.Inactive;
        }

        private void SetTableColumns(XSModelResultType resultType)
        {
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

        protected async Task SearchAsync(ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null)
        {
            if (string.IsNullOrWhiteSpace(SearchTextBox.Text))
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
                    var searchTerm = SearchTextBox.Text.Trim();
                    ReDoSearch = false;

                    string currentFile;
                    int caretPosition;
                    if (searchTerm.StartsWith("..") || searchTerm.StartsWith("::"))
                    {
                        currentFile = await DocumentHelper.GetCurrentFileAsync();
                        caretPosition = await DocumentHelper.GetCaretPositionAsync();
                    }
                    else
                    {
                        currentFile = null;
                        caretPosition = -1;
                    }

                    var (results, resultType) = await XSModel.GetSearchTermMatchesAsync(searchTerm, GetFilter(), SolutionDirectory, currentFile, caretPosition, direction, orderBy);

                    ResultsDataGrid.ItemsSource = results;
                    ResultsDataGrid.SelectedItem = results.FirstOrDefault();
                    SetTableColumns(resultType);
                    DisplayedResultType = resultType;
                    LastSearchTerm = searchTerm;

                    NoResultsLabel.Visibility = results.Count < 1 ? Visibility.Visible : Visibility.Collapsed;

                } while (ReDoSearch);
            }
            finally
            {
                SearchActive = false;
                AllowReturn = true;
            }
        }

        private async Task OpenItemAsync(XSModelResultItem item)
        {
            if (item == null)
                return;

            using var waitCursor = new WithWaitCursor();
            var keyword = item.ResultType == XSModelResultType.Member ? item.MemberName : item.TypeName;
            await DocumentHelper.OpenProjectItemAtAsync(item.ContainingFile, item.Line, item.SourceCode, keyword);
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (AllowReturn && e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SaveResultsToToolWindow();
            }
            else if (AllowReturn && e.Key == Key.Return)
            {
                XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () =>
                {
                    if (ResultsDataGrid.SelectedItem is XSModelResultItem item && item != null)
                        await OpenItemAsync(item);
                    else
                        await SearchAsync();
                }).FileAndForget($"{FileReference}Window_PreviewKeyDown");
            }
            else if (e.Key == Key.Down)
            {
                ResultsDataGrid.SelectNext();
            }
            else if (e.Key == Key.Up)
            {
                ResultsDataGrid.SelectPrevious();
            }
            else if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                MemberFilterGroup.ShowPopups();
                TypeFilterGroup.ShowPopups();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var isMemberHotKey = MemberFilterGroup.TryHandleHotKey(e.Key);
                var isTypeHotKey = TypeFilterGroup.TryHandleHotKey(e.Key);

                if (isMemberHotKey && isTypeHotKey)
                    return;

                if (isMemberHotKey) 
                {
                    FilterGroup_Click(MemberFilterGroup, null);
                    e.Handled = true;
                }
                else if (isTypeHotKey) 
                {
                    FilterGroup_Click(TypeFilterGroup, null);
                    e.Handled = true;
                }
            }
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                MemberFilterGroup.HidePopups();
                TypeFilterGroup.HidePopups();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await SearchAsync()).FileAndForget($"{FileReference}Window_Loaded");
            SearchTextBox.CaretIndex = int.MaxValue;
            try
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
            }
            catch (Exception)
            { }
        }

        private void Window_LostFocus(object sender, RoutedEventArgs e)
        {
            MemberFilterGroup.HidePopups();
            TypeFilterGroup.HidePopups();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e) =>
            HelpControl.Visibility = HelpControl.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

        protected override void OnTextChanged() => 
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}OnTextChanged");

        private async Task DoSearchAsync()
        {
            await XSharpPowerToolsPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();
            await SearchAsync();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) =>
            AllowReturn = false;

        public void OnReturn(object selectedItem)
        {
            if (AllowReturn)
            {
                var item = selectedItem as XSModelResultItem;
                XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await OpenItemAsync(item)).FileAndForget($"{FileReference}OnReturn");
            }
        }

        private void ResultsViewButton_Click(object sender, RoutedEventArgs e) =>
            SaveResultsToToolWindow();

        private void SaveResultsToToolWindow()
        {
            if (ResultsDataGrid.Items.Count < 1)
                return;

            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () =>
            {
                using var waitCursor = new WithWaitCursor();

                if (ResultsDataGrid.SelectedItem != null)
                    await OpenItemAsync(ResultsDataGrid.SelectedItem as XSModelResultItem);
                else
                    Close();

                var toolWindowPane = await CodeBrowserResultsToolWindow.ShowAsync();

                var items = ResultsDataGrid.ItemsSource as List<XSModelResultItem>;
                await (toolWindowPane.Content as ToolWindowControl).UpdateToolWindowContentsAsync(XSModel, GetFilter(), LastSearchTerm, SolutionDirectory, items, DisplayedResultType);

            }).FileAndForget($"{FileReference}SaveResultsToToolWindow");
        }

        public void OnSort(ResultsDataGrid sender, DataGridSortingEventArgs e) 
        {
            var column = e.Column;
           
            var direction = (column.SortDirection != ListSortDirection.Ascending) ? ListSortDirection.Ascending : ListSortDirection.Descending;
            var lcv = (ListCollectionView)CollectionViewSource.GetDefaultView(sender.ItemsSource);
            var comparer = new CodeBrowserResultComparer(direction, column, DisplayedResultType);

            if (lcv.Count < 100)
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

        private Filter GetFilter() 
        {
            var filter = new Filter { Type = ActiveFilterGroup };

            if (ActiveFilterGroup == FilterType.Member)
            {
                filter.MemberFilters = MemberFilterGroup.GetFilters();
            }
            else if (ActiveFilterGroup == FilterType.Type)
            {
                filter.TypeFilters = TypeFilterGroup.GetFilters();
            }
            else if (ActiveFilterGroup == FilterType.Inactive)
            {
                filter.MemberFilters = MemberFilterGroup.GetAllFilters();
                filter.TypeFilters = TypeFilterGroup.GetAllFilters();
            }

            return filter;
        }

        private void FilterGroup_Click(object sender, RoutedEventArgs e) 
        {
            if (sender == MemberFilterGroup)
            {
                TypeFilterGroup.Deactivate();
                ActiveFilterGroup = MemberFilterGroup.IsActive() ? FilterType.Member : FilterType.Inactive;
            }
            else if (sender == TypeFilterGroup)
            {
                MemberFilterGroup.Deactivate();
                ActiveFilterGroup = TypeFilterGroup.IsActive() ? FilterType.Type : FilterType.Inactive;
            }
            else 
            {
                return;
            }

            SearchTextBox.Focus();
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}FilterButton_Click");
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) 
        {
            MemberFilterGroup.Deactivate();
            TypeFilterGroup.Deactivate();
            ActiveFilterGroup = FilterType.Inactive;

            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}RefreshButton_Click");
        }
    }
}