using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace XSharpPowerTools.Helpers
{
    

    public class CodeBrowserResultComparer : IComparer
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

            ColumnIdentifier = column.Header.ToString().Trim();
            if (ColumnIdentifier.Equals("Type", StringComparison.OrdinalIgnoreCase)) 
            {
                CompareHelper = new TypeCompareHelper();
                SqlOrderBy = ResultType == XSModelResultType.Type
                    ? "Name"
                    : "TypeName";
            }
            else if (ColumnIdentifier.Equals("Member", StringComparison.OrdinalIgnoreCase))
            {
                CompareHelper = new MemberCompareHelper();
                SqlOrderBy = "Name";
            }
            else if (ColumnIdentifier.Equals("Kind", StringComparison.OrdinalIgnoreCase))
            {
                CompareHelper = new KindCompareHelper();
                SqlOrderBy = "Kind";
            }
            else if (ColumnIdentifier.Equals("File", StringComparison.OrdinalIgnoreCase))
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
                a.TypeName.Length == b.TypeName.Length
                    ? a.TypeName.CompareTo(b.TypeName)
                    : a.TypeName.Length.CompareTo(b.TypeName.Length);
        }

        private class MemberCompareHelper : ICodeBrowserCompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) =>
                a.MemberName.Length == b.MemberName.Length
                    ? a.MemberName.CompareTo(b.MemberName)
                    : a.MemberName.Length.CompareTo(b.MemberName.Length);
        }

        private class KindCompareHelper : ICodeBrowserCompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) => 
                a.Kind.CompareTo(b.Kind);
        }

        private class FileCompareHelper : ICodeBrowserCompareHelper
        {
            public int ExecuteComparison(XSModelResultItem a, XSModelResultItem b) 
            {
                var aLvl = a.ContainingFile.Count(q => q == Path.DirectorySeparatorChar);
                var bLvl = b.ContainingFile.Count(q => q == Path.DirectorySeparatorChar);
                return aLvl == bLvl
                    ? a.ContainingFile.Length.CompareTo(b.ContainingFile.Length)
                    : aLvl.CompareTo(bLvl);
            }
        }

        #endregion
    }
}
