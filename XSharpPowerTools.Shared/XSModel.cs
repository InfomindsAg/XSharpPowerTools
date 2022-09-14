using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
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
        public string Namespace { get; set; }
        public string Project { get; set; }
        public string SourceCode { get; set; }
        public int Line { get; set; }
        public int Kind { get; set; }

        private string _typeName;
        public string TypeName
        {
            get => _typeName;
            set => _typeName = "(Global Scope)".Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase) ? string.Empty : value?.Trim();
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

                var resultsAndResultType = await BuildAndExecuteSqlAsync(null, memberName, filter, orderBy, sqlSortDirection, limit, solutionDirectory, currentFile, caretPostion);

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

        public async Task<(List<XSModelResultItem>, XSModelResultType)> GetCodeSuggestionsAsync(string searchTerm, Filter filter, ListSortDirection direction, string orderBy, string currentFile = null, int caretPosition = -1) 
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return (new(), 0);

            await Connection.OpenAsync();

            var sqlSortDirection = direction == ListSortDirection.Ascending ? "ASC" : "DESC";
            var filtersApplied = filter.Type != FilterType.Inactive;
            var limit = 100;

            searchTerm = searchTerm.Replace(' ', '.');

            var separators = new[] { '.', ':' };
            if (separators.Any(searchTerm.Contains))
            {
                if (filter.Type == FilterType.Inactive)
                    filter.Type = FilterType.Member;

                (List<XSModelResultItem>, XSModelResultType) resultsAndResultType;
                if (!string.IsNullOrWhiteSpace(currentFile) && (searchTerm.Trim().StartsWith("..") || searchTerm.Trim().StartsWith("::"))) 
                {
                    var memberName = searchTerm.Substring(searchTerm.TakeWhile(q => q == '.' || q == ':').Count()).Trim().Replace('*', '%');
                    resultsAndResultType = await SearchMemberInHierarchyAsync(null, memberName, filter, orderBy, sqlSortDirection, currentFile, caretPosition);
                }
                else 
                {
                    var keyWords = searchTerm.Split(new[] { '.', ':' }, 2);
                    var typeName = keyWords[0].Trim();
                    var memberName = keyWords[1].Trim();
                    if (memberName.Contains('*'))
                        memberName = memberName.Replace('*', '%');
                    else
                        memberName = $"%{memberName}%";
                    resultsAndResultType = await SearchMemberInHierarchyAsync(typeName, memberName, filter, orderBy, sqlSortDirection);
                }
                Connection.Close();
                return resultsAndResultType;
            }
            else 
            {
                if (searchTerm.Contains('*'))
                    searchTerm = searchTerm.Replace('*', '%');
                else
                    searchTerm = $"%{searchTerm}%";

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

                var (results, resultType) = await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, limit);

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

        private async Task<(List<XSModelResultItem>, XSModelResultType)> BuildAndExecuteSqlAsync(string typeName, string memberName, Filter filter, string orderBy, string sqlSortDirection, int limit, string solutionDirectory = null, string currentFile = null, int caretPosition = -1, bool searchInHierarchy = false)
        {
            memberName = memberName.Replace("_", @"\_");
            typeName = typeName?.Replace("_", @"\_");

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

            var command = Connection.CreateCommand();

            command.CommandText =
                @$"
                    SELECT Name, FileName, StartLine, ProjectFileName, Kind, Namespace, Sourcecode{(searchingForMember ? ", TypeName" : string.Empty)}
                    FROM {filter.GetDbTable()} 
                    WHERE {filter.GetFilterSql(memberName)}
                    AND LOWER(TRIM(Name)) LIKE $name ESCAPE '\'
                ";
            command.Parameters.AddWithValue("$name", searchingForMember
                ? memberName.Trim().ToLower()
                : typeName.Trim().ToLower());

            if (searchingForMember)
            {
                if (!string.IsNullOrWhiteSpace(currentFile))
                {
                    if (caretPosition < 0)
                    {
                        command.CommandText += " AND LOWER(TRIM(FileName)) = $fileName";
                        command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower());
                    }
                    else
                    {
                        var idType = await GetContaingClassAsync(currentFile, caretPosition);
                        if (idType > 0)
                        {
                            if (searchInHierarchy)
                            { 
                                command.CommandText += 
                                    @$" 
                                        AND TypeName IN (
	                                        WITH RECURSIVE BaseClasses(BaseTypeName) AS(
		                                        SELECT Name FROM ProjectTypes WHERE Id = {idType}
		                                        UNION
		                                        SELECT ProjectTypes.BaseTypeName
		                                        FROM ProjectTypes, BaseClasses
		                                        WHERE LOWER(TRIM(BaseClasses.BaseTypeName)) = LOWER(TRIM(ProjectTypes.Name)) AND ProjectTypes.BaseTypeName IS NOT NULL AND TRIM(ProjectTypes.BaseTypeName) != """"
	                                        )
	                                        SELECT * FROM BaseClasses
                                        )
                                    ";
                            }
                            else 
                            {
                                command.CommandText += $" AND IdType = {idType}";
                            }
                        }
                        else
                        {
                            command.CommandText += " AND LOWER(TRIM(FileName)) = $fileName";
                            command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower());
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(typeName))
                {
                    if (searchInHierarchy)
                    {
                        command.CommandText += 
                            @" 
                                AND TypeName IN (
	                                WITH RECURSIVE BaseClasses(BaseTypeName) AS(
		                                SELECT $typeName
		                                UNION
		                                SELECT ProjectTypes.BaseTypeName
		                                FROM ProjectTypes, BaseClasses
		                                WHERE LOWER(TRIM(BaseClasses.BaseTypeName)) = LOWER(TRIM(ProjectTypes.Name)) AND ProjectTypes.BaseTypeName IS NOT NULL AND TRIM(ProjectTypes.BaseTypeName) != """"
	                                )
	                                SELECT * FROM BaseClasses
                                )
                            ";
                    }
                    else
                    {
                        command.CommandText += @" AND LOWER(TRIM(TypeName)) LIKE $typeName  ESCAPE '\'";
                    }
                    command.Parameters.AddWithValue("$typeName", typeName.Trim().ToLower());
                }

                if (searchInHierarchy) 
                {
                    command.CommandText +=
                        @"
					        AND LOWER(TRIM(Sourcecode)) NOT LIKE 'private%'
					        AND LOWER(TRIM(Sourcecode)) NOT LIKE 'hidden%'
					        AND LOWER(TRIM(Sourcecode)) NOT LIKE 'protected%'
                        ";
                }
            }
            command.CommandText +=
                @$"
                    AND LOWER(TRIM(FileName)) NOT LIKE '%\_vo.prg' ESCAPE '\' 
                    AND LOWER(TRIM(FileName)) NOT LIKE '%.designer.prg'
                    AND LOWER(TRIM(FileName)) NOT LIKE '%{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}%'
                    AND LOWER(TRIM(ProjectFileName)) NOT LIKE '%.(orphanedfiles).xsproj'
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
                    ResultType = resultType,
                    SolutionDirectory = solutionDirectory
                };
                if (searchingForMember)
                {
                    resultItem.MemberName = reader.GetString(0);
                    resultItem.TypeName = reader.GetString(7);
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

        private async Task<(List<XSModelResultItem>, XSModelResultType)> SearchMemberInHierarchyAsync(string typeName, string memberName, Filter filter, string orderBy, string sqlSortDirection, string currentFile = null, int caretPosition = -1) =>
            await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, 100, null, currentFile, -1, true);

        private async Task<int> GetContaingClassAsync(string currentFile, int caretPosition)
        {
            if (string.IsNullOrWhiteSpace(currentFile))
                return 0;

            var command = Connection.CreateCommand();
            command.CommandText =
                @"
                    SELECT Id
					FROM ProjectTypes
					WHERE Kind = 1
                    AND LOWER(TRIM(Sourcecode)) LIKE ""%class%""
                    AND TRIM(LOWER(FileName)) = $fileName
                    AND Start < $caretPos AND Stop > $caretPos
                    ORDER BY Stop - Start ASC LIMIT 1
                ";
            command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower());
            command.Parameters.AddWithValue("$caretPos", caretPosition);

            var idTypeResult = await command.ExecuteScalarAsync();
            idTypeResult = idTypeResult == DBNull.Value ? null : idTypeResult;
            return Convert.ToInt32(idTypeResult);
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
