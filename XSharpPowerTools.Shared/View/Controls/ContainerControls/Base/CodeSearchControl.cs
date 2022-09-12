using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XSharpPowerTools.View.Controls
{
    public abstract class CodeSearchControl : DialogSearchControl
    {
        protected FilterType ActiveFilterGroup;
        protected XSModelResultType DisplayedResultType;

        protected abstract MemberFilterControl MemberFilterGroup { get; }
        protected abstract TypeFilterControl TypeFilterGroup { get; }

        public CodeSearchControl(DialogWindow parentWindow) : base(parentWindow)
        {
            PreviewKeyDown += CodeSearchControl_PreviewKeyDown;
            PreviewKeyUp += CodeSearchControl_PreviewKeyUp;
            LostFocus += CodeSearchControl_LostFocus;
        }

        private void CodeSearchControl_LostFocus(object sender, RoutedEventArgs e)
        {
            MemberFilterGroup?.HidePopups();
            TypeFilterGroup?.HidePopups();
        }

        private void CodeSearchControl_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                MemberFilterGroup?.HidePopups();
                TypeFilterGroup?.HidePopups();
            }
        }

        private void CodeSearchControl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) 
        {
            if (MemberFilterGroup == null || TypeFilterGroup == null)
            {
                return;
            }
            else if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                MemberFilterGroup.ShowPopups();
                TypeFilterGroup.ShowPopups();
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var isMemberHotKey = MemberFilterGroup.TryHandleHotKey(e.Key);
                var isTypeHotKey = TypeFilterGroup.TryHandleHotKey(e.Key);

                if (isMemberHotKey && isTypeHotKey)
                    return;

                if (isMemberHotKey)
                {
                    FilterGroup_Click(MemberFilterGroup, null);
                    e.Handled = true;
                }
                else if (isTypeHotKey)
                {
                    FilterGroup_Click(TypeFilterGroup, null);
                    e.Handled = true;
                }
            }
        }

        protected void SetTableColumns(XSModelResultType resultType)
        {
            if (ResultsDataGrid == null)
                return;

            var memberSpecificColumnsVisibility = resultType == XSModelResultType.Type
                ? Visibility.Collapsed
                : Visibility.Visible;

            ResultsDataGrid.Columns[1].Visibility = memberSpecificColumnsVisibility;
            ResultsDataGrid.Columns[2].Visibility = memberSpecificColumnsVisibility;

            ResultsDataGrid.Columns[0].Width = 0;
            ResultsDataGrid.Columns[1].Width = 0;
            ResultsDataGrid.Columns[2].Width = 0;
            ResultsDataGrid.Columns[3].Width = 0;
            ResultsDataGrid.Columns[4].Width = 0;
            ResultsDataGrid.UpdateLayout();
            ResultsDataGrid.Columns[0].Width = new DataGridLength(4, DataGridLengthUnitType.Star);
            ResultsDataGrid.Columns[1].Width = new DataGridLength(4, DataGridLengthUnitType.Star);
            ResultsDataGrid.Columns[2].Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
            ResultsDataGrid.Columns[3].Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);
            ResultsDataGrid.Columns[4].Width = new DataGridLength(7, DataGridLengthUnitType.Star);
        }

        protected virtual Filter GetFilter()
        {
            var filter = new Filter { Type = ActiveFilterGroup };

            if (ActiveFilterGroup == FilterType.Inactive || (MemberFilterGroup == null || TypeFilterGroup == null))
            {
                filter.MemberFilters = MemberFilterGroup.GetAllFilters();
                filter.TypeFilters = TypeFilterGroup.GetAllFilters();
            }
            else if (ActiveFilterGroup == FilterType.Member)
            {
                filter.MemberFilters = MemberFilterGroup.GetFilters();
            }
            else if (ActiveFilterGroup == FilterType.Type)
            {
                filter.TypeFilters = TypeFilterGroup.GetFilters();
            }

            return filter;
        }

        protected void FilterGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender == MemberFilterGroup)
            {
                TypeFilterGroup.Deactivate();
                ActiveFilterGroup = MemberFilterGroup.IsActive() ? FilterType.Member : FilterType.Inactive;
            }
            else if (sender == TypeFilterGroup)
            {
                MemberFilterGroup.Deactivate();
                ActiveFilterGroup = TypeFilterGroup.IsActive() ? FilterType.Type : FilterType.Inactive;
            }
            else
            {
                return;
            }

            SearchTextBox.Focus();
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}FilterButton_Click");
        }

        protected void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            MemberFilterGroup.Deactivate();
            TypeFilterGroup.Deactivate();
            ActiveFilterGroup = FilterType.Inactive;

            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}RefreshButton_Click");
        }

    }
}
