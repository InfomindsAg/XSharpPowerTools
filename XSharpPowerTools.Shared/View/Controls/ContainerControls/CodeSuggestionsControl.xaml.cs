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

        public CodeSuggestionsControl(DialogWindow parentWindow) : base(parentWindow)
        {
            InitializeComponent();
            ResultsDataGrid.Parent = this;

            SearchTextBox.WhenTextChanged
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Subscribe(_ => OnTextChanged());

            ActiveFilterGroup = FilterType.Inactive;
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

            //using var waitCursor = new WithWaitCursor();
            //SearchActive = true;
            //try
            //{
            //    do
            //    {
            //        var searchTerm = SearchTextBox.Text.Trim();
            //        ReDoSearch = false;

            //        string currentFile;
            //        int caretPosition;
            //        if (searchTerm.StartsWith("..") || searchTerm.StartsWith("::"))
            //        {
            //            currentFile = await DocumentHelper.GetCurrentFileAsync();
            //            caretPosition = await DocumentHelper.GetCaretPositionAsync();
            //        }
            //        else
            //        {
            //            currentFile = null;
            //            caretPosition = -1;
            //        }

            //        var (results, resultType) = await XSModel.GetSearchTermMatchesAsync(searchTerm, GetFilter(), SolutionDirectory, currentFile, caretPosition, direction, orderBy);

            //        ResultsDataGrid.ItemsSource = results;
            //        ResultsDataGrid.SelectedItem = results.FirstOrDefault();
            //        SetTableColumns(resultType);
            //        DisplayedResultType = resultType;
            //        LastSearchTerm = searchTerm;

            //        NoResultsLabel.Visibility = results.Count < 1 ? Visibility.Visible : Visibility.Collapsed;

            //    } while (ReDoSearch);
            //}
            //finally
            //{
            //    SearchActive = false;
            //    AllowReturn = true;
            //}
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e) =>
            HelpControl.Visibility = HelpControl.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

        protected void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            AllowReturn = false;
            var separators = new[] { '.', ':' };
            if (separators.Any(SearchTextBox.Text.Contains))
            {
                if (MemberFilterGroup.Mode == MemberFilterControl.DisplayMode.GlobalScope) 
                {
                    MemberFilterGroup.Mode = MemberFilterControl.DisplayMode.ContainedInType;
                    FilterSeparator.Visibility = Visibility.Collapsed;
                    TypeFilterGroup.Visibility = Visibility.Collapsed;
                    //FilterContainer.UpdateLayout();
                }
            }
            else if (MemberFilterGroup.Mode == MemberFilterControl.DisplayMode.ContainedInType) 
            {
                MemberFilterGroup.Mode = MemberFilterControl.DisplayMode.GlobalScope;
                FilterSeparator.Visibility = Visibility.Visible;
                TypeFilterGroup.Visibility = Visibility.Visible;
                //FilterContainer.UpdateLayout();
            }
        }

        public override void OnReturn(object selectedItem)
        {
            if (AllowReturn)
            {
                //var item = selectedItem as XSModelResultItem;
                //if (Keyboard.Modifiers == ModifierKeys.Control)
                //    SaveResultsToToolWindow();
                //else
                //    XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await OpenItemAsync(item)).FileAndForget($"{FileReference}OnReturn");
            }
        }
    }
}
