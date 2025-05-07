using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private readonly DbConnection Connection;

        public XSModel() =>
            Connection = GetConnection() ?? throw new ArgumentNullException();

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

                var resultsAndResultType = await BuildAndExecuteSqlAsync(null, memberName, filter, orderBy, sqlSortDirection, solutionDirectory, limit, currentFile, caretPostion);

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

                var (results, resultType) = await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, solutionDirectory, limit);

                if (!filtersApplied && filter.Type == FilterType.Type && results.Count < 1) 
                {
                    filter.Type = FilterType.Member;
                    (results, resultType) = await BuildAndExecuteSqlAsync(typeName, memberName, filter, orderBy, sqlSortDirection, solutionDirectory, limit);
                }
                return (results, resultType);
            }
        }

        private async Task<(List<XSModelResultItem>, XSModelResultType)> BuildAndExecuteSqlAsync(string typeName, string memberName, Filter filter, string orderBy, string sqlSortDirection, string solutionDirectory, int limit, string currentFile = null, int caretPosition = -1)
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

            using var command = Connection.CreateCommand();

            command.CommandText =
                @$"
                    SELECT Name, FileName, StartLine, ProjectFileName, Kind, Sourcecode{(searchingForMember ? ", TypeName" : string.Empty)}
                    FROM {filter.GetDbTable()} 
                    WHERE {filter.GetFilterSql(memberName)}
                    AND LOWER(TRIM(Name)) LIKE $name ESCAPE '\'
                ";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = searchingForMember ? memberName.Trim().ToLower() : typeName?.Trim().ToLower();
            command.Parameters.Add(parameter);

            if (searchingForMember)
            {
                if (!string.IsNullOrWhiteSpace(currentFile)) 
                {
                    if (caretPosition < 0) 
                    {
                        command.CommandText += @$" AND LOWER(TRIM(FileName)) = $fileName";
                        parameter = command.CreateParameter();
                        parameter.ParameterName = "$fileName";
                        parameter.Value = currentFile.Trim().ToLower();
                        command.Parameters.Add(parameter);
                    }
                    else 
                    {
                        var idsType = await GetContaingClassAsync(currentFile, caretPosition);
                        if (idsType.Count > 0)
                        {
                            command.CommandText += @$" AND IdType IN (" + string.Join(",", idsType) + ")";
                        }
                        else 
                        {
                            command.CommandText += @$" AND LOWER(TRIM(FileName)) = $fileName";
                            parameter = command.CreateParameter();
                            parameter.ParameterName = "$fileName";
                            parameter.Value = currentFile.Trim().ToLower();
                            command.Parameters.Add(parameter);
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(typeName)) 
                {
                    command.CommandText += @" AND LOWER(TRIM(TypeName)) LIKE $typeName  ESCAPE '\'";
                    parameter = command.CreateParameter();
                    parameter.ParameterName = "$typeName";
                    parameter.Value = typeName.Trim().ToLower();
                    command.Parameters.Add(parameter);
                }
            }
            command.CommandText +=
                @$"
                    AND LOWER(TRIM(FileName)) NOT LIKE '%\_vo.prg' ESCAPE '\' 
                    AND LOWER(TRIM(FileName)) NOT LIKE '%.designer.prg'
                    ORDER BY {orderBy}
                    LIMIT {limit}
                ";

            using var reader = await command.ExecuteReaderAsync();

            var results = new List<XSModelResultItem>();
            var resultType = searchingForMember ? XSModelResultType.Member : XSModelResultType.Type;
            while (await reader.ReadAsync())
            {
                if (!reader.GetString(3).Trim().EndsWith("(OrphanedFiles).xsproj") && !reader.GetString(1).Trim().Contains($"{Path.DirectorySeparatorChar}.vs{Path.DirectorySeparatorChar}"))
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

        private static void AddWithValue(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        private async Task<List<long>> GetContaingClassAsync(string currentFile, int caretPosition)
        {
            var result = new List<long>();
            if (string.IsNullOrWhiteSpace(currentFile))
                return result;

            using var command = Connection.CreateCommand();
            command.CommandText =
                @"
                    SELECT Name, Namespace, idProject
					FROM ProjectTypes
					WHERE Kind = 1
                    AND LOWER(TRIM(Sourcecode)) LIKE ""%class%""
                    AND TRIM(LOWER(FileName)) = $fileName
                    AND Start < $caretPos AND Stop > $caretPos
                    ORDER BY Stop - Start ASC LIMIT 1
                ";
            AddWithValue(command, "$fileName", currentFile.Trim().ToLower());
            AddWithValue(command, "$caretPos", caretPosition);

            long idProject = 0;
            string name = string.Empty;
            string @namespace = string.Empty;
            using (var currentClassReader = await command.ExecuteReaderAsync())
            {
                if (!await currentClassReader.ReadAsync())
                    return result;

                // there is only one record
                name = currentClassReader.GetString(0);
                @namespace = currentClassReader.GetString(1);
                idProject = currentClassReader.GetInt64(2);
            }
            command.CommandText =
                @"
                    SELECT id
					FROM ProjectTypes
					WHERE Kind = 1 AND LOWER(Name) = $name AND LOWER(Namespace) = $namespace AND idProject = $idProject
                ";

            command.Parameters.Clear();
            AddWithValue(command, "$name", name.ToLower());
            AddWithValue(command, "$namespace", @namespace.ToLower());
            AddWithValue(command, "$idProject", idProject);

            using var types = await command.ExecuteReaderAsync();
            while (await types.ReadAsync())
            {
                result.Add(types.GetInt64(0));
            }
            
            return result;
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
            using var command = Connection.CreateCommand();
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
            AddWithValue(command, "$typeName", $"%{searchTerm.Trim().ToLower()}%");

            using var reader = await command.ExecuteReaderAsync();
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

            return results;
        }

        public async Task<bool> FileContainsUsingAsync(string file, string usingToInsert) 
        {
            if (string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(usingToInsert))
                return false;

            await Connection.OpenAsync();
            using var command = Connection.CreateCommand();
            command.CommandText =
                @"
                    SELECT Usings
					FROM Files
					WHERE TRIM(LOWER(FileName)) = $fileName
                ";
            AddWithValue(command, "$fileName", file.Trim().ToLower());

            var result = await command.ExecuteScalarAsync() as string;
            var usings = result?.Split(new[] { '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return usings?.Contains(usingToInsert) == true;
        }

        private static FieldInfo _connectionField;

        public static DbConnection GetConnection()
        {
            if (_connectionField == null)
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(q => q.GetType("XSharpModel.XDatabase", false) != null);
                var dbTypeXSharp = assembly?.GetType("XSharpModel.XDatabase");
                // get the static field with reflection from the type obj
                _connectionField = dbTypeXSharp?.GetField("oConn", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            }

            return _connectionField?.GetValue(null) as DbConnection;
        }
    }
}
