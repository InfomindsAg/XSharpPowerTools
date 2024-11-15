﻿using Microsoft.VisualStudio.Experimentation;
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
        private bool FiltersChanged = false;

        private readonly Dictionary<TypeFilter, MenuItem> TypeMenuItems;
        private readonly Dictionary<MemberFilter, MenuItem> MemberMenuItems;
        private readonly MenuItem GroupingMenuItem;

        volatile bool SearchActive = false;
        volatile bool ReDoSearch = false;

        public ToolWindowControl()
        {
            InitializeComponent();
            ResultsDataGrid.Parent = this;

            var classFilterMenuItem = new MenuItem { Header = "Classes", IsCheckable = true, StaysOpenOnClick = true };
            var enumFilterMenuItem = new MenuItem { Header = "Enums", IsCheckable = true, StaysOpenOnClick = true };
            var interfaceFilterMenuItem = new MenuItem { Header = "Interfaces", IsCheckable = true, StaysOpenOnClick = true };
            var structFilterMenuItem = new MenuItem { Header = "Structs", IsCheckable = true, StaysOpenOnClick = true };

            TypeMenuItems = new Dictionary<TypeFilter, MenuItem>
            {
                { TypeFilter.Class, classFilterMenuItem },
                { TypeFilter.Enum, enumFilterMenuItem },
                { TypeFilter.Interface, interfaceFilterMenuItem },
                { TypeFilter.Struct, structFilterMenuItem }
            };

            foreach (var typeMenuItem in TypeMenuItems)
            {
                typeMenuItem.Value.Checked += TypeFilter_ContextMenu_Checked;
                typeMenuItem.Value.Unchecked += Filter_ContextMenu_Unchecked;
            }

            var methodFilterMenuItem = new MenuItem { Header = "Methods", IsCheckable = true, StaysOpenOnClick = true };
            var functionFilterMenuItem = new MenuItem { Header = "Properties", IsCheckable = true, StaysOpenOnClick = true };
            var propertyFilterMenuItem = new MenuItem { Header = "Functions", IsCheckable = true, StaysOpenOnClick = true };
            var variableFilterMenuItem = new MenuItem { Header = "Variables", IsCheckable = true, StaysOpenOnClick = true };
            var defineFilterMenuItem = new MenuItem { Header = "Defines", IsCheckable = true, StaysOpenOnClick = true };
            var enumValueFilterMenuItem = new MenuItem { Header = "Enum values", IsCheckable = true, StaysOpenOnClick = true };

            MemberMenuItems = new Dictionary<MemberFilter, MenuItem>
            {
                { MemberFilter.Method, methodFilterMenuItem },
                { MemberFilter.Function, functionFilterMenuItem },
                { MemberFilter.Property, propertyFilterMenuItem },
                { MemberFilter.Variable, variableFilterMenuItem },
                { MemberFilter.Define, defineFilterMenuItem },
                { MemberFilter.EnumValue, enumValueFilterMenuItem }
            };

            foreach (var memberMenuItem in MemberMenuItems)
            {
                memberMenuItem.Value.Checked += MemberFilter_ContextMenu_Checked;
                memberMenuItem.Value.Unchecked += Filter_ContextMenu_Unchecked;
            }

            GroupingMenuItem = new MenuItem { Header = "Grouping", IsCheckable = true, IsChecked = true };
            GroupingMenuItem.Checked += Grouping_ContextMenu_Click;
            GroupingMenuItem.Unchecked += Grouping_ContextMenu_Click;

            var searchAgain = new MenuItem { Header = "Search again" };
            searchAgain.Click += SearchAgain_Click;

            var applyChanges = new MenuItem { Header = "Apply changes" };
            applyChanges.Click += ApplyChanges_ContextMenu_Click;

            ContextMenu = new ContextMenu();
            ContextMenu.Closed += ContextMenu_Closed;

            ContextMenu.Items.Add(GroupingMenuItem);
            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(searchAgain);
            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(classFilterMenuItem);
            ContextMenu.Items.Add(enumFilterMenuItem);
            ContextMenu.Items.Add(interfaceFilterMenuItem);
            ContextMenu.Items.Add(structFilterMenuItem);
            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(methodFilterMenuItem);
            ContextMenu.Items.Add(functionFilterMenuItem);
            ContextMenu.Items.Add(propertyFilterMenuItem);
            ContextMenu.Items.Add(variableFilterMenuItem);
            ContextMenu.Items.Add(defineFilterMenuItem);
            ContextMenu.Items.Add(enumValueFilterMenuItem);
            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(applyChanges);
        }

        private void SearchAgain_Click(object sender, RoutedEventArgs e) 
        {
            var filter = new Filter { Type = FilterType.Inactive };
            SetFilter(filter);
            ApplyChanges_ContextMenu_Click(sender, e);
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e) 
        { 
            if (FiltersChanged)
                ApplyChanges_ContextMenu_Click(sender, e);
        }

        public void OnReturn(object selectedItem)
        {
            if (selectedItem == null)
                return;
            var item = selectedItem as XSModelResultItem;
            using var waitCursor = new WithWaitCursor();
            var keyword = item.ResultType == XSModelResultType.Member ? item.MemberName : item.TypeName;
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DocumentHelper.OpenProjectItemAtAsync(item.ContainingFile, item.Line, item.SourceCode, keyword)).FileAndForget($"{FileReference}OnReturn");
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

            NoResultsLabel.Visibility = results.Count < 1 ? Visibility.Visible : Visibility.Collapsed;

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
                    XSModelResultType resultType;
                    if (SearchTerm.StartsWith("..") || SearchTerm.StartsWith("::"))
                    {
                        var currentFile = await DocumentHelper.GetCurrentFileAsync();
                        var caretPosition = await DocumentHelper.GetCaretPositionAsync();
                        (results, resultType) = await XSModel.GetSearchTermMatchesAsync(SearchTerm, GetFilter(), SolutionDirectory, currentFile, caretPosition, 2000, direction, orderBy); //aus DB, max 2000
                    }
                    else
                    {
                        (results, resultType) = await XSModel.GetSearchTermMatchesAsync(SearchTerm, GetFilter(), SolutionDirectory, 2000, direction, orderBy);
                    }

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

                    if (results.Count < 1) 
                    {
                        NoResultsLabel.Visibility =  Visibility.Visible;
                    }
                    else 
                    {
                        NoResultsLabel.Visibility =  Visibility.Collapsed;
                        ResultsDataGrid.SelectedItem = results.FirstOrDefault();
                    }
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

        public void ApplyChanges_ContextMenu_Click(object sender, RoutedEventArgs e)
        {
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await SearchAsync()).FileAndForget($"{FileReference}RefreshResults_ContextMenu_Click");
            FiltersChanged = false;
        }

        private Filter GetFilter()
        {
            var filter = new Filter { Type = ActiveFilterGroup };

            if (ActiveFilterGroup == FilterType.Member)
            {
                filter.MemberFilters = new List<MemberFilter>();
                filter.MemberFilters.AddRange(MemberMenuItems.Where(q => q.Value.IsChecked).Select(q => q.Key));
            }
            else if (ActiveFilterGroup == FilterType.Type)
            {
                filter.TypeFilters = new List<TypeFilter>();
                filter.TypeFilters.AddRange(TypeMenuItems.Where(q => q.Value.IsChecked).Select(q => q.Key));

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
            foreach (var typeMenuItem in TypeMenuItems.Values)
                typeMenuItem.IsChecked = false;

            foreach (var memberMenuItem in MemberMenuItems.Values)
                memberMenuItem.IsChecked = false;

            ActiveFilterGroup = filter.Type;

            if (ActiveFilterGroup == FilterType.Type)
            {
                foreach (var typeFilter in filter.TypeFilters)
                    TypeMenuItems[typeFilter].IsChecked = true;
            }
            else if (ActiveFilterGroup == FilterType.Member)
            {
                foreach (var memberFilter in filter.MemberFilters)
                    MemberMenuItems[memberFilter].IsChecked = true;
            }
        }

        public void TypeFilter_ContextMenu_Checked(object sender, RoutedEventArgs e) 
        {
            FiltersChanged = true;
            ActiveFilterGroup = FilterType.Type;
            foreach (var memberMenuItem in MemberMenuItems.Values)
                memberMenuItem.IsChecked = false;
        }

        public void MemberFilter_ContextMenu_Checked(object sender, RoutedEventArgs e) 
        {
            FiltersChanged = true;
            ActiveFilterGroup = FilterType.Member;
            foreach (var typeMenuItem in TypeMenuItems.Values)
                typeMenuItem.IsChecked = false;
        }

        public void Filter_ContextMenu_Unchecked(object sender, RoutedEventArgs e) 
        {
            FiltersChanged = true;
            if (TypeMenuItems.All(q => !q.Value.IsChecked) && MemberMenuItems.All(q => !q.Value.IsChecked))
                ActiveFilterGroup = FilterType.Inactive;
        }
    }
}
