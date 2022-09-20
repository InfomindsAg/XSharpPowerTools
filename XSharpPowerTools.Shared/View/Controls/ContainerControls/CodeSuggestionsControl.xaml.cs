using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XSharpPowerTools.Helpers;
using XSharpPowerTools.View.Windows;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.View.Controls
{
    /// <summary>
    /// Interaction logic for CodeSuggestionsControl.xaml
    /// </summary>
    public partial class CodeSuggestionsControl : CodeSearchControl
    {
        protected override string FileReference => "vs/XSharpPowerTools/CodeSuggestions/";

        volatile bool SearchActive = false;
        volatile bool ReDoSearch = false;

        protected override SearchTextBox SearchTextBox => _searchTextBox;
        protected override ResultsDataGrid ResultsDataGrid => _resultsDataGrid;
        protected override MemberFilterControl MemberFilterGroup => _memberFilterGroup;
        protected override TypeFilterControl TypeFilterGroup => _typeFilterGroup;

        private XSModelResultItem SelectedTypeInfo = null; 

        public CodeSuggestionsControl(DialogWindow parentWindow) : base(parentWindow)
        {
            InitializeComponent();
            ResultsDataGrid.Parent = this;

            SearchTextBox.WhenTextChanged
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Subscribe(_ => OnTextChanged());

            ActiveFilterGroup = FilterType.Inactive;
        }

        protected override void SetTableColumns(XSModelResultType resultType)
        {
            var typeSpecificColumnsVisibility = resultType == XSModelResultType.Type
                ? Visibility.Visible
                : Visibility.Collapsed;

            ResultsDataGrid.Columns[0].Visibility = typeSpecificColumnsVisibility;
            base.SetTableColumns(resultType); 
        }

        protected override async Task SearchAsync(ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null)
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

                    var (results, resultType) = await XSModel.GetCodeSuggestionsAsync(searchTerm, GetFilter(), direction, orderBy, SelectedTypeInfo, currentFile, caretPosition);

                    ResultsDataGrid.ItemsSource = results;
                    ResultsDataGrid.SelectedItem = results.FirstOrDefault();
                    SetTableColumns(resultType);
                    DisplayedResultType = resultType;

                    NoResultsLabel.Visibility = results.Count < 1 ? Visibility.Visible : Visibility.Collapsed;

                } while (ReDoSearch);
            }
            finally
            {
                SearchActive = false;
                AllowReturn = true;
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e) =>
            HelpControl.Visibility = HelpControl.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

        protected void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AllowReturn = false;
            var separators = new[] { '.', ':' };
            if (SelectedTypeInfo != null
                && (!SearchTextBox.Text.Trim().StartsWith(SelectedTypeInfo.TypeName) 
                    || SearchTextBox.Text.Trim().IndexOfAny(separators) != SelectedTypeInfo.TypeName.Length))
            {
                SelectedTypeInfo = null;
            }

            if ((SearchTextBox.Text.StartsWith("..") 
                    || SearchTextBox.Text.StartsWith("::") 
                    || (separators.Any(SearchTextBox.Text.Contains) && !(SearchTextBox.Text.StartsWith(".") || SearchTextBox.Text.StartsWith(":")))))
            {
                if (MemberFilterGroup.Mode == MemberFilterControl.DisplayMode.GlobalScope)
                    ResetFilters(MemberFilterControl.DisplayMode.ContainedInType);
            }
            else if (MemberFilterGroup.Mode == MemberFilterControl.DisplayMode.ContainedInType)
            {
                ResetFilters(MemberFilterControl.DisplayMode.GlobalScope);
            }
        }

        private void ResetFilters(MemberFilterControl.DisplayMode displayMode) 
        {
            var typefiltersVisibility = displayMode == MemberFilterControl.DisplayMode.ContainedInType ? Visibility.Collapsed : Visibility.Visible;
            MemberFilterGroup.Mode = displayMode;
            FilterSeparator.Visibility = typefiltersVisibility;
            TypeFilterGroup.Visibility = typefiltersVisibility;
            ActiveFilterGroup = FilterType.Inactive;
        }

        public override void OnReturn(object selectedItem)
        {
            if (AllowReturn)
            {
                var item = selectedItem as XSModelResultItem;
                if (Keyboard.Modifiers == ModifierKeys.Control && DisplayedResultType == XSModelResultType.Type)
                {
                    SearchTextBox.Text = $"{item.TypeName}.";
                    SelectedTypeInfo = item;
                    ResetFilters(MemberFilterControl.DisplayMode.ContainedInType);
                    SearchTextBox.CaretIndex = int.MaxValue;
                    return;
                }

                var codeSuggestion = DisplayedResultType == XSModelResultType.Type ? item.TypeName : item.MemberName;

                XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DocumentHelper.InsertCodeSuggestionAsync(codeSuggestion)).FileAndForget($"{FileReference}OnReturn");
            }
        }
    }
}
