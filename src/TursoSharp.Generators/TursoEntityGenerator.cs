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
    public class TursoEntityGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new TursoEntitySyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not TursoEntitySyntaxReceiver receiver)
                return;

            var compilation = context.Compilation;

            foreach (var classDeclaration in receiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
                var classSymbol = model.GetDeclaredSymbol(classDeclaration);

                if (classSymbol == null)
                    continue;

                var tursoEntityAttribute = classSymbol.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "TursoEntityAttribute");

                if (tursoEntityAttribute == null)
                    continue;

                var entityInfo = ExtractEntityInfo(classSymbol, tursoEntityAttribute);
                
                // Generate repository code
                if (entityInfo.GenerateRepository)
                {
                    var repositoryCode = GenerateRepository(entityInfo);
                    context.AddSource($"{entityInfo.ClassName}Repository.g.cs", SourceText.From(repositoryCode, Encoding.UTF8));
                }

                // Generate extensions for the entity
                var extensionsCode = GenerateEntityExtensions(entityInfo);
                context.AddSource($"{entityInfo.ClassName}Extensions.g.cs", SourceText.From(extensionsCode, Encoding.UTF8));
            }
        }

        private EntityInfo ExtractEntityInfo(INamedTypeSymbol classSymbol, AttributeData attribute)
        {
            var info = new EntityInfo
            {
                Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                ClassName = classSymbol.Name,
                TableName = classSymbol.Name.ToLowerInvariant() + "s", // Default pluralization
                GenerateRepository = true,
                GenerateAsync = true,
                GenerateCreateTable = true
            };

            // Extract attribute parameters
            foreach (var arg in attribute.NamedArguments)
            {
                switch (arg.Key)
                {
                    case "TableName":
                        info.TableName = arg.Value.Value?.ToString() ?? info.TableName;
                        break;
                    case "GenerateRepository":
                        info.GenerateRepository = (bool)(arg.Value.Value ?? true);
                        break;
                    case "GenerateAsync":
                        info.GenerateAsync = (bool)(arg.Value.Value ?? true);
                        break;
                    case "GenerateCreateTable":
                        info.GenerateCreateTable = (bool)(arg.Value.Value ?? true);
                        break;
                }
            }

            // Extract properties
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IPropertySymbol property && property.DeclaredAccessibility == Accessibility.Public)
                {
                    var propInfo = ExtractPropertyInfo(property);
                    if (propInfo != null)
                    {
                        info.Properties.Add(propInfo);
                        if (propInfo.IsPrimaryKey)
                        {
                            info.PrimaryKey = propInfo;
                        }
                    }
                }
            }

            return info;
        }

        private PropertyInfo? ExtractPropertyInfo(IPropertySymbol property)
        {
            // Check for TursoIgnore attribute
            if (property.GetAttributes().Any(a => a.AttributeClass?.Name == "TursoIgnoreAttribute"))
                return null;

            var info = new PropertyInfo
            {
                PropertyName = property.Name,
                PropertyType = property.Type.ToDisplayString(),
                ColumnName = ConvertToSnakeCase(property.Name),
                SqlType = GetSqlType(property.Type),
                IsNullable = property.Type.NullableAnnotation == NullableAnnotation.Annotated,
                IncludeInInsert = true,
                IncludeInUpdate = true
            };

            // Check for TursoPrimaryKey attribute
            var primaryKeyAttr = property.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "TursoPrimaryKeyAttribute");
            
            if (primaryKeyAttr != null)
            {
                info.IsPrimaryKey = true;
                info.AutoIncrement = true;
                info.IncludeInInsert = false; // Don't include auto-increment in insert
                
                foreach (var arg in primaryKeyAttr.NamedArguments)
                {
                    switch (arg.Key)
                    {
                        case "AutoIncrement":
                            info.AutoIncrement = (bool)(arg.Value.Value ?? true);
                            if (!info.AutoIncrement)
                                info.IncludeInInsert = true;
                            break;
                        case "ColumnName":
                            info.ColumnName = arg.Value.Value?.ToString() ?? info.ColumnName;
                            break;
                    }
                }
            }

            // Check for TursoColumn attribute
            var columnAttr = property.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "TursoColumnAttribute");
            
            if (columnAttr != null)
            {
                foreach (var arg in columnAttr.NamedArguments)
                {
                    switch (arg.Key)
                    {
                        case "ColumnName":
                            info.ColumnName = arg.Value.Value?.ToString() ?? info.ColumnName;
                            break;
                        case "SqlType":
                            info.SqlType = arg.Value.Value?.ToString() ?? info.SqlType;
                            break;
                        case "IsNullable":
                            info.IsNullable = (bool)(arg.Value.Value ?? true);
                            break;
                        case "DefaultValue":
                            info.DefaultValue = arg.Value.Value?.ToString();
                            break;
                        case "IncludeInInsert":
                            info.IncludeInInsert = (bool)(arg.Value.Value ?? true);
                            break;
                        case "IncludeInUpdate":
                            info.IncludeInUpdate = (bool)(arg.Value.Value ?? true);
                            break;
                    }
                }
            }

            return info;
        }

        private string GetSqlType(ITypeSymbol type)
        {
            var typeName = type.ToDisplayString();
            
            return typeName switch
            {
                "int" or "System.Int32" => "INTEGER",
                "long" or "System.Int64" => "INTEGER",
                "string" or "System.String" => "TEXT",
                "bool" or "System.Boolean" => "INTEGER",
                "float" or "System.Single" => "REAL",
                "double" or "System.Double" => "REAL",
                "decimal" or "System.Decimal" => "REAL",
                "System.DateTime" => "DATETIME",
                "System.Guid" => "TEXT",
                _ when typeName.StartsWith("int?") || typeName.StartsWith("System.Int32?") => "INTEGER",
                _ when typeName.StartsWith("long?") || typeName.StartsWith("System.Int64?") => "INTEGER",
                _ when typeName.StartsWith("string?") => "TEXT",
                _ when typeName.StartsWith("bool?") || typeName.StartsWith("System.Boolean?") => "INTEGER",
                _ when typeName.StartsWith("float?") || typeName.StartsWith("System.Single?") => "REAL",
                _ when typeName.StartsWith("double?") || typeName.StartsWith("System.Double?") => "REAL",
                _ when typeName.StartsWith("decimal?") || typeName.StartsWith("System.Decimal?") => "REAL",
                _ when typeName.StartsWith("System.DateTime?") => "DATETIME",
                _ when typeName.StartsWith("System.Guid?") => "TEXT",
                _ => "TEXT"
            };
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

        private string GenerateRepository(EntityInfo entity)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using TursoSharp;");
            sb.AppendLine();
            sb.AppendLine($"namespace {entity.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {entity.ClassName}Repository : IDisposable");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly TursoConnection _connection;");
            sb.AppendLine("        private readonly bool _ownsConnection;");
            sb.AppendLine();
            
            // Constructor
            sb.AppendLine($"        public {entity.ClassName}Repository(TursoConnection connection, bool ownsConnection = false)");
            sb.AppendLine("        {");
            sb.AppendLine("            _connection = connection ?? throw new ArgumentNullException(nameof(connection));");
            sb.AppendLine("            _ownsConnection = ownsConnection;");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Generate CreateTable method
            if (entity.GenerateCreateTable)
            {
                GenerateCreateTableMethod(sb, entity);
            }

            // Generate CRUD methods
            GenerateInsertMethod(sb, entity);
            GenerateUpdateMethod(sb, entity);
            GenerateDeleteMethod(sb, entity);
            GenerateGetByIdMethod(sb, entity);
            GenerateGetAllMethod(sb, entity);
            GenerateCountMethod(sb, entity);

            // Dispose
            sb.AppendLine("        public void Dispose()");
            sb.AppendLine("        {");
            sb.AppendLine("            if (_ownsConnection)");
            sb.AppendLine("            {");
            sb.AppendLine("                _connection?.Dispose();");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private void GenerateCreateTableMethod(StringBuilder sb, EntityInfo entity)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Creates the {entity.TableName} table if it doesn't exist.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public void CreateTable()");
            sb.AppendLine("        {");
            sb.AppendLine($"            var sql = @\"CREATE TABLE IF NOT EXISTS {entity.TableName} (");
            
            var columns = new List<string>();
            foreach (var prop in entity.Properties)
            {
                var column = $"                {prop.ColumnName} {prop.SqlType}";
                
                if (prop.IsPrimaryKey)
                {
                    column += " PRIMARY KEY";
                    if (prop.AutoIncrement)
                    {
                        column += " AUTOINCREMENT";
                    }
                }
                else if (!prop.IsNullable)
                {
                    column += " NOT NULL";
                }
                
                if (!string.IsNullOrEmpty(prop.DefaultValue))
                {
                    column += $" DEFAULT {prop.DefaultValue}";
                }
                
                columns.Add(column);
            }
            
            sb.AppendLine(string.Join(",\n", columns));
            sb.AppendLine("            )\";");
            sb.AppendLine("            _connection.Execute(sql);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateInsertMethod(StringBuilder sb, EntityInfo entity)
        {
            var insertableProps = entity.Properties.Where(p => p.IncludeInInsert).ToList();
            
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Inserts a new {entity.ClassName} into the database.");
            sb.AppendLine("        /// </summary>");
            
            if (entity.GenerateAsync)
            {
                if (entity.PrimaryKey != null && entity.PrimaryKey.AutoIncrement)
                {
                    sb.AppendLine($"        public async Task<{entity.PrimaryKey.PropertyType}> InsertAsync({entity.ClassName} entity)");
                    sb.AppendLine("        {");
                    sb.AppendLine("            return await Task.Run(() => Insert(entity));");
                    sb.AppendLine("        }");
                }
                else
                {
                    sb.AppendLine($"        public async Task InsertAsync({entity.ClassName} entity)");
                    sb.AppendLine("        {");
                    sb.AppendLine("            await Task.Run(() => Insert(entity));");
                    sb.AppendLine("        }");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine($"        public {(entity.PrimaryKey != null && entity.PrimaryKey.AutoIncrement ? entity.PrimaryKey.PropertyType : "void")} Insert({entity.ClassName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (entity == null) throw new ArgumentNullException(nameof(entity));");
            sb.AppendLine();
            
            var columns = string.Join(", ", insertableProps.Select(p => p.ColumnName));
            var parameters = string.Join(", ", insertableProps.Select((p, i) => "?"));
            
            sb.AppendLine($"            var sql = \"INSERT INTO {entity.TableName} ({columns}) VALUES ({parameters})\";");
            sb.AppendLine("            using var statement = _connection.Prepare(sql);");
            sb.AppendLine();
            
            // Bind parameters
            for (int i = 0; i < insertableProps.Count; i++)
            {
                var prop = insertableProps[i];
                GenerateBindParameter(sb, i + 1, prop, "entity");
            }
            
            sb.AppendLine();
            sb.AppendLine("            var result = statement.Step();");
            sb.AppendLine("            if (result != 0) // 0 = Done");
            sb.AppendLine("            {");
            sb.AppendLine($"                throw new TursoException(\"Failed to insert {entity.ClassName}\");");
            sb.AppendLine("            }");
            
            if (entity.PrimaryKey != null && entity.PrimaryKey.AutoIncrement)
            {
                sb.AppendLine();
                sb.AppendLine("            var id = _connection.QueryScalarInt32(\"SELECT last_insert_rowid()\");");
                
                if (entity.PrimaryKey.PropertyType.Contains("long") || entity.PrimaryKey.PropertyType.Contains("Int64"))
                {
                    sb.AppendLine("            return (long)id;");
                }
                else
                {
                    sb.AppendLine("            return id;");
                }
            }
            
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateUpdateMethod(StringBuilder sb, EntityInfo entity)
        {
            if (entity.PrimaryKey == null)
                return;
                
            var updatableProps = entity.Properties.Where(p => p.IncludeInUpdate && !p.IsPrimaryKey).ToList();
            
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Updates an existing {entity.ClassName} in the database.");
            sb.AppendLine("        /// </summary>");
            
            if (entity.GenerateAsync)
            {
                sb.AppendLine($"        public async Task UpdateAsync({entity.ClassName} entity)");
                sb.AppendLine("        {");
                sb.AppendLine("            await Task.Run(() => Update(entity));");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            
            sb.AppendLine($"        public void Update({entity.ClassName} entity)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (entity == null) throw new ArgumentNullException(nameof(entity));");
            sb.AppendLine();
            
            var setClause = string.Join(", ", updatableProps.Select(p => $"{p.ColumnName} = ?"));
            
            sb.AppendLine($"            var sql = \"UPDATE {entity.TableName} SET {setClause} WHERE {entity.PrimaryKey.ColumnName} = ?\";");
            sb.AppendLine("            using var statement = _connection.Prepare(sql);");
            sb.AppendLine();
            
            // Bind parameters
            for (int i = 0; i < updatableProps.Count; i++)
            {
                var prop = updatableProps[i];
                GenerateBindParameter(sb, i + 1, prop, "entity");
            }
            
            // Bind primary key
            GenerateBindParameter(sb, updatableProps.Count + 1, entity.PrimaryKey, "entity");
            
            sb.AppendLine();
            sb.AppendLine("            var result = statement.Step();");
            sb.AppendLine("            if (result != 0) // 0 = Done");
            sb.AppendLine("            {");
            sb.AppendLine($"                throw new TursoException(\"Failed to update {entity.ClassName}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateDeleteMethod(StringBuilder sb, EntityInfo entity)
        {
            if (entity.PrimaryKey == null)
                return;
                
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Deletes a {entity.ClassName} from the database by ID.");
            sb.AppendLine("        /// </summary>");
            
            if (entity.GenerateAsync)
            {
                sb.AppendLine($"        public async Task DeleteAsync({entity.PrimaryKey.PropertyType} id)");
                sb.AppendLine("        {");
                sb.AppendLine("            await Task.Run(() => Delete(id));");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            
            sb.AppendLine($"        public void Delete({entity.PrimaryKey.PropertyType} id)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var sql = \"DELETE FROM {entity.TableName} WHERE {entity.PrimaryKey.ColumnName} = ?\";");
            sb.AppendLine("            using var statement = _connection.Prepare(sql);");
            
            // Simplified binding for ID
            if (entity.PrimaryKey.PropertyType.Contains("long") || entity.PrimaryKey.PropertyType.Contains("Int64"))
            {
                sb.AppendLine("            statement.BindInt64(1, id);");
            }
            else
            {
                sb.AppendLine("            statement.BindInt64(1, id);");
            }
            
            sb.AppendLine();
            sb.AppendLine("            var result = statement.Step();");
            sb.AppendLine("            if (result != 0) // 0 = Done");
            sb.AppendLine("            {");
            sb.AppendLine($"                throw new TursoException(\"Failed to delete {entity.ClassName}\");");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateGetByIdMethod(StringBuilder sb, EntityInfo entity)
        {
            if (entity.PrimaryKey == null)
                return;
                
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Gets a {entity.ClassName} by ID.");
            sb.AppendLine("        /// </summary>");
            
            if (entity.GenerateAsync)
            {
                sb.AppendLine($"        public async Task<{entity.ClassName}?> GetByIdAsync({entity.PrimaryKey.PropertyType} id)");
                sb.AppendLine("        {");
                sb.AppendLine("            return await Task.Run(() => GetById(id));");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            
            sb.AppendLine($"        public {entity.ClassName}? GetById({entity.PrimaryKey.PropertyType} id)");
            sb.AppendLine("        {");
            
            var columns = string.Join(", ", entity.Properties.Select(p => p.ColumnName));
            sb.AppendLine($"            var sql = \"SELECT {columns} FROM {entity.TableName} WHERE {entity.PrimaryKey.ColumnName} = ?\";");
            sb.AppendLine("            using var statement = _connection.Prepare(sql);");
            
            // Bind ID
            if (entity.PrimaryKey.PropertyType.Contains("long") || entity.PrimaryKey.PropertyType.Contains("Int64"))
            {
                sb.AppendLine("            statement.BindInt64(1, id);");
            }
            else
            {
                sb.AppendLine("            statement.BindInt64(1, id);");
            }
            
            sb.AppendLine();
            sb.AppendLine("            var result = statement.Step();");
            sb.AppendLine("            if (result == 1) // Row available");
            sb.AppendLine("            {");
            sb.AppendLine("                var row = new TursoRow(statement);");
            sb.AppendLine("                return MapRowToEntity(row);");
            sb.AppendLine("            }");
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateGetAllMethod(StringBuilder sb, EntityInfo entity)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Gets all {entity.ClassName} entities from the database.");
            sb.AppendLine("        /// </summary>");
            
            if (entity.GenerateAsync)
            {
                sb.AppendLine($"        public async Task<List<{entity.ClassName}>> GetAllAsync()");
                sb.AppendLine("        {");
                sb.AppendLine("            return await Task.Run(() => GetAll());");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            
            sb.AppendLine($"        public List<{entity.ClassName}> GetAll()");
            sb.AppendLine("        {");
            
            var columns = string.Join(", ", entity.Properties.Select(p => p.ColumnName));
            sb.AppendLine($"            var sql = \"SELECT {columns} FROM {entity.TableName}\";");
            sb.AppendLine($"            var results = new List<{entity.ClassName}>();");
            sb.AppendLine();
            sb.AppendLine("            using var resultSet = _connection.Query(sql);");
            sb.AppendLine("            foreach (var row in resultSet)");
            sb.AppendLine("            {");
            sb.AppendLine("                results.Add(MapRowToEntity(row));");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return results;");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        private void GenerateCountMethod(StringBuilder sb, EntityInfo entity)
        {
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Gets the count of {entity.ClassName} entities in the database.");
            sb.AppendLine("        /// </summary>");
            
            if (entity.GenerateAsync)
            {
                sb.AppendLine("        public async Task<int> CountAsync()");
                sb.AppendLine("        {");
                sb.AppendLine("            return await Task.Run(() => Count());");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            
            sb.AppendLine("        public int Count()");
            sb.AppendLine("        {");
            sb.AppendLine($"            return _connection.QueryScalarInt32(\"SELECT COUNT(*) FROM {entity.TableName}\");");
            sb.AppendLine("        }");
            sb.AppendLine();

            // Generate CountWhere methods
            sb.AppendLine("        /// <summary>");
            sb.AppendLine($"        /// Gets the count of {entity.ClassName} entities in the database that match the specified WHERE clause.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        /// <param name=\"whereClause\">The WHERE clause (without the WHERE keyword)</param>");
            
            if (entity.GenerateAsync)
            {
                sb.AppendLine("        public async Task<int> CountWhereAsync(string whereClause)");
                sb.AppendLine("        {");
                sb.AppendLine("            return await Task.Run(() => CountWhere(whereClause));");
                sb.AppendLine("        }");
                sb.AppendLine();
            }
            
            sb.AppendLine("        public int CountWhere(string whereClause)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (string.IsNullOrWhiteSpace(whereClause))");
            sb.AppendLine("                throw new ArgumentException(\"WHERE clause cannot be null or empty\", nameof(whereClause));");
            sb.AppendLine();
            sb.AppendLine($"            var sql = $\"SELECT COUNT(*) FROM {entity.TableName} WHERE {{whereClause}}\";");
            sb.AppendLine("            return _connection.QueryScalarInt32(sql);");
            sb.AppendLine("        }");
            sb.AppendLine();
            
            // Generate the mapping method
            GenerateMappingMethod(sb, entity);
        }

        private void GenerateMappingMethod(StringBuilder sb, EntityInfo entity)
        {
            sb.AppendLine($"        private {entity.ClassName} MapRowToEntity(TursoRow row)");
            sb.AppendLine("        {");
            sb.AppendLine($"            return new {entity.ClassName}");
            sb.AppendLine("            {");
            
            for (int i = 0; i < entity.Properties.Count; i++)
            {
                var prop = entity.Properties[i];
                var comma = i < entity.Properties.Count - 1 ? "," : "";
                
                sb.Append($"                {prop.PropertyName} = ");
                GenerateGetValue(sb, prop);
                sb.AppendLine(comma);
            }
            
            sb.AppendLine("            };");
            sb.AppendLine("        }");
        }

        private void GenerateBindParameter(StringBuilder sb, int index, PropertyInfo prop, string entityVar)
        {
            var accessor = $"{entityVar}.{prop.PropertyName}";
            
            if (prop.PropertyType.Contains("string"))
            {
                sb.AppendLine($"            statement.BindString({index}, {accessor} ?? string.Empty);");
            }
            else if (prop.PropertyType.Contains("int") && !prop.PropertyType.Contains("uint"))
            {
                if (prop.PropertyType.Contains("?"))
                {
                    sb.AppendLine($"            if ({accessor}.HasValue)");
                    sb.AppendLine($"                statement.BindInt64({index}, {accessor}.Value);");
                    sb.AppendLine("            else");
                    sb.AppendLine($"                statement.BindNull({index});");
                }
                else
                {
                    sb.AppendLine($"            statement.BindInt64({index}, {accessor});");
                }
            }
            else if (prop.PropertyType.Contains("long") || prop.PropertyType.Contains("Int64"))
            {
                if (prop.PropertyType.Contains("?"))
                {
                    sb.AppendLine($"            if ({accessor}.HasValue)");
                    sb.AppendLine($"                statement.BindInt64({index}, {accessor}.Value);");
                    sb.AppendLine("            else");
                    sb.AppendLine($"                statement.BindNull({index});");
                }
                else
                {
                    sb.AppendLine($"            statement.BindInt64({index}, {accessor});");
                }
            }
            else if (prop.PropertyType.Contains("bool"))
            {
                if (prop.PropertyType.Contains("?"))
                {
                    sb.AppendLine($"            if ({accessor}.HasValue)");
                    sb.AppendLine($"                statement.BindInt64({index}, {accessor}.Value ? 1 : 0);");
                    sb.AppendLine("            else");
                    sb.AppendLine($"                statement.BindNull({index});");
                }
                else
                {
                    sb.AppendLine($"            statement.BindInt64({index}, {accessor} ? 1 : 0);");
                }
            }
            else if (prop.PropertyType.Contains("DateTime"))
            {
                if (prop.PropertyType.Contains("?"))
                {
                    sb.AppendLine($"            if ({accessor}.HasValue)");
                    sb.AppendLine($"                statement.BindString({index}, {accessor}.Value.ToString(\"yyyy-MM-dd HH:mm:ss\"));");
                    sb.AppendLine("            else");
                    sb.AppendLine($"                statement.BindNull({index});");
                }
                else
                {
                    sb.AppendLine($"            statement.BindString({index}, {accessor}.ToString(\"yyyy-MM-dd HH:mm:ss\"));");
                }
            }
            else if (prop.PropertyType.Contains("double") || prop.PropertyType.Contains("float") || prop.PropertyType.Contains("decimal"))
            {
                if (prop.PropertyType.Contains("?"))
                {
                    sb.AppendLine($"            if ({accessor}.HasValue)");
                    sb.AppendLine($"                statement.BindDouble({index}, (double){accessor}.Value);");
                    sb.AppendLine("            else");
                    sb.AppendLine($"                statement.BindNull({index});");
                }
                else
                {
                    sb.AppendLine($"            statement.BindDouble({index}, (double){accessor});");
                }
            }
            else if (prop.PropertyType.Contains("Guid"))
            {
                if (prop.PropertyType.Contains("?"))
                {
                    sb.AppendLine($"            if ({accessor}.HasValue)");
                    sb.AppendLine($"                statement.BindString({index}, {accessor}.Value.ToString());");
                    sb.AppendLine("            else");
                    sb.AppendLine($"                statement.BindNull({index});");
                }
                else
                {
                    sb.AppendLine($"            statement.BindString({index}, {accessor}.ToString());");
                }
            }
            else
            {
                // Default to string
                sb.AppendLine($"            statement.BindString({index}, {accessor}?.ToString() ?? string.Empty);");
            }
        }

        private void GenerateGetValue(StringBuilder sb, PropertyInfo prop)
        {
            if (prop.PropertyType.Contains("string"))
            {
                sb.Append($"row.GetString(\"{prop.ColumnName}\") ?? string.Empty");
            }
            else if (prop.PropertyType == "int" || prop.PropertyType == "System.Int32")
            {
                sb.Append($"row.GetInt32(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType.Contains("int?") || prop.PropertyType.Contains("System.Int32?"))
            {
                sb.Append($"row.GetInt32Nullable(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType == "long" || prop.PropertyType == "System.Int64")
            {
                sb.Append($"row.GetInt64(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType.Contains("long?") || prop.PropertyType.Contains("System.Int64?"))
            {
                sb.Append($"row.GetInt64Nullable(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType == "bool" || prop.PropertyType == "System.Boolean")
            {
                sb.Append($"row.GetBoolean(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType.Contains("bool?") || prop.PropertyType.Contains("System.Boolean?"))
            {
                sb.Append($"row.GetBooleanNullable(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType == "System.DateTime")
            {
                sb.Append($"row.GetDateTime(\"{prop.ColumnName}\") ?? DateTime.Now");
            }
            else if (prop.PropertyType.Contains("System.DateTime?"))
            {
                sb.Append($"row.GetDateTime(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType == "double" || prop.PropertyType == "System.Double")
            {
                sb.Append($"row.GetDouble(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType.Contains("double?") || prop.PropertyType.Contains("System.Double?"))
            {
                sb.Append($"row.GetDoubleNullable(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType == "float" || prop.PropertyType == "System.Single")
            {
                sb.Append($"(float)row.GetDouble(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType.Contains("float?") || prop.PropertyType.Contains("System.Single?"))
            {
                sb.Append($"(float?)row.GetDoubleNullable(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType == "decimal" || prop.PropertyType == "System.Decimal")
            {
                sb.Append($"(decimal)row.GetDouble(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType.Contains("decimal?") || prop.PropertyType.Contains("System.Decimal?"))
            {
                sb.Append($"(decimal?)row.GetDoubleNullable(\"{prop.ColumnName}\")");
            }
            else if (prop.PropertyType == "System.Guid")
            {
                sb.Append($"Guid.Parse(row.GetString(\"{prop.ColumnName}\") ?? Guid.Empty.ToString())");
            }
            else if (prop.PropertyType.Contains("System.Guid?"))
            {
                sb.Append($"row.GetString(\"{prop.ColumnName}\") != null ? Guid.Parse(row.GetString(\"{prop.ColumnName}\")!) : (Guid?)null");
            }
            else
            {
                // Default to string
                sb.Append($"row.GetString(\"{prop.ColumnName}\") ?? string.Empty");
            }
        }

        private string GenerateEntityExtensions(EntityInfo entity)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using TursoSharp;");
            sb.AppendLine();
            sb.AppendLine($"namespace {entity.Namespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public static class {entity.ClassName}Extensions");
            sb.AppendLine("    {");
            
            // Generate ToSqlCreateTable extension
            if (entity.GenerateCreateTable)
            {
                sb.AppendLine($"        public static string ToSqlCreateTable(this {entity.ClassName} _)");
                sb.AppendLine("        {");
                sb.AppendLine($"            return @\"CREATE TABLE IF NOT EXISTS {entity.TableName} (");
                
                var columns = new List<string>();
                foreach (var prop in entity.Properties)
                {
                    var column = $"                {prop.ColumnName} {prop.SqlType}";
                    
                    if (prop.IsPrimaryKey)
                    {
                        column += " PRIMARY KEY";
                        if (prop.AutoIncrement)
                        {
                            column += " AUTOINCREMENT";
                        }
                    }
                    else if (!prop.IsNullable)
                    {
                        column += " NOT NULL";
                    }
                    
                    if (!string.IsNullOrEmpty(prop.DefaultValue))
                    {
                        column += $" DEFAULT {prop.DefaultValue}";
                    }
                    
                    columns.Add(column);
                }
                
                sb.AppendLine(string.Join(",\n", columns));
                sb.AppendLine("            )\";");
                sb.AppendLine("        }");
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private class TursoEntitySyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax classDeclaration &&
                    classDeclaration.AttributeLists.Count > 0)
                {
                    CandidateClasses.Add(classDeclaration);
                }
            }
        }

        private class EntityInfo
        {
            public string Namespace { get; set; } = "";
            public string ClassName { get; set; } = "";
            public string TableName { get; set; } = "";
            public bool GenerateRepository { get; set; }
            public bool GenerateAsync { get; set; }
            public bool GenerateCreateTable { get; set; }
            public List<PropertyInfo> Properties { get; } = new();
            public PropertyInfo? PrimaryKey { get; set; }
        }

        private class PropertyInfo
        {
            public string PropertyName { get; set; } = "";
            public string PropertyType { get; set; } = "";
            public string ColumnName { get; set; } = "";
            public string SqlType { get; set; } = "";
            public bool IsPrimaryKey { get; set; }
            public bool AutoIncrement { get; set; }
            public bool IsNullable { get; set; }
            public string? DefaultValue { get; set; }
            public bool IncludeInInsert { get; set; }
            public bool IncludeInUpdate { get; set; }
        }
    }
}