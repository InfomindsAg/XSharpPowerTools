using Microsoft.VisualStudio.Experimentation;
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
        readonly List<ToggleButton> TypeToggleButtons;
        readonly List<ToggleButton> MemberToggleButtons;
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

            MemberToggleButtons = new List<ToggleButton>
            {
                MethodToggleButton,
                PropertyToggleButton,
                FunctionToggleButton,
                VariableToggleButton,
                DefineToggleButton
            };

            TypeToggleButtons = new List<ToggleButton>
            {
                ClassToggleButton,
                EnumToggleButton,
                InterfaceToggleButton,
                StructToggleButton
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
            if (AllowReturn && e.Key == Key.Return && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
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
            else if (Keyboard.Modifiers == ModifierKeys.Control) 
            {
                var toggleButtonChecked = false;
                if (e.Key == Key.D1)
                {
                    MethodToggleButton.IsChecked = !MethodToggleButton.IsChecked;
                    toggleButtonChecked = true;
                }
                else if (e.Key == Key.D2)
                {
                    PropertyToggleButton.IsChecked = !PropertyToggleButton.IsChecked;
                    toggleButtonChecked = true;
                }
                else if (e.Key == Key.D3)
                {
                    FunctionToggleButton.IsChecked = !FunctionToggleButton.IsChecked;
                    toggleButtonChecked = true;
                }
                else if (e.Key == Key.D4)
                {
                    DefineToggleButton.IsChecked = !DefineToggleButton.IsChecked;
                    toggleButtonChecked = true;
                }
    
                if (toggleButtonChecked) 
                {
                    FilterButton_Click(null, null);
                    e.Handled = true;
                }
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

        private void HelpButton_Click(object sender, RoutedEventArgs e) =>
            HelpControl.Visibility = HelpControl.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

        protected override void OnTextChanged() => 
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}OnTextChange");

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

        private void SearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
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

                if (MethodToggleButton.IsChecked.HasValue && MethodToggleButton.IsChecked.Value)
                    filter.MemberFilters.Add(MemberFilter.Method);
                if (PropertyToggleButton.IsChecked.HasValue && PropertyToggleButton.IsChecked.Value)
                    filter.MemberFilters.Add(MemberFilter.Property);
                if (FunctionToggleButton.IsChecked.HasValue && FunctionToggleButton.IsChecked.Value)
                    filter.MemberFilters.Add(MemberFilter.Function);
                if (VariableToggleButton.IsChecked.HasValue && VariableToggleButton.IsChecked.Value)
                    filter.MemberFilters.Add(MemberFilter.Variable);
                if (DefineToggleButton.IsChecked.HasValue && DefineToggleButton.IsChecked.Value)
                    filter.MemberFilters.Add(MemberFilter.Define);
            }
            else if (ActiveFilterGroup == FilterType.Type)
            {
                filter.TypeFilters = new List<TypeFilter>();

                if (ClassToggleButton.IsChecked.HasValue && ClassToggleButton.IsChecked.Value)
                    filter.TypeFilters.Add(TypeFilter.Class);
                if (EnumToggleButton.IsChecked.HasValue && EnumToggleButton.IsChecked.Value)
                    filter.TypeFilters.Add(TypeFilter.Enum);
                if (InterfaceToggleButton.IsChecked.HasValue && InterfaceToggleButton.IsChecked.Value)
                    filter.TypeFilters.Add(TypeFilter.Interface);
                if (StructToggleButton.IsChecked.HasValue && StructToggleButton.IsChecked.Value)
                    filter.TypeFilters.Add(TypeFilter.Struct);
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

            if (TypeToggleButtons.Contains(sender))
            {
                filterGroupToDeactivate = MemberFilterGrid.Children;
                if (TypeToggleButtons.Any(q => q.IsChecked.HasValue && q.IsChecked.Value))
                    ActiveFilterGroup = FilterType.Type;
                else
                    ActiveFilterGroup = FilterType.Inactive;
            }
            else if (MemberToggleButtons.Contains(sender as ToggleButton))
            {
                filterGroupToDeactivate = TypeFilterGrid.Children;
                if (MemberToggleButtons.Any(q => q.IsChecked.HasValue && q.IsChecked.Value))
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
            ClassToggleButton.IsChecked = false;
            EnumToggleButton.IsChecked = false;
            InterfaceToggleButton.IsChecked = false;
            StructToggleButton.IsChecked = false;

            MethodToggleButton.IsChecked = false;
            PropertyToggleButton.IsChecked = false;
            FunctionToggleButton.IsChecked = false;
            VariableToggleButton.IsChecked = false;
            DefineToggleButton.IsChecked = false;

            ActiveFilterGroup = FilterType.Inactive;

            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}RefreshButton_Click");
        }
    }
}