using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace XSharpPowerTools.Helpers
{
    public class FindNamespaceResultComparer : IResultComparer
    {
        private readonly ListSortDirection Direction;
        private readonly IFindNamespaceCompareHelper CompareHelper;
        private readonly string ColumnIdentifier;

        public string SqlOrderBy { get; }
        public FindNamespaceResultComparer(ListSortDirection direction, DataGridColumn column)
        {
            Direction = direction;
            ColumnIdentifier = column.Header.ToString().Trim();
            if (ColumnIdentifier.Equals("Namespace", StringComparison.OrdinalIgnoreCase))
            {
                CompareHelper = new NamespaceCompareHelper();
                SqlOrderBy = "Namespace";
            }
            else
            {
                CompareHelper = new TypeCompareHelper();
                SqlOrderBy = "Name";
            }
        }

        public int Compare(object x, object y)
        {
            var retVal = 0;
            if (x == null && y == null)
                retVal = 0;
            else if (x == null)
                retVal = -1;
            else if (y == null)
                retVal = 1;
            else if (x is NamespaceResultItem a && y is NamespaceResultItem b)
                retVal = CompareHelper.ExecuteComparison(a, b);

            if (Direction == ListSortDirection.Descending)
                retVal = -retVal;

            return retVal;
        }

        #region CompareHelpers

        private interface IFindNamespaceCompareHelper
        {
            int ExecuteComparison(NamespaceResultItem a, NamespaceResultItem b);
        }

        private class TypeCompareHelper : IFindNamespaceCompareHelper
        {
            public int ExecuteComparison(NamespaceResultItem a, NamespaceResultItem b) =>
                a.TypeName.Length == b.TypeName.Length
                    ? a.TypeName.CompareTo(b.TypeName)
                    : a.TypeName.Length.CompareTo(b.TypeName.Length);
        }

        private class NamespaceCompareHelper : IFindNamespaceCompareHelper
        {
            public int ExecuteComparison(NamespaceResultItem a, NamespaceResultItem b) =>
                a.Namespace.Length == b.Namespace.Length
                    ? a.Namespace.CompareTo(b.Namespace)
                    : a.Namespace.Length.CompareTo(b.Namespace.Length);
        }

        #endregion
    }
}
