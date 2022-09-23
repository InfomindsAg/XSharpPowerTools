using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XSharpPowerTools.Helpers;

namespace XSharpPowerTools
{
    public enum XSModelResultType
    {
        Type,
        Member
    }

    public class XSModelResultItem
    {
        private static readonly List<int> KindsWithParams = new() { 3, 5, 9, 10 };

        public string SolutionDirectory { get; set; }
        public XSModelResultType ResultType { get; set; }
        public string MemberName { get; set; }
        public string ContainingFile { get; set; }
        public string BaseTypeName { get; set; }
        public string Namespace { get; set; }
        public string Project { get; set; }
        public string SourceCode { get; set; }
        public int Line { get; set; }
        public int Kind { get; set; }
        public bool IsExternal { get; set; }

        private string _typeName;
        public string TypeName
        {
            get => _typeName;
            set => _typeName = "(Global Scope)".Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase) ? string.Empty : value?.Trim();
        }

        public string FullName
        {
            get
            {
                if (ResultType != XSModelResultType.Type || string.IsNullOrWhiteSpace(TypeName))
                    return string.Empty;

                return string.IsNullOrWhiteSpace(Namespace)
                    ? TypeName
                    : $"{Namespace}.{TypeName}";
            }
        }

        public string RelativePath =>
            string.IsNullOrWhiteSpace(SolutionDirectory) || !ContainingFile.StartsWith(SolutionDirectory) ? ContainingFile : ContainingFile.Substring(SolutionDirectory.Length + 1);

        public string ParametersCount
        {
            get
            {
                if (!KindsWithParams.Contains(Kind) || !SourceCode.Contains("(") || !SourceCode.Contains(")"))
                    return string.Empty;

                var paramDeclaration = SourceCode.Substring(SourceCode.IndexOf("(") + 1, SourceCode.IndexOf(")") - SourceCode.IndexOf("(") - 1);

                var paramCount = string.IsNullOrWhiteSpace(paramDeclaration) ? 0 : 1;
                paramCount += paramDeclaration.Count(q => q == ',');

                return paramCount.ToString();
            }
        }

        public string SourceCodeDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(SourceCode))
                    return string.Empty;

                var sourceCodeLines = SourceCode.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                return sourceCodeLines.Length > 1
                    ? string.Join(" ", sourceCodeLines.Select(q => q.Trim()))
                    : SourceCode.Trim();
            }
        }

        public string KindName => Kind switch
        {
            1 => "Class",
            3 => "Constructor",
            4 => "Destructor",
            5 => "Method",
            6 => "Access",
            7 => "Assign",
            8 => "Property",
            9 => "Function",
            10 => "Procedure",
            11 => "Variable",
            16 => "Interface",
            18 => "Enum",
            19 => "Enum",
            23 => "Define",
            25 => "Struct",
            26 => "Global",
            _ => string.Empty
        };
    }

    public class XSModel
    {
        private class XSResultMemberComparer : IEqualityComparer<XSModelResultItem>
        {
            public bool Equals(XSModelResultItem x, XSModelResultItem y) =>
                x?.Kind == y?.Kind
                && (x?.MemberName?.Equals(y?.MemberName, StringComparison.OrdinalIgnoreCase) ?? false)
                && (x?.TypeName?.Equals(y?.TypeName, StringComparison.OrdinalIgnoreCase) ?? false)
                && (x?.Namespace?.Equals(y?.Namespace, StringComparison.OrdinalIgnoreCase) ?? false);

            public int GetHashCode(XSModelResultItem obj) =>
                obj.GetHashCode();
        }

        private readonly SqliteConnection Connection;

        public XSModel(string dbFile) =>
            Connection = GetConnection(dbFile) ?? throw new ArgumentNullException();

        public async Task<(List<XSModelResultItem>, XSModelResultType)> GetSearchTermMatchesAsync(string searchTerm, Filter filter, string solutionDirectory, int limit, ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null) =>
            await GetSearchTermMatchesAsync(searchTerm, filter, solutionDirectory, null, direction, orderBy, limit, -1);

        public async Task<(List<XSModelResultItem>, XSModelResultType)> GetSearchTermMatchesAsync(string searchTerm, Filter filter, string solutionDirectory, string currentFile, int caretPosition, ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null) =>
            await GetSearchTermMatchesAsync(searchTerm, filter, solutionDirectory, currentFile, direction, orderBy, 100, caretPosition);

        public async Task<(List<XSModelResultItem>, XSModelResultType)> GetSearchTermMatchesAsync(string searchTerm, Filter filter, string solutionDirectory, string currentFile, int caretPosition, int limit, ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null) =>
            await GetSearchTermMatchesAsync(searchTerm, filter, solutionDirectory, currentFile, direction, orderBy, limit, caretPosition);

        private async Task<(List<XSModelResultItem>, XSModelResultType)> GetSearchTermMatchesAsync(string searchTerm, Filter filter, string solutionDirectory, string currentFile, ListSortDirection direction, string orderBy, int limit, int caretPostion)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return (new(), 0);

            await Connection.OpenAsync();

            var sqlSortDirection = direction == ListSortDirection.Ascending ? "ASC" : "DESC";

            var filtersApplied = filter.Type != FilterType.Inactive;

            if (!string.IsNullOrWhiteSpace(currentFile) && (searchTerm.Trim().StartsWith("..") || searchTerm.Trim().StartsWith("::")))
            {
                if (filter.Type == FilterType.Inactive)
                    filter.Type = FilterType.Member;

                var memberName = SearchTermHelper.EvaluateSearchTermLocal(searchTerm);

                if (string.IsNullOrWhiteSpace(orderBy) && (memberName.Equals(".ctor", StringComparison.OrdinalIgnoreCase) || memberName.Equals(".dtor", StringComparison.OrdinalIgnoreCase)))
                {
                    orderBy = "TypeName";
                }
                else
                {
                    if (!memberName.Contains("\"") && !memberName.Contains("*"))
                        memberName = $"%{memberName}%";

                    memberName = memberName.Replace("\"", string.Empty);
                    memberName = memberName.ToLower().Replace("*", "%");
                }

                var resultsAndResultType = await BuildAndExecuteSqlAsync(string.Empty, memberName, filter, orderBy, sqlSortDirection, limit, solutionDirectory, currentFile, caretPostion);

                Connection.Close();

                return resultsAndResultType;
            }
            else
            {
                var (typeName, memberName) = SearchTermHelper.EvaluateSearchTerm(searchTerm);

                if (!filtersApplied)
                {
                    if (string.IsNullOrWhiteSpace(memberName))
                        filter.Type = FilterType.Type;
                    else
                        filter.Type = FilterType.Member;
                }

                var (results, resultType) = await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, limit, solutionDirectory);

                if (!filtersApplied && filter.Type == FilterType.Type && results.Count < 1)
                {
                    filter.Type = FilterType.Member;
                    (results, resultType) = await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, limit, solutionDirectory);
                }
                Connection.Close();
                return (results, resultType);
            }
        }

        public async Task<(List<XSModelResultItem>, XSModelResultType)> GetCodeSuggestionsAsync(string searchTerm, Filter filter, ListSortDirection direction, string orderBy, XSModelResultItem selectedTypeInfo, string currentFile = null, int caretPosition = -1)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return (new(), 0);

            await Connection.OpenAsync();

            var sqlSortDirection = direction == ListSortDirection.Ascending ? "ASC" : "DESC";
            var filtersApplied = filter.Type != FilterType.Inactive;
            var limit = 100;

            searchTerm = searchTerm.Replace(' ', '.').Trim();

            var separators = new[] { '.', ':' };
            if (separators.Any(searchTerm.Contains))
            {
                if (filter.Type == FilterType.Inactive)
                    filter.Type = FilterType.Member;

                (List<XSModelResultItem>, XSModelResultType) resultsAndResultType;
                if (!string.IsNullOrWhiteSpace(currentFile) && (searchTerm.StartsWith("..") || searchTerm.StartsWith("::")))
                {
                    var memberName = searchTerm.Substring(searchTerm.TakeWhile(q => q == '.' || q == ':').Count()).Trim();
                    memberName = PrepareForSqlLike(memberName);
                    resultsAndResultType = await SearchMemberInHierarchyAsync(string.Empty, memberName, filter, orderBy, sqlSortDirection, currentFile, caretPosition);
                }
                else if (searchTerm.StartsWith(".") || searchTerm.StartsWith(":"))
                {
                    var memberName = searchTerm.Substring(1).Trim();
                    memberName = PrepareForSqlLike(memberName);
                    resultsAndResultType = await BuildAndExecuteSqlAsync(string.Empty, memberName, filter, orderBy, sqlSortDirection, limit);
                }
                else
                {
                    var keyWords = searchTerm.Split(new[] { '.', ':' }, 2);
                    var typeName = keyWords[0].Trim();
                    var memberName = keyWords[1].Trim();
                    memberName = PrepareForSqlLike(memberName);
                    resultsAndResultType = await SearchMemberInHierarchyAsync(typeName, memberName, filter, orderBy, sqlSortDirection, selectedTypeInfo);
                }
                Connection.Close();
                return resultsAndResultType;
            }
            else
            {
                searchTerm = PrepareForSqlLike(searchTerm);

                string typeName, memberName;
                if (filter.Type == FilterType.Member)
                {
                    typeName = string.Empty;
                    memberName = searchTerm;
                }
                else
                {
                    filter.Type = FilterType.Type;
                    typeName = searchTerm;
                    memberName = string.Empty;
                }

                var (results, resultType) = await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, limit, null, null, -1, false, null, true);

                if (!filtersApplied && filter.Type == FilterType.Type && results.Count < 1)
                {
                    filter.Type = FilterType.Member;
                    filter.MemberFilters = new List<MemberFilter> { MemberFilter.Function, MemberFilter.Define };
                    (results, resultType) = await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, limit);
                }
                Connection.Close();
                return (results, resultType);
            }
        }

        private async Task<(List<XSModelResultItem>, XSModelResultType)> SearchMemberInHierarchyAsync(string typeName, string memberName, Filter filter, string orderBy, string sqlSortDirection, string currentFile, int caretPosition) =>
            await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, 100, null, currentFile, caretPosition, true, null, true);

        private async Task<(List<XSModelResultItem>, XSModelResultType)> SearchMemberInHierarchyAsync(string typeName, string memberName, Filter filter, string orderBy, string sqlSortDirection, XSModelResultItem hierarchyTypeInfo = null) =>
            await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, 100, null, null, -1, true, hierarchyTypeInfo, true);

        private async Task<(List<XSModelResultItem>, XSModelResultType)> BuildAndExecuteSqlAsync(string typeName, string memberName, Filter filter, string orderBy, string sqlSortDirection, int limit, string solutionDirectory = null, string currentFile = null, int caretPosition = -1, bool searchInHierarchy = false, XSModelResultItem hierarchyTypeInfo = null, bool useGroupBy = false)
        {
            memberName = memberName.Replace("_", @"\_");
            typeName = typeName?.Replace("_", @"\_");

            var groupByClause = string.Empty;
            var searchingForMember = filter.Type == FilterType.Member;

            if (searchingForMember && string.IsNullOrWhiteSpace(memberName))
            {
                memberName = typeName;
                typeName = null;
            }

            if (string.IsNullOrWhiteSpace(orderBy))
            {
                orderBy = searchingForMember
                    ? $"LENGTH(TRIM(Name)) {sqlSortDirection}, TRIM(Name) {sqlSortDirection}, LENGTH(TRIM(TypeName)) {sqlSortDirection}, TRIM(TypeName) {sqlSortDirection}"
                    : $"LENGTH(TRIM(Name)) {sqlSortDirection}, TRIM(Name) {sqlSortDirection}";
            }
            else
            {
                orderBy = $"TRIM({orderBy}) {sqlSortDirection}";
            }

            var sql =
                @$"
                    SELECT Name, FileName, StartLine, ProjectFileName, Kind, Namespace, Sourcecode, BaseTypeName, IsExternal, TypeName, Id
                    FROM cte 
                    WHERE {filter.GetFilterSql(memberName)}
                    AND LOWER(TRIM(Name)) LIKE $name ESCAPE '\'
                ";
            var command = Connection.CreateCommand();
            command.Parameters.AddWithValue("$name", searchingForMember
                ? memberName.Trim().ToLower()
                : typeName.Trim().ToLower());

            List<XSModelResultItem> hierarchyTypeInfos = null;
            bool searchingWithinClass = false;
            if (searchingForMember)
            {
                if (!string.IsNullOrWhiteSpace(currentFile))
                {
                    if (caretPosition < 0)
                    {
                        sql += " AND LOWER(TRIM(FileName)) = $fileName";
                        command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower());
                    }
                    else
                    {
                        var typeInfo = await GetContaingClassAsync(currentFile, caretPosition);
                        searchingWithinClass = typeInfo != null;
                        if (searchingWithinClass)
                        {
                            if (searchInHierarchy)
                            {
                                hierarchyTypeInfos = await GetClassHierarchyInfosAsync(typeInfo);
                                sql += $"AND LOWER(TRIM(COALESCE(Namespace, '') || '.' || TypeName, '.')) IN ('{string.Join("', '", hierarchyTypeInfos.Select(q => q?.FullName?.ToLower()))}')";
                            }
                            else
                            {
                                sql += " AND TRIM(TypeName) = $typeName AND TRIM(Namespace) = $namespace";
                                command.Parameters.AddWithValue("$typeName", typeInfo.TypeName.Trim());
                                command.Parameters.AddWithValue("$namespace", typeInfo.Namespace.Trim());
                            }
                        }
                        else
                        {
                            sql += " AND LOWER(TRIM(FileName)) = $fileName";
                            command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower());
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(typeName))
                {
                    if (searchInHierarchy)
                    {
                        if (hierarchyTypeInfo == null)
                            hierarchyTypeInfo = await GetTypeInfoAsync(typeName, isFullName: false);

                        hierarchyTypeInfos = await GetClassHierarchyInfosAsync(hierarchyTypeInfo);
                        sql += $"AND LOWER(TRIM(COALESCE(Namespace, '') || '.' || TypeName, '.')) IN ('{string.Join("', '", hierarchyTypeInfos.Select(q => q?.FullName?.ToLower()))}')";
                    }
                    else
                    {
                        sql += @" AND LOWER(TRIM(TypeName)) LIKE $typeName  ESCAPE '\'";
                        command.Parameters.AddWithValue("$typeName", typeName.Trim().ToLower());
                    }
                }

                if (searchInHierarchy)
                {
                    sql +=
                        @"
					        AND LOWER(TRIM(Sourcecode)) NOT LIKE 'private%'
					        AND LOWER(TRIM(Sourcecode)) NOT LIKE 'hidden%'
                            AND LOWER(TRIM(Sourcecode)) NOT LIKE '%override%'
                        ";
                    if (!searchingWithinClass)
                        sql += "AND LOWER(TRIM(Sourcecode)) NOT LIKE 'protected%'";
                }

                if (useGroupBy)
                {
                    groupByClause =
                        @"
                            GROUP BY Name, TypeName, Namespace, Kind
					        HAVING Id = MIN(Id)
                        ";
                }
            }
            else if (useGroupBy)
            {
                groupByClause =
                    @"
                        GROUP BY Name, Namespace
					    HAVING BaseTypeName = MAX(BaseTypeName)
                    ";
            }

            sql +=
                @$"
                    AND LOWER(TRIM(FileName)) NOT LIKE '%\_vo.prg' ESCAPE '\' 
                    AND LOWER(TRIM(FileName)) NOT LIKE '%.designer.prg'
                    AND LOWER(TRIM(FileName)) NOT LIKE '%{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}%'
                    AND LOWER(TRIM(ProjectFileName)) NOT LIKE '%.(orphanedfiles).xsproj'
                ";

            var includeExternalsSql = string.Empty;
            if (searchingForMember) 
            {
                if (searchInHierarchy && hierarchyTypeInfos != null && hierarchyTypeInfos.Any(q => (q?.IsExternal ?? false)))
                {
                    var assemblyTypeInfo = hierarchyTypeInfos.First(q => q.IsExternal);
                    includeExternalsSql = ReflectionHelper.GetExternalMembersSql(assemblyTypeInfo, memberName.Split(new[] { '%' }, StringSplitOptions.RemoveEmptyEntries), searchingWithinClass) ?? string.Empty;
                }
            }
            else if (useGroupBy) //for Code Suggestions
            {
                includeExternalsSql =
                    @"
                        WHERE ((Kind = 1 AND TRIM(LOWER(Sourcecode)) NOT LIKE '(global scope)') OR Kind = 18 OR Kind = 16 OR Kind = 25)
	                    UNION
	                    SELECT Name, AssemblyFileName, 0, '', Kind, Namespace, FullName, COALESCE(BaseTypeName,''), true, '', 0
	                    FROM AssemblyTypes
	                    WHERE (Kind = 1 OR Kind = 18 OR Kind = 16 OR Kind = 25)
                    ";
            }

            var kindSql = useGroupBy && searchingForMember ? "REPLACE(REPLACE(Kind, 6, 8), 7, 8) AS Kind" : "Kind";
            var cte =
                $@"
                    WITH cte(Name, FileName, StartLine, ProjectFileName, Kind, Namespace, Sourcecode, BaseTypeName, IsExternal, TypeName, Id) AS 
                    (
	                    SELECT Name, FileName, StartLine, ProjectFileName, {kindSql}, Namespace, Sourcecode, BaseTypeName, false, {(searchingForMember ? "TypeName" : "''")}, Id
	                    FROM {filter.GetDbTable()}
                        {includeExternalsSql}
                    )
                ";

            command.CommandText =
                @$"
                    {cte}
                    {sql}
                    {groupByClause}
                    ORDER BY {orderBy}
                    LIMIT {limit}
                ";
            var reader = await command.ExecuteReaderAsync();

            var results = new List<XSModelResultItem>();
            var resultType = searchingForMember ? XSModelResultType.Member : XSModelResultType.Type;
            while (await reader.ReadAsync())
            {
                var resultItem = new XSModelResultItem
                {
                    ContainingFile = reader.GetString(1),
                    Line = reader.GetInt32(2),
                    Project = Path.GetFileNameWithoutExtension(reader.GetString(3)),
                    Kind = reader.GetInt32(4),
                    Namespace = reader.GetString(5),
                    SourceCode = reader.GetString(6),
                    BaseTypeName = reader.GetString(7),
                    IsExternal = reader.GetBoolean(8),
                    ResultType = resultType,
                    SolutionDirectory = solutionDirectory
                };
                if (searchingForMember)
                {
                    resultItem.MemberName = reader.GetString(0);
                    resultItem.TypeName = reader.GetString(9);
                }
                else
                {
                    resultItem.TypeName = reader.GetString(0);
                    resultItem.MemberName = string.Empty;
                }
                results.Add(resultItem);
            }

            return (results, resultType);
        }

        private async Task<List<XSModelResultItem>> GetClassHierarchyInfosAsync(XSModelResultItem typeInfo)
        {
            var nextTypeInfo = typeInfo;
            var typeInfos = new List<XSModelResultItem>();
            if (string.IsNullOrEmpty(typeInfo?.BaseTypeName))
                nextTypeInfo = await GetTypeInfoAsync(typeInfo?.FullName) ?? typeInfo;

            if (string.IsNullOrEmpty(nextTypeInfo?.BaseTypeName))
            {
                typeInfos.Add(nextTypeInfo);
                return typeInfos;
            }

            string fullBaseName;
            do
            {
                typeInfos.Add(nextTypeInfo);

                var baseNamespace = await FindBaseTypeNamespaceAsync(nextTypeInfo.ContainingFile, nextTypeInfo.BaseTypeName);
                fullBaseName = string.IsNullOrWhiteSpace(baseNamespace)
                    ? nextTypeInfo.BaseTypeName
                    : $"{baseNamespace}.{nextTypeInfo.BaseTypeName}";
                nextTypeInfo = await GetTypeInfoAsync(fullBaseName);
            }
            while (nextTypeInfo != null && !string.IsNullOrEmpty(nextTypeInfo.BaseTypeName));

            if (nextTypeInfo == null && !string.IsNullOrEmpty(fullBaseName))
            {
                nextTypeInfo = await GetAssemblyTypeInfoAsync(fullBaseName);
                if (nextTypeInfo != null)
                    typeInfos.Add(nextTypeInfo);
            }

            return typeInfos;
        }

        private async Task<string> FindBaseTypeNamespaceAsync(string fileName, string baseTypeName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var command = Connection.CreateCommand();
            command.CommandText =
                @"
                    WITH split(ns, usings) AS (
	                    SELECT  '',  Usings||char(13) FROM Files WHERE LOWER(TRIM(FileName)) = $fileName
	                    UNION ALL SELECT
		                    substr(usings, 0, instr(usings, char(13))),
		                    substr(usings, instr(usings, char(13)) + 1)
	                    FROM split
	                    WHERE usings != '' 
                    )
                    SELECT TRIM(ns, char(10)) AS Namespace 
                    FROM split 
                    WHERE TRIM(Namespace) in (
	                    SELECT ReferencedTypes.Namespace FROM ReferencedTypes WHERE LOWER(TRIM(Name)) = $baseTypeName
                    )
                ";
            command.Parameters.AddWithValue("$fileName", fileName.Trim().ToLower());
            command.Parameters.AddWithValue("$baseTypeName", baseTypeName.Trim().ToLower());

            var idTypeResult = await command.ExecuteScalarAsync();
            idTypeResult = idTypeResult == DBNull.Value ? null : idTypeResult;
            return idTypeResult?.ToString();
        }

        private async Task<XSModelResultItem> GetContaingClassAsync(string currentFile, int caretPosition)
        {
            if (string.IsNullOrWhiteSpace(currentFile))
                return null;

            var command = Connection.CreateCommand();
            command.CommandText =
                @"
                    WITH ContainingClass(Name, Namespace) AS (
	                    SELECT Name, Namespace
	                    FROM ProjectTypes
	                    WHERE Kind = 1
	                    AND LOWER(TRIM(Sourcecode)) LIKE '%class%'
	                    AND LOWER(TRIM(FileName)) = $fileName
	                    AND Start < $caretPos AND Stop > $caretPos
	                    ORDER BY Stop - Start ASC LIMIT 1
                    )
                    SELECT p.Name, p.Namespace, p.BaseTypeName, p.FileName 
                    FROM ProjectTypes p, ContainingClass c
                    WHERE p.Name = c.Name 
                    AND p.Namespace = c.Namespace 
                    ORDER BY BaseTypeName DESC LIMIT 1
                ";
            command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower());
            command.Parameters.AddWithValue("$caretPos", caretPosition);

            var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new XSModelResultItem
                {
                    TypeName = reader.GetString(0),
                    Namespace = reader.GetString(1),
                    BaseTypeName = reader.GetString(2),
                    ContainingFile = reader.GetString(3)
                };
            }
            return null;
        }

        private async Task<XSModelResultItem> GetTypeInfoAsync(string name, bool isFullName = true)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var command = Connection.CreateCommand();
            command.CommandText =
                $@"
                    SELECT Name, Namespace, BaseTypeName, FileName
					FROM ProjectTypes
					WHERE LOWER(TRIM({(isFullName ? "COALESCE(Namespace, '') || '.' || Name, '.'" : "Name")})) = $name
                    ORDER BY BaseTypeName DESC LIMIT 1
                ";
            command.Parameters.AddWithValue("$name", name.ToLower());

            var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new XSModelResultItem
                {
                    TypeName = reader.GetString(0),
                    Namespace = reader.GetString(1),
                    BaseTypeName = reader.GetString(2),
                    ContainingFile = reader.GetString(3),
                    IsExternal = false
                };
            }
            return null;
        }

        private async Task<XSModelResultItem> GetAssemblyTypeInfoAsync(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return null;

            var command = Connection.CreateCommand();
            command.CommandText =
                @"
                    SELECT Name, Namespace, AssemblyFileName
					FROM AssemblyTypes
					WHERE LOWER(TRIM(FullName)) = $fullName
                    ORDER BY BaseTypeName DESC LIMIT 1
                ";
            command.Parameters.AddWithValue("$fullName", fullName.ToLower());

            var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new XSModelResultItem
                {
                    TypeName = reader.GetString(0),
                    Namespace = reader.GetString(1),
                    ContainingFile = reader.GetString(2),
                    IsExternal = true
                };
            }
            return null;
        }

        public async Task<List<XSModelResultItem>> GetContainingNamespaceAsync(string searchTerm, ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null;

            var sqlSortDirection = direction == ListSortDirection.Ascending ? "ASC" : "DESC";

            if (string.IsNullOrWhiteSpace(orderBy))
                orderBy = "Name";

            await Connection.OpenAsync();
            searchTerm = searchTerm.Replace("_", @"\_");
            var command = Connection.CreateCommand();
            command.CommandText =
                @$"
                    SELECT *
                    FROM 
	                    (SELECT DISTINCT Name, Namespace
		                    FROM AssemblyTypes
		                    WHERE Namespace IS NOT NULL
			                    AND TRIM(Namespace) != ''
			                    AND LOWER(TRIM(Name)) LIKE $typeName ESCAPE '\'
	                    UNION
	                    SELECT DISTINCT Name, Namespace
		                    FROM ProjectTypes 
		                    WHERE Namespace IS NOT NULL
			                    AND TRIM(Namespace) != ''
			                    AND LOWER(TRIM(Name)) LIKE $typeName ESCAPE '\')
                    ORDER BY LENGTH(TRIM({orderBy})) {sqlSortDirection}, TRIM({orderBy}) {sqlSortDirection}
                    LIMIT 100
                ";
            command.Parameters.AddWithValue("$typeName", $"%{searchTerm.Trim().ToLower()}%");

            var reader = await command.ExecuteReaderAsync();
            var results = new List<XSModelResultItem>();
            while (await reader.ReadAsync())
            {
                var result = new XSModelResultItem
                {
                    TypeName = reader.GetString(0),
                    Namespace = reader.GetString(1)
                };
                results.Add(result);
            }

            Connection.Close();
            return results;
        }

        public async Task<bool> FileContainsUsingAsync(string file, string usingToInsert)
        {
            if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(usingToInsert))
                return false;

            await Connection.OpenAsync();
            var command = Connection.CreateCommand();
            command.CommandText =
                @"
                    SELECT Usings
					FROM Files
					WHERE LOWER(TRIM(FileName)) = $fileName
                ";
            command.Parameters.AddWithValue("$fileName", file.Trim().ToLower());

            var result = await command.ExecuteScalarAsync() as string;
            var usings = result?.Split(new[] { '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return usings?.Contains(usingToInsert) == true;
        }

        public void CloseConnection()
        {
            if (Connection?.State != System.Data.ConnectionState.Closed)
                Connection?.Close();
        }

        public static SqliteConnection GetConnection(string dbFile)
        {
            var connectionSB = new SqliteConnectionStringBuilder
            {
                DataSource = dbFile,
                Mode = SqliteOpenMode.ReadOnly
            };
            var connection = new SqliteConnection(connectionSB.ConnectionString);
            return connection;
        }

        private string PrepareForSqlLike(string value) =>
            value.Contains('*')
                ? value.Replace('*', '%')
                : $"%{value}%";
    }
}
