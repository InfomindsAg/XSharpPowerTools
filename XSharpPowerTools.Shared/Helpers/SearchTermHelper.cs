using System;
using System.Linq;

namespace XSharpPowerTools.Helpers
{
    public static class SearchTermHelper
    {
        public static (string, string) EvaluateSearchTerm(string searchTerm)
        {
            var memberName = string.Empty;
            var typeName = string.Empty;

            searchTerm = searchTerm.Trim().Replace(':', '.');
            searchTerm = searchTerm.Replace(' ', '.');
            searchTerm = searchTerm.Replace('*', '%');
            searchTerm = searchTerm.Replace('\'', '"');
            var keyWords = searchTerm.Split(new[] { '.' }, 2);

            if (searchTerm.EndsWith(".."))
            {
                typeName = searchTerm.Substring(0, searchTerm.Length - 2);
                memberName = ".ctor";
            }
            else if (keyWords.Length > 1)
            {
                if (keyWords[keyWords.Length - 1].Trim().Equals(".c", StringComparison.OrdinalIgnoreCase))
                    memberName = ".ctor";
                else if (keyWords[keyWords.Length - 1].Trim().Equals(".d", StringComparison.OrdinalIgnoreCase))
                    memberName = ".dtor";
                else
                    memberName = keyWords[keyWords.Length - 1];
                typeName = keyWords[keyWords.Length - 2];
            }
            else if (searchTerm.StartsWith("."))
            {
                memberName = searchTerm.Substring(1);
                if (memberName.Equals("ctor", StringComparison.OrdinalIgnoreCase))
                    memberName = ".ctor";
                else if (memberName.Equals("dtor", StringComparison.OrdinalIgnoreCase))
                    memberName = ".dtor";
            }
            else
            {
                typeName = searchTerm;
            }

            if (typeName.Contains('"'))
                typeName = typeName.Replace("\"", "");
            else if (!string.IsNullOrWhiteSpace(typeName) && !typeName.Contains("%"))
                typeName = $"%{typeName}%";

            if (!memberName.Equals(".ctor", StringComparison.OrdinalIgnoreCase) && !memberName.Equals(".dtor", StringComparison.OrdinalIgnoreCase))
            {
                if (memberName.Contains('"'))
                    memberName = memberName.Replace("\"", "");
                else if (!string.IsNullOrWhiteSpace(memberName) && !memberName.Contains("%"))
                    memberName = $"%{memberName}%";
            }

            return (typeName, memberName);
        }

        public static string EvaluateSearchTermLocal(string searchTerm)
        {
            searchTerm = searchTerm.Trim().Replace(':', '.');
            searchTerm = searchTerm.Replace(' ', '.');
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
