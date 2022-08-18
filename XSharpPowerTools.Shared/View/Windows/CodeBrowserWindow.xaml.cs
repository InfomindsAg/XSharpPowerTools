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
        readonly Dictionary<FilterButton, TypeFilter> TypeFilterButtons;
        readonly Dictionary<FilterButton, MemberFilter> MemberFilterButtons;
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

            MemberFilterButtons = new Dictionary<FilterButton, MemberFilter>
            {
                { MethodFilterButton, MemberFilter.Method },
                { PropertyFilterButton, MemberFilter.Property },
                { FunctionFilterButton, MemberFilter.Function },
                { VariableFilterButton, MemberFilter.Variable },
                { DefineFilterButton, MemberFilter.Define },
                { EnumValueFilterButton, MemberFilter.EnumValue }
            };

            TypeFilterButtons = new Dictionary<FilterButton, TypeFilter>
            {
                { ClassFilterButton, TypeFilter.Class },
                { EnumFilterButton, TypeFilter.Enum },
                { InterfaceFilterButton, TypeFilter.Interface },
                { StructFilterButton, TypeFilter.Struct }
            };

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
            await DocumentHelper.OpenProjectItemAtAsync(item.ContainingFile, item.Line);
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
                foreach (var typeFilterButton in TypeFilterButtons.Keys)
                    typeFilterButton.ShowPopup();

                foreach (var memberFilterButton in MemberFilterButtons.Keys)
                    memberFilterButton.ShowPopup();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var filterButtonToCheck = e.Key switch
                {
                    Key.D1 => MethodFilterButton,
                    Key.D2 => PropertyFilterButton,
                    Key.D3 => FunctionFilterButton,
                    Key.D4 => VariableFilterButton,
                    Key.D5 => DefineFilterButton,
                    Key.D6 => EnumValueFilterButton,
                    Key.D7 => ClassFilterButton,
                    Key.D8 => EnumFilterButton,
                    Key.D9 => InterfaceFilterButton,
                    Key.D0 => StructFilterButton,
                    _ => null
                };

                if (filterButtonToCheck != null)
                {
                    filterButtonToCheck.IsChecked = !filterButtonToCheck.IsChecked;
                    FilterButton_Click(filterButtonToCheck, null);
                    e.Handled = true;
                }
            }
        }

        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                foreach (var typeFilterButton in TypeFilterButtons.Keys)
                    typeFilterButton.HidePopup();

                foreach (var memberFilterButton in MemberFilterButtons.Keys)
                    memberFilterButton.HidePopup();
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
            foreach (var typeFilterButton in TypeFilterButtons.Keys)
                typeFilterButton.HidePopup();

            foreach (var memberFilterButton in MemberFilterButtons.Keys)
                memberFilterButton.HidePopup();
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
                filter.MemberFilters = new List<MemberFilter>();
                filter.MemberFilters.AddRange(MemberFilterButtons.Where(q => q.Key.IsChecked.HasValue && q.Key.IsChecked.Value).Select(q => q.Value));
            }
            else if (ActiveFilterGroup == FilterType.Type)
            {
                filter.TypeFilters = new List<TypeFilter>();
                filter.TypeFilters.AddRange(TypeFilterButtons.Where(q => q.Key.IsChecked.HasValue && q.Key.IsChecked.Value).Select(q => q.Value));
            }
            else if (ActiveFilterGroup == FilterType.Inactive)
            {
                filter.MemberFilters = new List<MemberFilter>
                {
                    MemberFilter.Method,
                    MemberFilter.Property,
                    MemberFilter.Function,
                    MemberFilter.Variable,
                    MemberFilter.Define
                };
                filter.TypeFilters = new List<TypeFilter>
                {
                    TypeFilter.Class,
                    TypeFilter.Enum,
                    TypeFilter.Interface,
                    TypeFilter.Struct
                };
            }

            return filter;
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e) 
        {
            UIElementCollection filterGroupToDeactivate = null;

            if (TypeFilterButtons.ContainsKey(sender as FilterButton))
            {
                filterGroupToDeactivate = MemberFilterGrid.Children;
                if (TypeFilterButtons.Any(q => q.Key.IsChecked.HasValue && q.Key.IsChecked.Value))
                    ActiveFilterGroup = FilterType.Type;
                else
                    ActiveFilterGroup = FilterType.Inactive;
            }
            else if (MemberFilterButtons.ContainsKey(sender as FilterButton))
            {
                filterGroupToDeactivate = TypeFilterGrid.Children;
                if (MemberFilterButtons.Any(q => q.Key.IsChecked.HasValue && q.Key.IsChecked.Value))
                    ActiveFilterGroup = FilterType.Member;
                else
                    ActiveFilterGroup = FilterType.Inactive;
            }

            if (filterGroupToDeactivate == null || filterGroupToDeactivate.Count < 1)
                return;

            foreach (var child in filterGroupToDeactivate) 
            {
                if (child is ToggleButton toggleButton)
                    toggleButton.IsChecked = false;
            }

            SearchTextBox.Focus();
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}FilterButton_Click");
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e) 
        {
            ClassFilterButton.IsChecked = false;
            EnumFilterButton.IsChecked = false;
            InterfaceFilterButton.IsChecked = false;
            StructFilterButton.IsChecked = false;

            MethodFilterButton.IsChecked = false;
            PropertyFilterButton.IsChecked = false;
            FunctionFilterButton.IsChecked = false;
            VariableFilterButton.IsChecked = false;
            DefineFilterButton.IsChecked = false;

            ActiveFilterGroup = FilterType.Inactive;

            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}RefreshButton_Click");
        }
    }
}