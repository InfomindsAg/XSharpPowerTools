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
            var keyWords = searchTerm.Split(new[] { '.' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (keyWords.Length > 1)
            {
                if (keyWords[keyWords.Length - 1].Equals("ctor", StringComparison.OrdinalIgnoreCase))
                    memberName = ".ctor";
                else if (keyWords[keyWords.Length - 1].Equals("dtor", StringComparison.OrdinalIgnoreCase))
                    memberName = ".dtor";
                else if (keyWords[keyWords.Length - 1].Equals("c", StringComparison.OrdinalIgnoreCase) && searchTerm.Substring(searchTerm.LastIndexOf(keyWords[keyWords.Length - 1]) - 2, 2) == "..")
                    memberName = ".ctor";
                else if (keyWords[keyWords.Length - 1].Equals("d", StringComparison.OrdinalIgnoreCase) && searchTerm.Substring(searchTerm.LastIndexOf(keyWords[keyWords.Length - 1]) - 2, 2) == "..")
                    memberName = ".dtor";
                else
                    memberName = keyWords[keyWords.Length - 1];
                className = keyWords[keyWords.Length - 2];
            }
            else if (searchTerm.StartsWith("."))
            {
                memberName = searchTerm.Substring(searchTerm.TakeWhile(q => q == '.').Count());
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

            if (memberName.Contains('"'))
                memberName = memberName.Replace("\"", "");
            else if (!string.IsNullOrWhiteSpace(memberName) && !memberName.Contains("%"))
                memberName = $"%{memberName}%";

            return (className, memberName);
        }
    }
}
