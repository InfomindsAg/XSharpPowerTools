using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using XSharpPowerTools.Helpers;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.View.Controls
{
    public abstract class BaseSearchControl : UserControl
    {
        public XSModel XSModel { get; set; }

        protected abstract ResultsDataGrid ResultsDataGrid { get; }
        protected abstract string FileReference { get; }

        protected DialogWindow ParentWindow;

        public BaseSearchControl(DialogWindow parentWindow = null)
        {
            Community.VisualStudio.Toolkit.Themes.SetUseVsTheme(this, true);
            ParentWindow = parentWindow;
            PreviewKeyDown += BaseSearchControl_PreviewKeyDown;
        }

        protected abstract XSModelResultComparer GetComparer(ListSortDirection direction, DataGridColumn column);
        protected abstract Task SearchAsync(ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null);
        public abstract void OnReturn(object selectedItem);

        private void BaseSearchControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
                OnReturn(ResultsDataGrid?.SelectedItem);
            else if (e.Key == Key.Down || e.Key == Key.PageDown)
                ResultsDataGrid.SelectNext();
            else if (e.Key == Key.Up || e.Key == Key.PageUp)
                ResultsDataGrid.SelectPrevious();
        }

        public virtual void OnSort(ResultsDataGrid sender, DataGridSortingEventArgs e)
        {
            var column = e.Column;

            var direction = (column.SortDirection != ListSortDirection.Ascending) ? ListSortDirection.Ascending : ListSortDirection.Descending;
            var lcv = (ListCollectionView)CollectionViewSource.GetDefaultView(sender.ItemsSource);
            var comparer = GetComparer(direction, column);

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

        protected void Close() =>
            ParentWindow?.Close();
    }
}
