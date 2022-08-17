using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.PlatformUI.OleComponentSupport;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
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
        private readonly Dictionary<char, MemberFilter> ValidPrefixes = new()
        {
            { 'm', MemberFilter.Method },
            { 'p', MemberFilter.Property },
            { 'f', MemberFilter.Function },
            { 'v', MemberFilter.Variable },
            { 'd', MemberFilter.Define }
        };

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

            //Prefix Filters noch zu klären
            //
            //if (searchTerm.Trim().Contains(' '))
            //{
            //    var searchTermElements = searchTerm.Trim().Split(new[] { ' ' }, 2);
            //    var prefixes = searchTermElements[0];
            //    searchTerm = searchTermElements[1];
            //    if (prefixes.All(q => ValidPrefixes.Keys.Contains(q))) 
            //    {
            //        filters.Clear();
            //        foreach (var prefix in prefixes) 
            //            filters.Add(ValidPrefixes[prefix]);
            //        filtersApplied = true;
            //    }
            //}
            //else if (filters.Count > 0 && filters.Count < 5) 
            //{
            //    filtersApplied = true;
            //}

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

                var resultsAndResultType = await BuildAndExecuteSqlAsync(null, memberName, filter, orderBy, sqlSortDirection, solutionDirectory, limit, currentFile, caretPostion);

                Connection.Close();

                return resultsAndResultType;
            }
            else
            {
                var (className, memberName) = SearchTermHelper.EvaluateSearchTerm(searchTerm);
                                    
                if (!filtersApplied) 
                {
                    if (string.IsNullOrWhiteSpace(memberName))
                        filter.Type = FilterType.Type;
                    else
                        filter.Type = FilterType.Member;
                }

                var (results, resultType) = await BuildAndExecuteSqlAsync(className, memberName, filter, orderBy, sqlSortDirection, solutionDirectory, limit);

                if (!filtersApplied && filter.Type == FilterType.Type && results.Count < 1) 
                {
                    filter.Type = FilterType.Member;
                    (results, resultType) = await BuildAndExecuteSqlAsync(className, memberName, filter, orderBy, sqlSortDirection, solutionDirectory, limit);
                }
                Connection.Close();
                return (results, resultType);
            }
        }

        private async Task<(List<XSModelResultItem>, XSModelResultType)> BuildAndExecuteSqlAsync(string className, string memberName, Filter filter, string orderBy, string sqlSortDirection, string solutionDirectory, int limit, string currentFile = null, int caretPosition = -1)
        {
            memberName = memberName.Replace("_", @"\_");
            className = className?.Replace("_", @"\_");

            var searchingForMember = filter.Type == FilterType.Member;

            if (searchingForMember && string.IsNullOrWhiteSpace(memberName))
            {
                memberName = className;
                className = null;
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
                    SELECT Name, FileName, StartLine, ProjectFileName, Kind, Sourcecode{(searchingForMember ? ", TypeName" : string.Empty)}
                    FROM {filter.GetDbTable()} 
                    WHERE {filter.GetFilterSql(memberName)}
                    AND LOWER(TRIM(Name)) LIKE $name ESCAPE '\'
                ";
            command.Parameters.AddWithValue("$name", searchingForMember
                ? memberName.Trim().ToLower()
                : className.Trim().ToLower());

            if (searchingForMember)
            {
                if (!string.IsNullOrWhiteSpace(currentFile)) 
                {
                    if (caretPosition < 0) 
                    {
                        command.CommandText += @$" AND LOWER(TRIM(FileName)) = $fileName";
                        command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower());
                    }
                    else 
                    {
                        var idType = await GetContaingClassAsync(currentFile, caretPosition);
                        if (idType > 0)
                        {
                            command.CommandText += @$" AND IdType = {idType}";
                        }
                        else 
                        {
                            command.CommandText += @$" AND LOWER(TRIM(FileName)) = $fileName";
                            command.Parameters.AddWithValue("$fileName", currentFile.Trim().ToLower());
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(className)) 
                {
                    command.CommandText += @" AND LOWER(TRIM(TypeName)) LIKE $className  ESCAPE '\'";
                    command.Parameters.AddWithValue("$className", className.Trim().ToLower());
                }
            }
            command.CommandText +=
                @$"
                    AND LOWER(TRIM(FileName)) NOT LIKE '%\_vo.prg' ESCAPE '\' 
                    AND LOWER(TRIM(FileName)) NOT LIKE '%.designer.prg'
                    ORDER BY {orderBy}
                    LIMIT {limit}
                ";

            var reader = await command.ExecuteReaderAsync();

            var results = new List<XSModelResultItem>();
            var resultType = searchingForMember ? XSModelResultType.Member : XSModelResultType.Type;
            while (await reader.ReadAsync())
            {
                if (!reader.GetString(4).Trim().EndsWith("(OrphanedFiles).xsproj") && !reader.GetString(1).Trim().Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"))
                {
                    var resultItem = new XSModelResultItem
                    {
                        ContainingFile = reader.GetString(1),
                        Line = reader.GetInt32(2),
                        Project = Path.GetFileNameWithoutExtension(reader.GetString(3)),
                        Kind = reader.GetInt32(4),
                        SourceCode = reader.GetString(5),
                        ResultType = resultType,
                        SolutionDirectory = solutionDirectory
                    };
                    if (searchingForMember)
                    {
                        resultItem.MemberName = reader.GetString(0);
                        resultItem.TypeName = reader.GetString(6);
                    }
                    else 
                    {
                        resultItem.TypeName = reader.GetString(0);
                        resultItem.MemberName = string.Empty;
                    }
                    results.Add(resultItem);
                }
            }
            return (results, resultType);
        }

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
