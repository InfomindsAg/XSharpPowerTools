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
    /// Interaction logic for CodeBrowserControl.xaml
    /// </summary>
    public partial class CodeBrowserControl : CodeSearchControl
    {
        protected override string FileReference => "vs/XSharpPowerTools/CodeBrowser/";
        readonly string SolutionDirectory;

        string LastSearchTerm;
        volatile bool SearchActive = false;
        volatile bool ReDoSearch = false;

        protected override SearchTextBox SearchTextBox => _searchTextBox;
        protected override ResultsDataGrid ResultsDataGrid => _resultsDataGrid;
        protected override MemberFilterControl MemberFilterGroup => _memberFilterGroup;
        protected override TypeFilterControl TypeFilterGroup => _typeFilterGroup;

        public CodeBrowserControl(string solutionDirectory, DialogWindow parentWindow) : base(parentWindow)
        {
            InitializeComponent();
            SolutionDirectory = solutionDirectory;
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

        private void HelpButton_Click(object sender, RoutedEventArgs e) =>
            HelpControl.Visibility = HelpControl.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;

        public override void OnReturn(object selectedItem)
        {
            if (AllowReturn)
            {
                var item = selectedItem as XSModelResultItem;
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    SaveResultsToToolWindow();
                else
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

        protected override IResultComparer GetComparer(ListSortDirection direction, DataGridColumn column) =>
            new CodeBrowserResultComparer(direction, column, DisplayedResultType);
    }
}
