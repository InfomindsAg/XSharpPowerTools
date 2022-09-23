using System;
using System.Collections;
using System.ComponentModel;
using System.Windows.Controls;

namespace XSharpPowerTools.Helpers
{
    public class XSModelResultComparer : IComparer
    {
        private readonly ListSortDirection Direction;
        private readonly XSModelResultType ResultType;
        private readonly ICompareHelper CompareHelper;
        private readonly string ColumnIdentifier;

        public string SqlOrderBy { get; }

        public XSModelResultComparer(ListSortDirection direction, DataGridColumn column) : this(direction, column, XSModelResultType.Type)
        { }

        public XSModelResultComparer(ListSortDirection direction, DataGridColumn column, XSModelResultType resultType)
        {
            Direction = direction;
            ResultType = resultType;

            ColumnIdentifier = column.SortMemberPath.Trim();
            if (ColumnIdentifier.Equals("TypeName", StringComparison.OrdinalIgnoreCase))
            {
                CompareHelper = new TypeCompareHelper();
                SqlOrderBy = ResultType == XSModelResultType.Type
                    ? "Name"
                    : "TypeName";
            }
            else if (ColumnIdentifier.Equals("MemberName", StringComparison.OrdinalIgnoreCase))
            {
                CompareHelper = new MemberCompareHelper();
                SqlOrderBy = "Name";
            }
            else if (ColumnIdentifier.Equals("KindName", StringComparison.OrdinalIgnoreCase))
            {
                CompareHelper = new KindCompareHelper();
                SqlOrderBy = "Kind";
            }
            else if (ColumnIdentifier.Equals("RelativePath", StringComparison.OrdinalIgnoreCase))
            {
                CompareHelper = new FileCompareHelper();
                SqlOrderBy = "FileName";
            }
            else if (ColumnIdentifier.Equals("Namespace", StringComparison.OrdinalIgnoreCase))
            {
                CompareHelper = new NamespaceCompareHelper();
                SqlOrderBy = "Namespace";
            }
            else
            {
                CompareHelper = ResultType == XSModelResultType.Type
                    ? new TypeCompareHelper()
                    : new MemberCompareHelper();
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
            else if (x is XSModelResultItem a && y is XSModelResultItem b)
                retVal = CompareHelper.ExecuteComparison(a, b);

            if (Direction == ListSortDirection.Descending)
                retVal = -retVal;

            return retVal;
        }

        #region CompareHelpers

        private interface ICompareHelper
        {
            int ExecuteComparison(XSModelResultItem a, XSModelResultItem b);
        }

        private class TypeCompareHelper : ICompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.TypeName.CompareTo(b.TypeName);
        }

        private class MemberCompareHelper : ICompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.MemberName.CompareTo(b.MemberName);
        }

        private class KindCompareHelper : ICompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.Kind.CompareTo(b.Kind);
        }

        private class FileCompareHelper : ICompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.ContainingFile.CompareTo(b.ContainingFile);
        }

        private class NamespaceCompareHelper : ICompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.Namespace.CompareTo(b.Namespace);
        }

        #endregion
    }
}
