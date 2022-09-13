using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace XSharpPowerTools.Helpers
{
    public class CodeBrowserResultComparer : IResultComparer
    {
        private readonly ListSortDirection Direction;
        private readonly XSModelResultType ResultType;
        private readonly ICodeBrowserCompareHelper CompareHelper;
        private readonly string ColumnIdentifier;

        public string SqlOrderBy { get; }

        public CodeBrowserResultComparer(ListSortDirection direction, DataGridColumn column, XSModelResultType resultType)
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

        private interface ICodeBrowserCompareHelper
        {
            int ExecuteComparison(XSModelResultItem a, XSModelResultItem b);
        }

        private class TypeCompareHelper : ICodeBrowserCompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.TypeName.CompareTo(b.TypeName);
        }

        private class MemberCompareHelper : ICodeBrowserCompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.MemberName.CompareTo(b.MemberName);
        }

        private class KindCompareHelper : ICodeBrowserCompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.Kind.CompareTo(b.Kind);
        }

        private class FileCompareHelper : ICodeBrowserCompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.ContainingFile.CompareTo(b.ContainingFile);
        }

        #endregion
    }
}
