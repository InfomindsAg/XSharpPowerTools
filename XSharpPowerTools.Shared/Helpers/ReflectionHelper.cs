using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Media;

namespace XSharpPowerTools.Helpers
{
    public static class ReflectionHelper
    {
        private class MemberComparer : IEqualityComparer<MemberInfo>
        {
            public bool Equals(MemberInfo x, MemberInfo y) =>
                x?.GetType() == y?.GetType() 
                && (x?.Name?.Equals(y?.Name, StringComparison.OrdinalIgnoreCase) ?? false); 

            public int GetHashCode(MemberInfo obj) => 
                obj.GetHashCode();
        }

        private static bool ContainsAllInOrder(this string stringToCheck, string[] keywords) 
        {
            if (string.IsNullOrEmpty(stringToCheck) || keywords == null || keywords.Length == 0)
                return false;

            var searchIndex = 0;
            foreach (var keyword in keywords) 
            { 
                var index = stringToCheck.IndexOf(keyword, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;
                searchIndex = index + keyword.Length;
            }
            return true;
        }

        public static string GetExternalMembersSql(XSModelResultItem assemblyTypeInfo, string[] memberNameFragments = null, bool includeProtected = false)
        {
            if (!File.Exists(assemblyTypeInfo.ContainingFile))
                return null;
            
            var dummyDomain = AppDomain.CreateDomain("dummy"); //needed to unload assembly after use
            var assemblyName = new AssemblyName { CodeBase = assemblyTypeInfo.ContainingFile };

            Assembly assembly;
            try 
            {
                assembly = dummyDomain.Load(assemblyName);
            }
            catch (FileNotFoundException) 
            {
                return null;
            }

            if (assembly == null)
                return null;

            var type = assembly.DefinedTypes?.FirstOrDefault(q => q.Name == assemblyTypeInfo.TypeName && q.Namespace == assemblyTypeInfo.Namespace);
            var members = type?.DeclaredMembers;

            AppDomain.Unload(dummyDomain);

            if (members == null)
                return null;

            if (memberNameFragments != null && memberNameFragments.Length > 0)
                members = members.Where(q => q.Name.ContainsAllInOrder(memberNameFragments));

            var sb = new StringBuilder();
            var i = 0;
            foreach (var member in members.Distinct(new MemberComparer()))
            {
                int kind;
                if (member is MethodInfo methodInfo && !methodInfo.IsSpecialName && (methodInfo.IsPublic || (includeProtected && methodInfo.IsFamily)))
                    kind = 5;
                else if (member is PropertyInfo propertyInfo && !propertyInfo.IsSpecialName && propertyInfo.GetAccessors().Any(q => q.IsPublic || (includeProtected && q.IsFamily)))
                    kind = 8;
                else if (member is FieldInfo fieldInfo && !fieldInfo.IsSpecialName && (fieldInfo.IsPublic || (includeProtected && fieldInfo.IsFamily)))
                    kind = 11;
                else
                    continue;

                sb.AppendLine("UNION");
                sb.AppendLine($"SELECT '{member.Name}', '', 0, '', '{kind}', '{assemblyTypeInfo.Namespace}', '', '', true, '{assemblyTypeInfo.TypeName}', {int.MaxValue - i} AS Id");
                i++;
            }
            return sb.ToString();
        }
    }
}
