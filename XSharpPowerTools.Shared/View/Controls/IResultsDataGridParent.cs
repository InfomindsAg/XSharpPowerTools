using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;

namespace XSharpPowerTools.View.Controls
{
    public interface IResultsDataGridParent
    {
        public void OnReturn(object selectedItem);
        public Task OnSort(ResultsDataGrid sender, DataGridSortingEventArgs e);
    }
}
