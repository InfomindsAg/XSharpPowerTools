using System.Windows.Controls;

namespace XSharpPowerTools.View.Controls
{
    public interface IResultsDataGridParent
    {
        public void OnReturn(object selectedItem);
        public void OnSort(ResultsDataGrid sender, DataGridSortingEventArgs e);
    }
}
