﻿using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XSharpPowerTools.Helpers;

namespace XSharpPowerTools
{
    public enum XSModelResultType
    {
        Type,
        Member,
        Procedure
    }

    public enum FilterableKind //later to be expanded for filtering
    { 
        Method,
        Property,
        Function,
        Variable,
        Define
    }

    public class XSModelResultItem
    {
        private static readonly List<int> KindsWithParams = new List<int> { 3, 5, 9, 10 };

        public string SolutionDirectory { get; set; }
        public XSModelResultType ResultType { get; set; }
        public string TypeName { get; set; }
        public string MemberName { get; set; }
        public string ContainingFile { get; set; }
        public string Project { get; set; }
        public string SourceCode { get; set; }
        public int Line { get; set; }
        public int Kind { get; set; }

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
            23 => "Define",
            _ => string.Empty
        };
    }

    public class NamespaceResultItem
    {
        public string TypeName { get; set; }
        public string Namespace { get; set; }
        public int ProjectId { get; set; }

        public override int GetHashCode() =>
            TypeName.GetHashCode() + Namespace.GetHashCode();
    }

    public class XSModel
    {
        private readonly Dictionary<char, FilterableKind> ValidPrefixes = new()
        {
            { 'm', FilterableKind.Method },
            { 'p', FilterableKind.Property },
            { 'f', FilterableKind.Function },
            { 'v', FilterableKind.Variable },
            { 'd', FilterableKind.Define }
        };

        private readonly SqliteConnection Connection;

        public XSModel(string dbFile) =>
            Connection = GetConnection(dbFile) ?? throw new ArgumentNullException();

        public async Task<(List<XSModelResultItem>, XSModelResultType)> GetSearchTermMatchesAsync(string searchTerm, List<FilterableKind> filters, string solutionDirectory, int limit, ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null) =>
            await GetSearchTermMatchesAsync(searchTerm, filters, solutionDirectory, null, direction, orderBy, limit, -1);

        public async Task<(List<XSModelResultItem>, XSModelResultType)> GetSearchTermMatchesAsync(string searchTerm, List<FilterableKind> filters, string solutionDirectory, string currentFile, int caretPosition, ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null) =>
            await GetSearchTermMatchesAsync(searchTerm, filters, solutionDirectory, currentFile, direction, orderBy, 100, caretPosition);

        public async Task<(List<XSModelResultItem>, XSModelResultType)> GetSearchTermMatchesAsync(string searchTerm, List<FilterableKind> filters, string solutionDirectory, string currentFile, int caretPosition, int limit, ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null) =>
            await GetSearchTermMatchesAsync(searchTerm, filters, solutionDirectory, currentFile, direction, orderBy, limit, caretPosition);

        private async Task<(List<XSModelResultItem>, XSModelResultType)> GetSearchTermMatchesAsync(string searchTerm, List<FilterableKind> filters, string solutionDirectory, string currentFile, ListSortDirection direction, string orderBy, int limit, int caretPostion)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return (new(), 0);

            await Connection.OpenAsync();

            var sqlSortDirection = direction == ListSortDirection.Ascending ? "ASC" : "DESC";

            var prefixFiltersApplied = false;
            if (searchTerm.Trim().Contains(' '))
            {
                var searchTermElements = searchTerm.Trim().Split(new[] { ' ' }, 2);
                var prefixes = searchTermElements[0];
                searchTerm = searchTermElements[1];
                if (prefixes.All(q => ValidPrefixes.Keys.Contains(q))) 
                {
                    filters.Clear();
                    foreach (var prefix in prefixes) 
                        filters.Add(ValidPrefixes[prefix]);
                    prefixFiltersApplied = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentFile) && (searchTerm.Trim().StartsWith("..") || searchTerm.Trim().StartsWith("::")))
            {
                List<XSModelResultItem> results;
                XSModelResultType resultType;

                var memberName = SearchTermHelper.EvaluateSearchTermLocal(searchTerm);

                if (caretPostion < 0)
                {
                    results = await SearchInCurrentFileAsync(memberName, filters, currentFile, orderBy, sqlSortDirection, solutionDirectory, limit);
                }
                else 
                {
                    var classInfo = await GetContaingClassAsync(currentFile, caretPostion);
                    if (classInfo != null)
                        results = await SearchInCurrentClassAsync(memberName, filters, classInfo, orderBy, sqlSortDirection, solutionDirectory, limit);
                    else
                        results = await SearchInCurrentFileAsync(memberName, filters, currentFile, orderBy, sqlSortDirection, solutionDirectory, limit);
                }
                Connection.Close();

                resultType = results.Any(q => q.ResultType == XSModelResultType.Member) 
                    ? XSModelResultType.Member 
                    : XSModelResultType.Procedure;

                return (results, resultType);
            }
            else
            {
                var (className, memberName) = SearchTermHelper.EvaluateSearchTerm(searchTerm);

                if (prefixFiltersApplied && string.IsNullOrEmpty(memberName)) 
                {
                    memberName = className;
                    className = null;
                }

                if (string.IsNullOrWhiteSpace(memberName))
                {
                    var results = await SearchForClassAsync(className, orderBy, sqlSortDirection, solutionDirectory, limit);
                    var resultType = XSModelResultType.Type;

                    if (results.Count < 1) 
                    {
                        results = await SearchForMemberAsync(null, className, filters, orderBy, sqlSortDirection, solutionDirectory, limit);
                        resultType = XSModelResultType.Member;
                    }
                    if (results.Count < 1) 
                    { 
                        results = await SearchForKindAsync(className, orderBy, sqlSortDirection, solutionDirectory, limit, FilterableKind.Function);
                        resultType = XSModelResultType.Procedure;
                    }
                    if (results.Count < 1)
                        results = await SearchForKindAsync(className, orderBy, sqlSortDirection, solutionDirectory, limit, FilterableKind.Define);

                    Connection.Close();
                    return (results, resultType);
                }
                else
                {
                    var results = await SearchForMemberAsync(className, memberName, filters, orderBy, sqlSortDirection, solutionDirectory, limit);
                    var resultType = results.Any(q => q.ResultType == XSModelResultType.Member)
                        ? XSModelResultType.Member
                        : XSModelResultType.Procedure;

                    if (results.Count < 1 && string.IsNullOrWhiteSpace(className))
                    {
                        results = await SearchForKindAsync(memberName, orderBy, sqlSortDirection, solutionDirectory, limit, FilterableKind.Function);
                        resultType = XSModelResultType.Procedure;
                    }
                    if (results.Count < 1 && string.IsNullOrWhiteSpace(className))
                        results = await SearchForKindAsync(memberName, orderBy, sqlSortDirection, solutionDirectory, limit, FilterableKind.Define);

                    Connection.Close();
                    return (results, resultType);
                }
            }
        }

        private async Task<List<XSModelResultItem>> SearchForClassAsync(string className, string orderBy, string sqlSortDirection, string solutionDirectory, int limit) 
        {
            className = className.Replace("_", @"\_");

            if (string.IsNullOrWhiteSpace(orderBy))
                orderBy = "Name";

            var command = Connection.CreateCommand();

            command.CommandText =
            @$"
                        SELECT Name, FileName, StartLine, ProjectFileName, Kind, Sourcecode
                        FROM ProjectTypes 
                        WHERE ((Kind = 1 AND LOWER(Sourcecode) LIKE '%class%') OR Kind = 16 OR Kind = 18)
                        AND LOWER(TRIM(Name)) LIKE $className ESCAPE '\'
                        ORDER BY LENGTH(TRIM({orderBy})) {sqlSortDirection}, TRIM({orderBy}) {sqlSortDirection}
                        LIMIT {limit}
                    ";
            command.Parameters.AddWithValue("$className", className.Trim().ToLower());

            var reader = await command.ExecuteReaderAsync();

            var results = new List<XSModelResultItem>();
            while (await reader.ReadAsync())
            {
                if (!reader.GetString(3).Trim().EndsWith("(OrphanedFiles).xsproj") && !reader.GetString(1).Trim().Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"))
                {
                    var resultItem = new XSModelResultItem
                    {
                        TypeName = reader.GetString(0),
                        MemberName = string.Empty,
                        ContainingFile = reader.GetString(1),
                        Line = reader.GetInt32(2),
                        Project = Path.GetFileNameWithoutExtension(reader.GetString(3)),
                        Kind = reader.GetInt32(4),
                        SourceCode = reader.GetString(5),
                        ResultType = XSModelResultType.Type,
                        SolutionDirectory = solutionDirectory
                    };
                    results.Add(resultItem);
                }
            }
            return results;
        }

        private async Task<List<XSModelResultItem>> SearchForMemberAsync(string className, string memberName, List<FilterableKind> filters, string orderBy, string sqlSortDirection, string solutionDirectory, int limit)
        {
            memberName = memberName.Replace("_", @"\_");
            className = className?.Replace("_", @"\_");

            if (string.IsNullOrWhiteSpace(orderBy))
            {
                orderBy = filters.Any(q => q != FilterableKind.Function && q != FilterableKind.Define)
                    ? "TypeName"
                    : "Name";
            }

            var command = Connection.CreateCommand();

            command.CommandText =
                @$"
                    SELECT Name, FileName, StartLine, TypeName, ProjectFileName, Kind, Sourcecode
                    FROM ProjectMembers 
                    WHERE {GetFilterSql(filters, memberName)}
                    AND LOWER(TRIM(Name)) LIKE $memberName ESCAPE '\'
                ";
            command.Parameters.AddWithValue("$memberName", memberName.Trim().ToLower());

            if (!string.IsNullOrWhiteSpace(className))
            {
                command.CommandText += @" AND LOWER(TRIM(TypeName)) LIKE $className  ESCAPE '\'";
                command.Parameters.AddWithValue("$className", className.Trim().ToLower());
            }
            command.CommandText += $" ORDER BY LENGTH(TRIM({orderBy})) {sqlSortDirection}, TRIM({orderBy}) {sqlSortDirection} LIMIT {limit}";

            var reader = await command.ExecuteReaderAsync();

            var results = new List<XSModelResultItem>();
            while (await reader.ReadAsync())
            {
                if (!reader.GetString(4).Trim().EndsWith("(OrphanedFiles).xsproj") && !reader.GetString(1).Trim().Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"))
                {
                    var resultItem = new XSModelResultItem
                    {
                        MemberName = reader.GetString(0),
                        ContainingFile = reader.GetString(1),
                        Line = reader.GetInt32(2),
                        TypeName = reader.GetString(3),
                        Project = Path.GetFileNameWithoutExtension(reader.GetString(4)),
                        Kind = reader.GetInt32(5),
                        SourceCode = reader.GetString(6),
                        ResultType = reader.GetInt32(5) == 9 || reader.GetInt32(5) == 10 || reader.GetInt32(5) == 23 ? XSModelResultType.Procedure : XSModelResultType.Member,
                        SolutionDirectory = solutionDirectory
                    };
                    results.Add(resultItem);
                }
            }
            return results;
        }

        private async Task<List<XSModelResultItem>> SearchInCurrentClassAsync(string memberName, List<FilterableKind> filters, NamespaceResultItem classInfo, string orderBy, string sqlSortDirection, string solutionDirectory, int limit)
        {
            if (string.IsNullOrWhiteSpace(memberName))
                return new();

            if (!memberName.Equals(".ctor", StringComparison.OrdinalIgnoreCase) && !memberName.Equals(".dtor", StringComparison.OrdinalIgnoreCase)) 
            {
                if (!memberName.Contains("\"") && !memberName.Contains("*"))
                    memberName = $"%{memberName}%";

                memberName = memberName.Replace("_", @"\_");
                memberName = memberName.Replace("\"", string.Empty);
                memberName = memberName.ToLower().Replace("*", "%");
            }

            if (string.IsNullOrWhiteSpace(orderBy))
                orderBy = "Name";

            var command = Connection.CreateCommand();

            command.CommandText =
                @$"
                    SELECT Name, FileName, StartLine, TypeName, ProjectFileName, Kind, Sourcecode
                    FROM ProjectMembers
                    WHERE IdType IN (SELECT Id
                				        FROM ProjectTypes
                				        WHERE Kind = 1
                				        AND LOWER(Sourcecode) LIKE '%class%'
                                        AND LOWER(TRIM(Name)) = $typeName
                                        AND LOWER(TRIM(Namespace)) = $namespace
                                        AND idProject = $projectId)
                    AND {GetFilterSql(filters, memberName)}
                    AND LOWER(Name) LIKE $memberName  ESCAPE '\'
                    ORDER BY LENGTH(TRIM({orderBy})) {sqlSortDirection}, TRIM({orderBy}) {sqlSortDirection}
                    LIMIT {limit}
                ";

            command.Parameters.AddWithValue("$memberName", memberName).SqliteType = SqliteType.Text;
            command.Parameters.AddWithValue("$typeName", classInfo.TypeName.Trim().ToLower()).SqliteType = SqliteType.Text;
            command.Parameters.AddWithValue("$namespace", classInfo.Namespace.Trim().ToLower()).SqliteType = SqliteType.Text;
            command.Parameters.AddWithValue("$projectId", classInfo.ProjectId).SqliteType = SqliteType.Integer;

            var reader = await command.ExecuteReaderAsync();

            var results = new List<XSModelResultItem>();
            while (await reader.ReadAsync())
            {
                if (!reader.GetString(4).Trim().EndsWith("(OrphanedFiles).xsproj") && !reader.GetString(1).Trim().Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"))
                {
                    var resultItem = new XSModelResultItem
                    {
                        MemberName = reader.GetString(0),
                        ContainingFile = reader.GetString(1),
                        Line = reader.GetInt32(2),
                        TypeName = reader.GetString(3),
                        Project = Path.GetFileNameWithoutExtension(reader.GetString(4)),
                        Kind = reader.GetInt32(5),
                        SourceCode = reader.GetString(6),
                        ResultType = XSModelResultType.Member,
                        SolutionDirectory = solutionDirectory
                    };
                    results.Add(resultItem);
                }
            }
            return results;
        }

        private async Task<List<XSModelResultItem>> SearchInCurrentFileAsync(string memberName, List<FilterableKind> filters, string currentFile, string orderBy, string sqlSortDirection, string solutionDirectory, int limit) 
        {
            if (string.IsNullOrWhiteSpace(memberName))
                return new();

            if (!memberName.Contains("\"") && !memberName.Contains("*"))
                memberName = $"%{memberName}%";

            memberName = memberName.Replace("_", @"\_");
            memberName = memberName.Replace("\"", string.Empty);
            memberName = memberName.ToLower().Replace("*", "%");

            if (string.IsNullOrWhiteSpace(orderBy))
                orderBy = "Name";

            var command = Connection.CreateCommand();

            command.CommandText =
                @$"
                    SELECT Name, FileName, StartLine, TypeName, ProjectFileName, Kind, Sourcecode
                    FROM ProjectMembers
                    WHERE IdType IN (SELECT Id
                				        FROM ProjectTypes
                				        WHERE Kind = 1
                				        AND LOWER(Sourcecode) LIKE '%class%'
                                        AND LOWER(TRIM(FileName))=$fileName)
                    AND {GetFilterSql(filters, memberName)}
                    AND LOWER(Name) LIKE $memberName  ESCAPE '\'
                    ORDER BY LENGTH(TRIM({orderBy})) {sqlSortDirection}, TRIM({orderBy}) {sqlSortDirection}
                    LIMIT {limit}
                ";

            command.Parameters.AddWithValue("$memberName", memberName).SqliteType = SqliteType.Text;
            command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower()).SqliteType = SqliteType.Text;

            var reader = await command.ExecuteReaderAsync();

            var results = new List<XSModelResultItem>();
            while (await reader.ReadAsync())
            {
                if (!reader.GetString(4).Trim().EndsWith("(OrphanedFiles).xsproj") && !reader.GetString(1).Trim().Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"))
                {
                    var resultItem = new XSModelResultItem
                    {
                        MemberName = reader.GetString(0),
                        ContainingFile = reader.GetString(1),
                        Line = reader.GetInt32(2),
                        TypeName = reader.GetString(3),
                        Project = Path.GetFileNameWithoutExtension(reader.GetString(4)),
                        Kind = reader.GetInt32(5),
                        SourceCode = reader.GetString(6),
                        ResultType = XSModelResultType.Member,
                        SolutionDirectory = solutionDirectory
                    };
                    results.Add(resultItem);
                }
            }
            return results;
        }

        private async Task<List<XSModelResultItem>> SearchForKindAsync(string searchTerm, string orderBy, string sqlSortDirection, string solutionDirectory, int limit, FilterableKind kind) 
        {
            var command = Connection.CreateCommand();

            if (string.IsNullOrWhiteSpace(orderBy))
                orderBy = "Name";


            command.CommandText =
                @$"
                    SELECT Name, FileName, StartLine, TypeName, ProjectFileName, Kind, Sourcecode
                    FROM ProjectMembers 
                    WHERE {GetFilterSqlConditions(kind)}
                    AND LOWER(TRIM(Name)) LIKE $memberName ESCAPE '\' 
                    ORDER BY LENGTH(TRIM({orderBy})), TRIM({orderBy}) {sqlSortDirection} LIMIT {limit}
                ";

            if (!searchTerm.Contains("\"") && !searchTerm.Contains("*"))
                searchTerm = $"%{searchTerm}%";

            searchTerm = searchTerm.Replace("_", @"\_");
            searchTerm = searchTerm.Replace("\"", string.Empty);
            searchTerm = searchTerm.ToLower().Replace("*", "%");

            command.Parameters.AddWithValue("$memberName", searchTerm);

            var reader = await command.ExecuteReaderAsync();

            var results = new List<XSModelResultItem>();
            while (await reader.ReadAsync())
            {
                if (!reader.GetString(4).Trim().EndsWith("(OrphanedFiles).xsproj") && !reader.GetString(1).Trim().Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"))
                {
                    var resultItem = new XSModelResultItem
                    {
                        MemberName = reader.GetString(0),
                        ContainingFile = reader.GetString(1),
                        Line = reader.GetInt32(2),
                        TypeName = reader.GetString(3),
                        Project = Path.GetFileNameWithoutExtension(reader.GetString(4)),
                        Kind = reader.GetInt32(5),
                        SourceCode = reader.GetString(6),
                        ResultType = XSModelResultType.Procedure,
                        SolutionDirectory = solutionDirectory
                    };
                    results.Add(resultItem);
                }
            }
            return results;
        }

        private string GetFilterSql(List<FilterableKind> kinds, string memberName) 
        {
            if (memberName.Equals(".ctor", StringComparison.OrdinalIgnoreCase))
                return "Kind = 3";
            else if (memberName.Equals(".dtor", StringComparison.OrdinalIgnoreCase))
                return "Kind = 4";

            var sb = new StringBuilder().Append('(');
            foreach (var kind in kinds) 
            {
                sb.Append(GetFilterSqlConditions(kind));
                sb.Append(" OR ");
            }
            sb.Length = sb.Length - 4;
            return sb.Append(')').ToString();
        }

        private string GetFilterSqlConditions(FilterableKind kind) => 
            kind switch
            {
                FilterableKind.Method => "Kind = 5",
                FilterableKind.Property => "(Kind = 6 OR Kind = 7 OR Kind = 8)",
                FilterableKind.Function => "(Kind = 9 OR Kind = 10)",
                FilterableKind.Variable => "Kind = 11",
                FilterableKind.Define => "Kind = 23",
                _ => null,
            };

        private async Task<NamespaceResultItem> GetContaingClassAsync(string currentFile, int caretPosition)
        {
            if (string.IsNullOrWhiteSpace(currentFile))
                return null;

            var command = Connection.CreateCommand();
            command.CommandText =
                @"
                    SELECT Name, Namespace, idProject
					FROM ProjectTypes
					WHERE Kind = 1
                    AND LOWER(TRIM(Sourcecode)) LIKE ""%class%""
                    AND TRIM(LOWER(FileName)) = $fileName
                    AND Start < $caretPos AND Stop > $caretPos
                ";
            command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower());
            command.Parameters.AddWithValue("$caretPos", caretPosition);

            var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            return reader.HasRows
                ? new NamespaceResultItem
                {
                    TypeName = reader.GetString(0),
                    Namespace = reader.GetString(1),
                    ProjectId = reader.GetInt32(2)
                }
                : null;
        }

        public async Task<List<NamespaceResultItem>> GetContainingNamespaceAsync(string searchTerm, ListSortDirection direction = ListSortDirection.Ascending, string orderBy = null)
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
			                        AND trim(Namespace) != ''
			                        AND LOWER(TRIM(Name)) LIKE $typeName ESCAPE '\'
	                        UNION
	                        SELECT DISTINCT Name, Namespace
		                        FROM ProjectTypes 
		                        WHERE Namespace IS NOT NULL
			                        AND trim(Namespace) != ''
			                        AND LOWER(TRIM(Name)) LIKE $typeName ESCAPE '\')
                        ORDER BY LENGTH(TRIM({orderBy})) {sqlSortDirection}, TRIM({orderBy}) {sqlSortDirection}
                        LIMIT 100
                    ";
            command.Parameters.AddWithValue("$typeName", $"%{searchTerm.Trim().ToLower()}%");

            var reader = await command.ExecuteReaderAsync();
            var results = new List<NamespaceResultItem>();
            while (await reader.ReadAsync())
            {
                var result = new NamespaceResultItem
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
					WHERE TRIM(LOWER(FileName)) = $fileName
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
    }
}
