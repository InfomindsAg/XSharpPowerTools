using Microsoft.VisualStudio.Experimentation;
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
        private FilterType ActiveFilterGroup;
        private readonly List<MenuItem> TypeMenuItems;
        private readonly List<MenuItem> MemberMenuItems;
        private readonly MenuItem GroupingMenuItem;
        private readonly MenuItem ClassFilterMenuItem;
        private readonly MenuItem EnumFilterMenuItem;
        private readonly MenuItem InterfaceFilterMenuItem;
        private readonly MenuItem StructFilterMenuItem;
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

            ClassFilterMenuItem = new MenuItem { Header = "Classes", IsCheckable = true, StaysOpenOnClick = true };
            EnumFilterMenuItem = new MenuItem { Header = "Enums", IsCheckable = true, StaysOpenOnClick = true };
            InterfaceFilterMenuItem = new MenuItem { Header = "Interfaces", IsCheckable = true, StaysOpenOnClick = true };
            StructFilterMenuItem = new MenuItem { Header = "Structs", IsCheckable = true, StaysOpenOnClick = true };

            TypeMenuItems = new List<MenuItem>
            {
                ClassFilterMenuItem,
                EnumFilterMenuItem,
                InterfaceFilterMenuItem,
                StructFilterMenuItem
            };

            foreach (var typeMenuItem in TypeMenuItems)
            {
                typeMenuItem.Checked += TypeFilter_ContextMenu_Checked;
                typeMenuItem.Unchecked += Filter_ContextMenu_Unchecked;
            }

            MethodFilterMenuItem = new MenuItem { Header = "Methods", IsCheckable = true, StaysOpenOnClick = true };
            FunctionFilterMenuItem = new MenuItem { Header = "Properties", IsCheckable = true, StaysOpenOnClick = true };
            PropertyFilterMenuItem = new MenuItem { Header = "Functions", IsCheckable = true, StaysOpenOnClick = true };
            VariableFilterMenuItem = new MenuItem { Header = "Variables", IsCheckable = true, StaysOpenOnClick = true };
            DefineFilterMenuItem = new MenuItem { Header = "Defines", IsCheckable = true, StaysOpenOnClick = true };

            MemberMenuItems = new List<MenuItem>
            {
                MethodFilterMenuItem,
                FunctionFilterMenuItem,
                PropertyFilterMenuItem,
                VariableFilterMenuItem,
                DefineFilterMenuItem
            };

            foreach (var memberMenuItem in MemberMenuItems)
            {
                memberMenuItem.Checked += MemberFilter_ContextMenu_Checked;
                memberMenuItem.Unchecked += Filter_ContextMenu_Unchecked;
            }

            GroupingMenuItem = new MenuItem { Header = "Grouping", IsCheckable = true, IsChecked = true };
            GroupingMenuItem.Checked += Grouping_ContextMenu_Click;
            GroupingMenuItem.Unchecked += Grouping_ContextMenu_Click;

            var refreshResults = new MenuItem { Header = "Refresh results" };
            refreshResults.Click += RefreshResults_ContextMenu_Click;

            ResultsDataGrid.ContextMenu = new ContextMenu();

            ResultsDataGrid.ContextMenu.Items.Add(GroupingMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(new Separator());
            ResultsDataGrid.ContextMenu.Items.Add(ClassFilterMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(EnumFilterMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(InterfaceFilterMenuItem);
            ResultsDataGrid.ContextMenu.Items.Add(StructFilterMenuItem);
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

        public async Task UpdateToolWindowContentsAsync(XSModel xsModel, Filter filter, string searchTerm, string solutionDirectory, List<XSModelResultItem> results, XSModelResultType resultType)
        {
            XSModel = xsModel;
            SearchTerm = searchTerm;
            SolutionDirectory = solutionDirectory;
            SetFilter(filter);

            if (results == null || results.Count >= 100 || results.Count < 1) 
            {
                if (SearchTerm.StartsWith("..") || SearchTerm.StartsWith("::"))
                {
                    var currentFile = await DocumentHelper.GetCurrentFileAsync();
                    var caretPosition = await DocumentHelper.GetCaretPositionAsync();
                    (results, resultType) = await XSModel.GetSearchTermMatchesAsync(searchTerm, filter, solutionDirectory, currentFile, caretPosition, 2000); //aus DB, max 2000
                }
                else
                {
                    (results, resultType) = await XSModel.GetSearchTermMatchesAsync(searchTerm, filter, solutionDirectory, 2000); //aus DB, max 2000
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
                        (results, _) = await XSModel.GetSearchTermMatchesAsync(SearchTerm, GetFilter(), SolutionDirectory, currentFile, caretPosition, 2000, direction, orderBy); //aus DB, max 2000
                    }
                    else
                    {
                        (results, _) = await XSModel.GetSearchTermMatchesAsync(SearchTerm, GetFilter(), SolutionDirectory, 2000, direction, orderBy);
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

        private Filter GetFilter()
        {
            var filter = new Filter { Type = ActiveFilterGroup };

            if (ActiveFilterGroup == FilterType.Member)
            {
                filter.MemberFilters = new List<MemberFilter>();

                if (MethodFilterMenuItem.IsChecked)
                    filter.MemberFilters.Add(MemberFilter.Method);
                if (PropertyFilterMenuItem.IsChecked)
                    filter.MemberFilters.Add(MemberFilter.Property);
                if (FunctionFilterMenuItem.IsChecked)
                    filter.MemberFilters.Add(MemberFilter.Function);
                if (VariableFilterMenuItem.IsChecked)
                    filter.MemberFilters.Add(MemberFilter.Variable);
                if (DefineFilterMenuItem.IsChecked)
                    filter.MemberFilters.Add(MemberFilter.Define);
            }
            else if (ActiveFilterGroup == FilterType.Type)
            {
                filter.TypeFilters = new List<TypeFilter>();

                if (ClassFilterMenuItem.IsChecked)
                    filter.TypeFilters.Add(TypeFilter.Class);
                if (EnumFilterMenuItem.IsChecked)
                    filter.TypeFilters.Add(TypeFilter.Enum);
                if (InterfaceFilterMenuItem.IsChecked)
                    filter.TypeFilters.Add(TypeFilter.Interface);
                if (StructFilterMenuItem.IsChecked)
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

        private void SetFilter(Filter filter) 
        {
            ActiveFilterGroup = filter.Type;
            
            TypeMenuItems.ForEach(q => q.IsChecked = false);
            MemberMenuItems.ForEach(q => q.IsChecked = false);

            if (ActiveFilterGroup == FilterType.Type)
            {
                foreach (var typeFilter in filter.TypeFilters)
                {
                    if (typeFilter == TypeFilter.Class)
                        ClassFilterMenuItem.IsChecked = true;
                    else if (typeFilter == TypeFilter.Enum)
                        EnumFilterMenuItem.IsChecked = true;
                    else if (typeFilter == TypeFilter.Interface)
                        InterfaceFilterMenuItem.IsChecked = true;
                    else if (typeFilter == TypeFilter.Struct)
                        StructFilterMenuItem.IsChecked = true;
                }
            }
            else if (ActiveFilterGroup == FilterType.Member)
            {
                foreach (var memberFilter in filter.MemberFilters)
                {
                    if (memberFilter == MemberFilter.Method)
                        MethodFilterMenuItem.IsChecked = true;
                    else if (memberFilter == MemberFilter.Property)
                        PropertyFilterMenuItem.IsChecked = true;
                    else if (memberFilter == MemberFilter.Function)
                        FunctionFilterMenuItem.IsChecked = true;
                    else if (memberFilter == MemberFilter.Variable)
                        VariableFilterMenuItem.IsChecked = true;
                    else if (memberFilter == MemberFilter.Define)
                        DefineFilterMenuItem.IsChecked = true;
                }
            }
        }

        public void TypeFilter_ContextMenu_Checked(object sender, RoutedEventArgs e) 
        {
            ActiveFilterGroup = FilterType.Type;
            MemberMenuItems.ForEach(q => q.IsChecked = false);
        }

        public void MemberFilter_ContextMenu_Checked(object sender, RoutedEventArgs e) 
        {
            ActiveFilterGroup = FilterType.Member;
            TypeMenuItems.ForEach(q => q.IsChecked = false);
        }

        public void Filter_ContextMenu_Unchecked(object sender, RoutedEventArgs e) 
        {
            if (TypeMenuItems.All(q => !q.IsChecked) && MemberMenuItems.All(q => !q.IsChecked))
                ActiveFilterGroup = FilterType.Inactive;
        }
    }
}
