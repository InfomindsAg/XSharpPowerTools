﻿using Microsoft.VisualStudio.Shell;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using XSharpPowerTools.Helpers;
using DataGrid = System.Windows.Controls.DataGrid;

namespace XSharpPowerTools.View.Controls
{
    /// <summary>
    /// Interaction logic for ResultsDataGrid.xaml
    /// </summary>
    public partial class ResultsDataGrid : DataGrid
    {
        public new IResultsDataGridParent Parent { private get; set; }

        public ResultsDataGrid() =>
            InitializeComponent();

        protected void ResultsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            Parent?.OnReturn((sender as DataGridRow).Item);

        public void SelectNext()
        {
            object currentItem = SelectedItem;
            if (currentItem == null)
                return;
            int currentIndex = Items.IndexOf(currentItem);
            if (currentIndex >= Items.Count - 1)
                return;
            SelectedItem = Items.GetItemAt(currentIndex + 1);
            UpdateLayout();
            ScrollIntoView(SelectedItem);
        }

        public void SelectPrevious()
        {
            object currentItem = SelectedItem;
            if (currentItem == null)
                return;
            int currentIndex = Items.IndexOf(currentItem);
            if (currentIndex < 1)
                return;
            SelectedItem = Items.GetItemAt(currentIndex - 1);
            UpdateLayout();
            ScrollIntoView(SelectedItem);
        }

        protected void SortHandler(object sender, DataGridSortingEventArgs e) =>
            Parent?.OnSort(this, e);

        protected void CopyRowClipboardContentHandler(object sender, DataGridRowClipboardEventArgs e)
        {
            var column = Columns[CurrentCell.Column.DisplayIndex];
            var cellContent = e.ClipboardRowContent.Where(item => item.Column == column).First();
            e.ClipboardRowContent.Clear();
            e.ClipboardRowContent.Add(cellContent);
        }
    }
}
