using System;
using System.Linq;

namespace XSharpPowerTools.Helpers
{
    public static class SearchTermHelper
    {
        public static (string, string) EvaluateSearchTerm(string searchTerm)
        {
            var memberName = string.Empty;
            var className = string.Empty;

            searchTerm = searchTerm.Trim().Replace(':', '.');
            searchTerm = searchTerm.Replace(' ', '.');
            searchTerm = searchTerm.Replace('*', '%');
            searchTerm = searchTerm.Replace('\'', '"');
            var keyWords = searchTerm.Split(new[] { '.' }, 2);
            if (keyWords.Length > 1)
            {
                if (keyWords[keyWords.Length - 1].Trim().Equals(".c", StringComparison.OrdinalIgnoreCase))
                    memberName = ".ctor";
                else if (keyWords[keyWords.Length - 1].Trim().Equals(".d", StringComparison.OrdinalIgnoreCase))
                    memberName = ".dtor";
                else
                    memberName = keyWords[keyWords.Length - 1];
                className = keyWords[keyWords.Length - 2];
            }
            else if (searchTerm.StartsWith("."))
            {
                memberName = searchTerm.Substring(1);
                if (memberName.Equals("ctor", StringComparison.OrdinalIgnoreCase))
                    memberName = ".ctor";
                else if (memberName.Equals("dtor", StringComparison.OrdinalIgnoreCase))
                    memberName = ".dtor";
            }
            else if (searchTerm.EndsWith("..")) 
            { 
                className = searchTerm.Substring(0, searchTerm.Length - 2);
                memberName = ".ctor";
            }
            else
            {
                className = searchTerm;
            }

            if (className.Contains('"'))
                className = className.Replace("\"", "");
            else if (!string.IsNullOrWhiteSpace(className) && !className.Contains("%"))
                className = $"%{className}%";

            if (!memberName.Equals(".ctor", StringComparison.OrdinalIgnoreCase) && !memberName.Equals(".dtor", StringComparison.OrdinalIgnoreCase))
            {
                if (memberName.Contains('"'))
                    memberName = memberName.Replace("\"", "");
                else if (!string.IsNullOrWhiteSpace(memberName) && !memberName.Contains("%"))
                    memberName = $"%{memberName}%";
            }

            return (className, memberName);
        }

        public static string EvaluateSearchTermLocal(string searchTerm)
        {
            searchTerm = searchTerm.Trim().Replace(':', '.');
            searchTerm = searchTerm.Replace(' ', '.');
            searchTerm = searchTerm.Replace('*', '%');
            searchTerm = searchTerm.Replace('\'', '"');

            var memberName = searchTerm.Substring(searchTerm.TakeWhile(q => q == '.').Count());
            if (memberName.Equals("ctor", StringComparison.OrdinalIgnoreCase))
                memberName = ".ctor";
            else if (memberName.Equals("dtor", StringComparison.OrdinalIgnoreCase))
                memberName = ".dtor";
            else if (searchTerm.Equals("..."))
                memberName = ".ctor";

            return memberName;
        }
    }
}
