using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;

namespace XSharpPowerTools.View.Controls
{
    public abstract class BaseSearchControl : UserControl, IResultsDataGridParent
    {
        public XSModel XSModel { get; set; }
        public abstract string SearchTerm { set; }
        protected bool AllowReturn;
        protected DialogWindow ParentWindow;

        public BaseSearchControl(DialogWindow parentWindow) 
        { 
            Community.VisualStudio.Toolkit.Themes.SetUseVsTheme(this, true);
            ParentWindow = parentWindow;
        }

        protected abstract void OnTextChanged();
        public abstract void OnReturn(object selectedItem);
        public abstract void OnSort(ResultsDataGrid sender, DataGridSortingEventArgs e);

        protected void Close() => 
            ParentWindow?.Close();
    }
}
