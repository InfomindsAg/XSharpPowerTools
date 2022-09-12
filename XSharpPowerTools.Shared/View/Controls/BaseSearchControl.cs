using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using XSharpPowerTools.Helpers;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.View.Controls
{
    public abstract class BaseSearchControl : UserControl, IResultsDataGridParent
    {
        public XSModel XSModel { get; set; }
        protected abstract string FileReference { get; }
        protected bool AllowReturn;
        protected DialogWindow ParentWindow;

        public BaseSearchControl(DialogWindow parentWindow = null) 
        { 
            Community.VisualStudio.Toolkit.Themes.SetUseVsTheme(this, true);
            ParentWindow = parentWindow;
        }

        protected abstract IResultComparer GetComparer(ListSortDirection direction, DataGridColumn column);
        protected abstract Task SearchAsync(ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null);
        public abstract void OnReturn(object selectedItem);

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
