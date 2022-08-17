using System;
using System.Collections.Generic;
using System.Text;

namespace XSharpPowerTools
{
    public enum FilterType
    {
        Type,
        Member,
        Inactive
    }

    public enum MemberFilter
    {
        Method,
        Property,
        Function,
        Variable,
        Define,
        EnumValue
    }

    public enum TypeFilter
    {
        Class,
        Enum,
        Interface,
        Struct
    }

    public class Filter
    {
        public FilterType Type { get; set; }
        public List<TypeFilter> TypeFilters { get; set; }
        public List<MemberFilter> MemberFilters { get; set; }

        public string GetDbTable() =>
            Type == FilterType.Member ? "ProjectMembers" : "ProjectTypes";

        public string GetFilterSql(string memberName = null)
        {
            var sb = new StringBuilder().Append('(');
            if (Type == FilterType.Member) 
            {
                if (memberName.Equals(".ctor", StringComparison.OrdinalIgnoreCase))
                    return "Kind = 3";
                else if (memberName.Equals(".dtor", StringComparison.OrdinalIgnoreCase))
                    return "Kind = 4";

                foreach (var filter in MemberFilters)
                {
                    sb.Append(GetFilterSqlConditions(filter));
                    sb.Append(" OR ");
                }

                if (memberName.Equals("%"))
                    sb.Append("Kind = 3 OR Kind = 4");
                else
                    sb.Length = sb.Length - 4;
            }
            else if (Type == FilterType.Type) 
            {
                foreach (var filter in TypeFilters)
                {
                    sb.Append(GetFilterSqlConditions(filter));
                    sb.Append(" OR ");
                }
                sb.Length = sb.Length - 4;
            }

            return sb.Append(')').ToString();
        }

        private string GetFilterSqlConditions(MemberFilter filter) =>
            filter switch
            {
                MemberFilter.Method => "Kind = 5",
                MemberFilter.Property => "(Kind = 6 OR Kind = 7 OR Kind = 8)",
                MemberFilter.Function => "(Kind = 9 OR Kind = 10)",
                MemberFilter.Variable => "(Kind = 11 OR Kind = 26)",
                MemberFilter.Define => "Kind = 23",
                MemberFilter.EnumValue => "Kind = 19",
                _ => null,
            };

        private string GetFilterSqlConditions(TypeFilter filter) =>
            filter switch
            {
                TypeFilter.Class => "(Kind = 1 AND LOWER(Sourcecode) LIKE '%class%')", //to exclude (Global Scope)-Type
                TypeFilter.Interface => "Kind = 16",
                TypeFilter.Enum => "Kind = 18",
                TypeFilter.Struct => "Kind = 25",
                _ => null,
            };

    }
}
