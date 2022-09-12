using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using XSharpPowerTools.Helpers;
using XSharpPowerTools.View.Windows;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.View.Controls
{
    /// <summary>
    /// Interaction logic for FindNamespaceControl.xaml
    /// </summary>
    public partial class FindNamespaceControl : DialogSearchControl
    {
        protected override string FileReference => "vs/XSharpPowerTools/FindNamespace/";
        volatile bool SearchActive = false;
        volatile bool ReDoSearch = false;

        protected override SearchTextBox SearchTextBox => _searchTextBox;
        protected override ResultsDataGrid ResultsDataGrid => _resultsDataGrid;

        public FindNamespaceControl(DialogWindow parentWindow) : base(parentWindow)
        {
            InitializeComponent();
            InitializeSearchTextBox();
            ResultsDataGrid.Parent = this;
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
                    var results = await XSModel.GetContainingNamespaceAsync(searchTerm, direction, orderBy);
                    ResultsDataGrid.ItemsSource = results;
                    ResultsDataGrid.SelectedItem = results.FirstOrDefault();

                    NoResultsLabel.Visibility = results.Count < 1 ? Visibility.Visible : Visibility.Collapsed;

                } while (ReDoSearch);
            }
            finally
            {
                SearchActive = false;
                AllowReturn = true;
            };
        }

        private async Task InsertUsingAsync(NamespaceResultItem item)
        {
            if (item == null)
                return;
            await DocumentHelper.InsertUsingAsync(item.Namespace, item.TypeName, XSModel);
            Close();
        }

        private async Task InsertNamespaceReferenceAsync(NamespaceResultItem item)
        {
            if (item == null)
                return;
            await DocumentHelper.InsertNamespaceReferenceAsync(item.Namespace, item.TypeName);
            Close();
        }

        public override void OnReturn(object selectedItem)
        {
            if (AllowReturn)
            {
                var item = selectedItem as NamespaceResultItem;
                if (Keyboard.Modifiers == ModifierKeys.Control)
                    XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await InsertNamespaceReferenceAsync(item)).FileAndForget($"{FileReference}OnReturn");
                else
                    XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await InsertUsingAsync(item)).FileAndForget($"{FileReference}OnReturn");
            }
        }

        protected override IResultComparer GetComparer(ListSortDirection direction, DataGridColumn column) =>
            new FindNamespaceResultComparer(direction, column);
    }
}
