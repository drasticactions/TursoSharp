using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TursoSharp.Generators
{
    [Generator]
    public class TursoQueryResultGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new TursoQueryResultSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not TursoQueryResultSyntaxReceiver receiver)
                return;

            var compilation = context.Compilation;
            var queryResultInfos = new List<QueryResultInfo>();
            bool hasOptionalColumns = false;

            foreach (var candidate in receiver.CandidateTypes)
            {
                var model = compilation.GetSemanticModel(candidate.SyntaxTree);
                var typeSymbol = model.GetDeclaredSymbol(candidate);

                if (typeSymbol == null)
                    continue;

                var queryResultAttribute = typeSymbol.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "TursoQueryResultAttribute");

                if (queryResultAttribute == null)
                    continue;

                var queryResultInfo = ExtractQueryResultInfo(typeSymbol, queryResultAttribute);
                queryResultInfos.Add(queryResultInfo);

                if (queryResultInfo.Properties.Any(p => p.IsOptional))
                    hasOptionalColumns = true;
            }

            // Generate shared helper class if needed
            if (hasOptionalColumns && queryResultInfos.Count > 0)
            {
                var sharedHelperCode = GenerateSharedHelperClass(queryResultInfos[0].Namespace);
                context.AddSource("TursoQueryResultHelpers.g.cs", SourceText.From(sharedHelperCode, Encoding.UTF8));
            }

            // Generate individual type extensions
            foreach (var queryResultInfo in queryResultInfos)
            {
                // Generate row extensions
                if (queryResultInfo.GenerateRowExtensions)
                {
                    var extensionsCode = GenerateRowExtensions(queryResultInfo);
                    context.AddSource($"{queryResultInfo.TypeName}RowExtensions.g.cs", SourceText.From(extensionsCode, Encoding.UTF8));
                }

                // Generate query helpers
                if (queryResultInfo.GenerateQueryHelpers)
                {
                    var helpersCode = GenerateQueryHelpers(queryResultInfo);
                    context.AddSource($"{queryResultInfo.TypeName}QueryHelpers.g.cs", SourceText.From(helpersCode, Encoding.UTF8));
                }
            }
        }

        private QueryResultInfo ExtractQueryResultInfo(INamedTypeSymbol typeSymbol, AttributeData attribute)
        {
            var info = new QueryResultInfo
            {
                Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
                TypeName = typeSymbol.Name,
                IsStruct = typeSymbol.TypeKind == TypeKind.Struct,
                GenerateRowExtensions = true,
                GenerateQueryHelpers = true,
                GenerateAsync = true
            };

            // Extract attribute parameters
            foreach (var arg in attribute.NamedArguments)
            {
                switch (arg.Key)
                {
                    case "GenerateRowExtensions":
                        info.GenerateRowExtensions = (bool)(arg.Value.Value ?? true);
                        break;
                    case "GenerateQueryHelpers":
                        info.GenerateQueryHelpers = (bool)(arg.Value.Value ?? true);
                        break;
                    case "GenerateAsync":
                        info.GenerateAsync = (bool)(arg.Value.Value ?? true);
                        break;
                }
            }

            // Extract properties
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is IPropertySymbol property && property.DeclaredAccessibility == Accessibility.Public)
                {
                    var propInfo = ExtractPropertyInfo(property);
                    if (propInfo != null)
                    {
                        info.Properties.Add(propInfo);
                    }
                }
            }

            return info;
        }

        private QueryPropertyInfo? ExtractPropertyInfo(IPropertySymbol property)
        {
            // Skip read-only properties if it's a class (structs need init)
            if (property.ContainingType.TypeKind == TypeKind.Class && property.SetMethod == null)
                return null;

            var info = new QueryPropertyInfo
            {
                PropertyName = property.Name,
                PropertyType = property.Type.ToDisplayString(),
                ColumnName = ConvertToSnakeCase(property.Name),
                IsNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated,
                IsOptional = false,
                ColumnIndex = -1
            };

            // Check for TursoQueryColumn attribute
            var columnAttr = property.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "TursoQueryColumnAttribute");

            if (columnAttr != null)
            {
                foreach (var arg in columnAttr.NamedArguments)
                {
                    switch (arg.Key)
                    {
                        case "ColumnName":
                            info.ColumnName = arg.Value.Value?.ToString() ?? info.ColumnName;
                            break;
                        case "IsOptional":
                            info.IsOptional = (bool)(arg.Value.Value ?? false);
                            break;
                        case "ColumnIndex":
                            info.ColumnIndex = (int)(arg.Value.Value ?? -1);
                            break;
                        case "ConverterType":
                            if (arg.Value.Value is INamedTypeSymbol converterType)
                            {
                                info.ConverterType = converterType.ToDisplayString();
                            }
                            break;
                    }
                }
            }

            // Check for TursoIgnore attribute
            if (property.GetAttributes().Any(a => a.AttributeClass?.Name == "TursoIgnoreAttribute"))
                return null;

            return info;
        }

        private string ConvertToSnakeCase(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    sb.Append('_');
                }
                sb.Append(char.ToLower(name[i]));
            }
            return sb.ToString();
        }

        private string GenerateRowExtensions(QueryResultInfo queryResult)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            if (queryResult.GenerateAsync)
            {
                sb.AppendLine("using System.Threading.Tasks;");
            }
            sb.AppendLine("using TursoSharp;");
            sb.AppendLine();
            sb.AppendLine($"namespace {queryResult.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Extension methods for mapping TursoRow to {queryResult.TypeName}");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static partial class TursoRowExtensions");
            sb.AppendLine("    {");

            // Generate To[TypeName] extension method
            GenerateToTypeMethod(sb, queryResult);

            // Generate TryTo[TypeName] extension method
            GenerateTryToTypeMethod(sb, queryResult);

            // Generate ToList[TypeName] extension method for result sets
            GenerateToListMethod(sb, queryResult);

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void GenerateToTypeMethod(StringBuilder sb, QueryResultInfo queryResult)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Maps a TursoRow to a {queryResult.TypeName} instance.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static {queryResult.TypeName} To{queryResult.TypeName}(this TursoRow row)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (row == null) throw new ArgumentNullException(nameof(row));");
            sb.AppendLine();

            if (queryResult.IsStruct)
            {
                sb.AppendLine($"            return new {queryResult.TypeName}");
            }
            else
            {
                sb.AppendLine($"            return new {queryResult.TypeName}");
            }
            sb.AppendLine("            {");

            for (int i = 0; i < queryResult.Properties.Count; i++)
            {
                var prop = queryResult.Properties[i];
                var comma = i < queryResult.Properties.Count - 1 ? "," : "";

                sb.Append($"                {prop.PropertyName} = ");

                if (prop.IsOptional)
                {
                    GenerateOptionalPropertyMapping(sb, prop);
                }
                else if (prop.ColumnIndex >= 0)
                {
                    GenerateIndexedPropertyMapping(sb, prop);
                }
                else
                {
                    GenerateNamedPropertyMapping(sb, prop);
                }

                sb.AppendLine(comma);
            }

            sb.AppendLine("            };");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateTryToTypeMethod(StringBuilder sb, QueryResultInfo queryResult)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Attempts to map a TursoRow to a {queryResult.TypeName} instance.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static bool TryTo{queryResult.TypeName}(this TursoRow row, out {queryResult.TypeName}? result)");
            sb.AppendLine("        {");
            sb.AppendLine("            result = default;");
            sb.AppendLine("            if (row == null) return false;");
            sb.AppendLine();
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                result = row.To{queryResult.TypeName}();");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine("            catch");
            sb.AppendLine("            {");
            sb.AppendLine("                return false;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateToListMethod(StringBuilder sb, QueryResultInfo queryResult)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Maps a TursoResultSet to a list of {queryResult.TypeName} instances.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static List<{queryResult.TypeName}> To{queryResult.TypeName}List(this TursoResultSet resultSet)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (resultSet == null) throw new ArgumentNullException(nameof(resultSet));");
            sb.AppendLine();
            sb.AppendLine($"            var results = new List<{queryResult.TypeName}>();");
            sb.AppendLine("            foreach (var row in resultSet)");
            sb.AppendLine("            {");
            sb.AppendLine($"                results.Add(row.To{queryResult.TypeName}());");
            sb.AppendLine("            }");
            sb.AppendLine("            return results;");
            sb.AppendLine("        }");

            if (queryResult.GenerateAsync)
            {
                sb.AppendLine();
                sb.AppendLine("        /// <summary>");
                sb.AppendLine($"        /// Asynchronously maps a TursoResultSet to a list of {queryResult.TypeName} instances.");
                sb.AppendLine("        /// </summary>");
                sb.AppendLine($"        public static async Task<List<{queryResult.TypeName}>> To{queryResult.TypeName}ListAsync(this TursoResultSet resultSet)");
                sb.AppendLine("        {");
                sb.AppendLine($"            return await Task.Run(() => resultSet.To{queryResult.TypeName}List());");
                sb.AppendLine("        }");
            }
            sb.AppendLine();
        }

        private void GenerateOptionalPropertyMapping(StringBuilder sb, QueryPropertyInfo prop)
        {
            sb.Append("TursoQueryResultHelpers.TryGetColumnValue(row, \"");
            sb.Append(prop.ColumnName);
            sb.Append("\", () => ");
            GenerateGetValueExpression(sb, prop, "row", true);
            sb.Append(", ");
            GenerateDefaultValue(sb, prop);
            sb.Append(")");
        }

        private void GenerateIndexedPropertyMapping(StringBuilder sb, QueryPropertyInfo prop)
        {
            if (prop.ConverterType != null)
            {
                sb.Append($"Convert{prop.PropertyName}(row, {prop.ColumnIndex})");
            }
            else
            {
                GenerateGetValueExpression(sb, prop, "row", false, prop.ColumnIndex);
            }
        }

        private void GenerateNamedPropertyMapping(StringBuilder sb, QueryPropertyInfo prop)
        {
            if (prop.ConverterType != null)
            {
                sb.Append($"Convert{prop.PropertyName}(row, \"{prop.ColumnName}\")");
            }
            else
            {
                GenerateGetValueExpression(sb, prop, "row", false);
            }
        }

        private void GenerateGetValueExpression(StringBuilder sb, QueryPropertyInfo prop, string rowVar, bool inTryGet, int? columnIndex = null)
        {
            var columnAccessor = columnIndex.HasValue ? columnIndex.Value.ToString() : $"\"{prop.ColumnName}\"";

            if (prop.PropertyType.Contains("string"))
            {
                sb.Append($"{rowVar}.GetString({columnAccessor})");
                if (!prop.IsNullable && !inTryGet)
                {
                    sb.Append(" ?? string.Empty");
                }
            }
            else if (prop.PropertyType == "int" || prop.PropertyType == "System.Int32")
            {
                sb.Append($"{rowVar}.GetInt32({columnAccessor})");
            }
            else if (prop.PropertyType.Contains("int?") || prop.PropertyType.Contains("System.Int32?"))
            {
                sb.Append($"{rowVar}.GetInt32Nullable({columnAccessor})");
            }
            else if (prop.PropertyType == "long" || prop.PropertyType == "System.Int64")
            {
                sb.Append($"{rowVar}.GetInt64({columnAccessor})");
            }
            else if (prop.PropertyType.Contains("long?") || prop.PropertyType.Contains("System.Int64?"))
            {
                sb.Append($"{rowVar}.GetInt64Nullable({columnAccessor})");
            }
            else if (prop.PropertyType == "bool" || prop.PropertyType == "System.Boolean")
            {
                sb.Append($"{rowVar}.GetBoolean({columnAccessor})");
            }
            else if (prop.PropertyType.Contains("bool?") || prop.PropertyType.Contains("System.Boolean?"))
            {
                sb.Append($"{rowVar}.GetBooleanNullable({columnAccessor})");
            }
            else if (prop.PropertyType == "System.DateTime")
            {
                sb.Append($"{rowVar}.GetDateTime({columnAccessor}) ?? DateTime.MinValue");
            }
            else if (prop.PropertyType.Contains("System.DateTime?"))
            {
                sb.Append($"{rowVar}.GetDateTime({columnAccessor})");
            }
            else if (prop.PropertyType == "double" || prop.PropertyType == "System.Double")
            {
                sb.Append($"{rowVar}.GetDouble({columnAccessor})");
            }
            else if (prop.PropertyType.Contains("double?") || prop.PropertyType.Contains("System.Double?"))
            {
                sb.Append($"{rowVar}.GetDoubleNullable({columnAccessor})");
            }
            else if (prop.PropertyType == "float" || prop.PropertyType == "System.Single")
            {
                sb.Append($"{rowVar}.GetFloat({columnAccessor})");
            }
            else if (prop.PropertyType.Contains("float?") || prop.PropertyType.Contains("System.Single?"))
            {
                sb.Append($"{rowVar}.GetFloatNullable({columnAccessor})");
            }
            else if (prop.PropertyType == "decimal" || prop.PropertyType == "System.Decimal")
            {
                sb.Append($"(decimal){rowVar}.GetDouble({columnAccessor})");
            }
            else if (prop.PropertyType.Contains("decimal?") || prop.PropertyType.Contains("System.Decimal?"))
            {
                sb.Append($"(decimal?){rowVar}.GetDoubleNullable({columnAccessor})");
            }
            else if (prop.PropertyType == "System.Guid")
            {
                if (inTryGet)
                {
                    sb.Append($"Guid.Parse({rowVar}.GetString({columnAccessor}) ?? Guid.Empty.ToString())");
                }
                else
                {
                    sb.Append($"{rowVar}.GetString({columnAccessor}) != null ? Guid.Parse({rowVar}.GetString({columnAccessor})!) : Guid.Empty");
                }
            }
            else if (prop.PropertyType.Contains("System.Guid?"))
            {
                sb.Append($"{rowVar}.GetString({columnAccessor}) != null ? Guid.Parse({rowVar}.GetString({columnAccessor})!) : (Guid?)null");
            }
            else
            {
                // Default to string conversion
                sb.Append($"{rowVar}.GetString({columnAccessor})");
                if (!prop.IsNullable && !inTryGet)
                {
                    sb.Append(" ?? string.Empty");
                }
            }
        }

        private void GenerateDefaultValue(StringBuilder sb, QueryPropertyInfo prop)
        {
            if (prop.IsNullable)
            {
                sb.Append("null");
            }
            else if (prop.PropertyType.Contains("string"))
            {
                sb.Append("string.Empty");
            }
            else if (prop.PropertyType.Contains("DateTime"))
            {
                sb.Append("DateTime.MinValue");
            }
            else if (prop.PropertyType.Contains("Guid"))
            {
                sb.Append("Guid.Empty");
            }
            else if (prop.PropertyType.Contains("bool"))
            {
                sb.Append("false");
            }
            else
            {
                sb.Append("default");
            }
        }

        private string GenerateQueryHelpers(QueryResultInfo queryResult)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            if (queryResult.GenerateAsync)
            {
                sb.AppendLine("using System.Threading.Tasks;");
            }
            sb.AppendLine("using TursoSharp;");
            sb.AppendLine();
            sb.AppendLine($"namespace {queryResult.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// Query helper methods for {queryResult.TypeName}");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public static class {queryResult.TypeName}QueryHelpers");
            sb.AppendLine("    {");

            // Generate Query extension method for TursoConnection
            GenerateQueryMethod(sb, queryResult);

            // Generate QueryFirst extension method
            GenerateQueryFirstMethod(sb, queryResult);

            // Generate QueryFirstOrDefault extension method
            GenerateQueryFirstOrDefaultMethod(sb, queryResult);

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private string GenerateSharedHelperClass(string namespaceName)
        {
            var sb = new StringBuilder();

            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using TursoSharp;");
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Shared helper methods for TursoQueryResult mapping");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static class TursoQueryResultHelpers");
            sb.AppendLine("    {");

            GenerateTryGetColumnValueMethod(sb);

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void GenerateQueryMethod(StringBuilder sb, QueryResultInfo queryResult)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Executes a query and returns the results as a list of {queryResult.TypeName}.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static List<{queryResult.TypeName}> Query{queryResult.TypeName}(this TursoConnection connection, string sql, params object[] parameters)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (connection == null) throw new ArgumentNullException(nameof(connection));");
            sb.AppendLine("            if (sql == null) throw new ArgumentNullException(nameof(sql));");
            sb.AppendLine();
            sb.AppendLine("            using var resultSet = connection.Query(sql);");
            sb.AppendLine($"            return resultSet.To{queryResult.TypeName}List();");
            sb.AppendLine("        }");

            if (queryResult.GenerateAsync)
            {
                sb.AppendLine();
                sb.AppendLine("        /// <summary>");
                sb.AppendLine($"        /// Asynchronously executes a query and returns the results as a list of {queryResult.TypeName}.");
                sb.AppendLine("        /// </summary>");
                sb.AppendLine($"        public static async Task<List<{queryResult.TypeName}>> Query{queryResult.TypeName}Async(this TursoConnection connection, string sql, params object[] parameters)");
                sb.AppendLine("        {");
                sb.AppendLine($"            return await Task.Run(() => connection.Query{queryResult.TypeName}(sql, parameters));");
                sb.AppendLine("        }");
            }
            sb.AppendLine();
        }

        private void GenerateQueryFirstMethod(StringBuilder sb, QueryResultInfo queryResult)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Executes a query and returns the first result as a {queryResult.TypeName}.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static {queryResult.TypeName} QueryFirst{queryResult.TypeName}(this TursoConnection connection, string sql, params object[] parameters)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (connection == null) throw new ArgumentNullException(nameof(connection));");
            sb.AppendLine("            if (sql == null) throw new ArgumentNullException(nameof(sql));");
            sb.AppendLine();
            sb.AppendLine("            using var resultSet = connection.Query(sql);");
            sb.AppendLine("            var enumerator = resultSet.GetEnumerator();");
            sb.AppendLine("            if (!enumerator.MoveNext())");
            sb.AppendLine("            {");
            sb.AppendLine("                throw new InvalidOperationException(\"Query returned no results\");");
            sb.AppendLine("            }");
            sb.AppendLine($"            return enumerator.Current.To{queryResult.TypeName}();");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateQueryFirstOrDefaultMethod(StringBuilder sb, QueryResultInfo queryResult)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Executes a query and returns the first result as a {queryResult.TypeName}, or default if no results.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine($"        public static {queryResult.TypeName}? QueryFirstOrDefault{queryResult.TypeName}(this TursoConnection connection, string sql, params object[] parameters)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (connection == null) throw new ArgumentNullException(nameof(connection));");
            sb.AppendLine("            if (sql == null) throw new ArgumentNullException(nameof(sql));");
            sb.AppendLine();
            sb.AppendLine("            using var resultSet = connection.Query(sql);");
            sb.AppendLine("            var enumerator = resultSet.GetEnumerator();");
            sb.AppendLine("            if (!enumerator.MoveNext())");
            sb.AppendLine("            {");
            sb.AppendLine("                return default;");
            sb.AppendLine("            }");
            sb.AppendLine($"            return enumerator.Current.To{queryResult.TypeName}();");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateTryGetColumnValueMethod(StringBuilder sb)
        {
            sb.AppendLine("        public static T TryGetColumnValue<T>(TursoRow row, string columnName, Func<T> getValue, T defaultValue)");
            sb.AppendLine("        {");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine("                // Check if column exists");
            sb.AppendLine("                for (int i = 0; i < row.ColumnCount; i++)");
            sb.AppendLine("                {");
            sb.AppendLine("                    if (string.Equals(row.GetColumnName(i), columnName, StringComparison.OrdinalIgnoreCase))");
            sb.AppendLine("                    {");
            sb.AppendLine("                        return getValue();");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                return defaultValue;");
            sb.AppendLine("            }");
            sb.AppendLine("            catch");
            sb.AppendLine("            {");
            sb.AppendLine("                return defaultValue;");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
        }

        private class TursoQueryResultSyntaxReceiver : ISyntaxReceiver
        {
            public List<TypeDeclarationSyntax> CandidateTypes { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is TypeDeclarationSyntax typeDeclaration &&
                    typeDeclaration.AttributeLists.Count > 0)
                {
                    CandidateTypes.Add(typeDeclaration);
                }
            }
        }

        private class QueryResultInfo
        {
            public string Namespace { get; set; } = "";
            public string TypeName { get; set; } = "";
            public bool IsStruct { get; set; }
            public bool GenerateRowExtensions { get; set; }
            public bool GenerateQueryHelpers { get; set; }
            public bool GenerateAsync { get; set; }
            public List<QueryPropertyInfo> Properties { get; } = new();
        }

        private class QueryPropertyInfo
        {
            public string PropertyName { get; set; } = "";
            public string PropertyType { get; set; } = "";
            public string ColumnName { get; set; } = "";
            public bool IsNullable { get; set; }
            public bool IsOptional { get; set; }
            public int ColumnIndex { get; set; } = -1;
            public string? ConverterType { get; set; }
        }
    }
}