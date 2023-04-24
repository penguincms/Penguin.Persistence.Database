using Loxifi;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Penguin.Persistence.Database.Extensions;
using Penguin.Persistence.Database.Helpers;
using Penguin.Persistence.Database.Objects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Penguin.Persistence.Database
{
    /// <summary>
    /// An object used to provide slightly better access to a database than using simple ADO
    /// </summary>
    public class DatabaseInstance
    {
        private const string MULTIPLE_ROWS_MESSAGE = "Multiple rows returned for Query";

        /// <summary>
        /// A global command timeout in seconds
        /// </summary>
        public int CommandTimeout { get; set; }

        /// <summary>
        /// The connection string used when constructing this object
        /// </summary>
        public string ConnectionString { get; internal set; }

        /// <summary>
        /// Creates a new DatabaseInstance using the provided connection string
        /// </summary>
        /// <param name="connectionString">The connection string to use</param>
        /// <param name="commandTimeout">The global command timeout</param>
        public DatabaseInstance(string connectionString, int commandTimeout = 300)
        {
            ConnectionString = connectionString;
            CommandTimeout = commandTimeout;
            CommandBuilder = new TransientCommandBuilder(connectionString, commandTimeout);
        }

        public DatabaseInstance(string server, string database, int commandTimeout = 300) : this($"Server={server};Database={database};Trusted_Connection=True;", commandTimeout)
        {
        }

        public DatabaseInstance(string server, string database, string username, string password, int commandTimeout = 300) : this($"Server={server};Database={database};User Id={username};Password={password};", commandTimeout)
        {
        }

        /// <summary>
        /// Backs up an entire database schema to a file, including data
        /// </summary>
        /// <param name="ConnectionString">The database connection string</param>
        /// <param name="FileName">The file to output the data to</param>
        /// <param name="Compress"></param>
        public static void Backup(string ConnectionString, string FileName, bool Compress = false)
        {
            ConnectionString connection = new(ConnectionString);

            Console.WriteLine($"Starting backup of {connection.DataSource}\\{connection.Database}...");

            try
            {
                BackupOrTransfer(ConnectionString, (smoServer, transfer) =>
                {
                    transfer.Options.ToFileOnly = true;
                    transfer.Options.FileName = FileName;
                });
            }
            catch (Exception)
            {
                if (System.IO.File.Exists(FileName))
                {
                    System.IO.File.Delete(FileName);
                }

                throw;
            }

            if (Compress)
            {
                ScriptHelpers.CompressScript(FileName);
            }

            Console.WriteLine($"Backup completed.");
        }

        /// <summary>
        /// Ensures that procedure names always contain once set of braces so SQL parses them correctly
        /// </summary>
        /// <param name="ProdecureName">The name of the procedure to format</param>
        public static string FormatProcedure(string ProdecureName)
        {
            return ProdecureName is null
                ? throw new ArgumentNullException(nameof(ProdecureName))
                : "[" + ProdecureName.Trim('[').Trim(']') + "]";
        }

        /// <summary>
        /// Returns an SQL type representing the requested .Net Type
        /// </summary>
        /// <param name="type">The .Net type to get a value for</param>
        /// <returns>The equivalent SqlDbType to the provided .net type</returns>
        public static SqlDbType GetSqlType(Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type == typeof(string))
            {
                return SqlDbType.NVarChar;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            SqlParameter param = new("", Activator.CreateInstance(type));
            return param.SqlDbType;
        }

        /// <summary>
        /// Truncates the database and runs the SQL file at the provided path against the current database
        /// </summary>
        /// <param name="FileName">The file name to run</param>
        /// <param name="ConnectionString"></param>
        /// <param name="CommandTimeout"></param>
        /// <param name="SplitOn">The batch delimeter, defaults to "GO"</param>
        /// <param name="encoding"></param>
        public static async Task Restore(string FileName, string ConnectionString, int CommandTimeout = 300, string SplitOn = ScriptHelpers.DEFAULT_SPLIT, Encoding encoding = null)
        {
            encoding ??= Encoding.Default;

            TruncateDatabase(ConnectionString);
            await ScriptHelpers.RunSplitScript(FileName, ConnectionString, CommandTimeout, SplitOn, encoding).ConfigureAwait(false);
        }

        /// <summary>
        /// Transfers a database from one server to another
        /// </summary>
        public static void Transfer(string SourceConnectionString, string DestinationConnection)
        {
            ConnectionString sourceConnection = new(SourceConnectionString);
            ConnectionString destConnection = new(DestinationConnection);

            Console.WriteLine($"Starting backup of {sourceConnection.DataSource}\\{sourceConnection.Database}...");

            BackupOrTransfer(SourceConnectionString, (smoServer, transfer) =>
            {
                transfer.DestinationDatabase = destConnection.Database;
                transfer.DestinationServer = destConnection.DataSource;

                if (string.IsNullOrWhiteSpace(destConnection.UserName))
                {
                    //?
                }
                else
                {
                    transfer.DestinationLogin = destConnection.UserName;
                    transfer.DestinationPassword = destConnection.Password;
                    transfer.DestinationLoginSecure = false;
                }
            });

            Console.WriteLine($"Backup completed.");
        }

        /// <summary>
        /// Drops all non-security information from the database
        /// </summary>
        public static void TruncateDatabase(string ConnectionString)
        {
            using (SqlConnection connection = new(ConnectionString))
            {
                connection.Open();

                // Create the command and set its properties.
                using SqlCommand command = new()
                {
                    Connection = connection,
                    CommandText = ResourceHelper.ReadEmbeddedScript("TruncateDatabase.sql"),
                    CommandType = CommandType.Text,
                    CommandTimeout = 600000
                };
                // Open the connection and execute the reader.

                _ = command.ExecuteNonQuery();
            }
        }

        public static void Zip(string FileName, bool Delete = true)
        {
            string zipName = FileName + ".zip";

            if (System.IO.File.Exists(zipName))
            {
                System.IO.File.Delete(zipName);
            }

            using (ZipArchive zip = ZipFile.Open(zipName, ZipArchiveMode.Create))
            {
                _ = zip.CreateEntryFromFile(FileName, new System.IO.FileInfo(FileName).Name, CompressionLevel.Optimal);
            }

            if (Delete)
            {
                System.IO.File.Delete(FileName);
            }
        }

        public void Backup(string FileName, bool Compress = false)
        {
            Backup(ConnectionString, FileName, Compress);
        }

        /// <summary>
        /// Drops a stored procedure from the database
        /// </summary>
        /// <param name="ProcedureName">The name of the stored procedure to drop</param>
        public void DropProcedure(string ProcedureName)
        {
            string query = $"IF EXISTS ( SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{ProcedureName}') AND type IN ( N'P', N'PC' ) ) BEGIN DROP PROCEDURE {FormatProcedure(ProcedureName)} END";

            ExecuteSingleQuery(query);
        }

        /// <summary>
        /// Drops a table from the database
        /// </summary>
        /// <param name="TableName">The name of the table to drop</param>
        public void DropTable(string TableName)
        {
            ExecuteSingleQuery($"Drop table [{TableName}]");
        }

        /// <summary>
        /// Executes a string Query
        /// </summary>
        /// <param name="Query">The Query to execute</param>
        /// <param name="args">The ordered values of any "@i" formatted parameters to replace in the query</param>
        /// <returns>The number of rows affected</returns>
        public int Execute(string Query, params object[] args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            int affectedRows;

            SqlConnection conn = new(ConnectionString);
            using (SqlCommand command = new(Query, conn))
            {
                for (int i = 0; i < args.Length; i++)
                {
                    SqlParameter param = new($"@{i}", args[i]);

                    _ = command.Parameters.Add(param);
                }

                command.CommandTimeout = CommandTimeout;

                conn.Open();

                // create data adapter
                affectedRows = command.ExecuteNonQuery();

                conn.Close();
            }

            return affectedRows;
        }

        /// <summary>
        /// Runs the SQL file at the provided path against the current database
        /// </summary>
        /// <param name="FileName">The file name to run</param>
        /// <param name="SplitOn">The batch delimeter, defaults to "GO"</param>
        /// <param name="encoding">The encoding to read the file with</param>
        /// <param name="detectEncodingFromByteOrderMarks">Try to automatically detect the file encoding</param>
        /// <param name="bufferSize">The buffer size for the stream reader</param>
        public async Task ExecuteScript(string FileName, string SplitOn = ScriptHelpers.DEFAULT_SPLIT, Encoding encoding = null, bool detectEncodingFromByteOrderMarks = true, int bufferSize = -1)
        {
            encoding ??= Encoding.Default;

            await ScriptHelpers.RunSplitScript(FileName, ConnectionString, CommandTimeout, SplitOn, encoding, detectEncodingFromByteOrderMarks, bufferSize).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes a stored procedure by name
        /// </summary>
        /// <param name="ProcedureName">The name of the stored procedure to execute</param>

        public void ExecuteStoredProcedure(string ProcedureName)
        {
            using SqlConnection conn = new(ConnectionString);
            using SqlCommand command = new(ProcedureName, conn) { CommandType = CommandType.StoredProcedure };
            command.CommandTimeout = CommandTimeout;
            conn.Open();
            _ = command.ExecuteNonQuery();
        }

        /// <summary>
        /// Executes a stored procedure to a List
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        public IEnumerable<T> ExecuteStoredProcedureToList<T>(string ProcedureName, params string[] parameters)
        {
            foreach (object o in ExecuteStoredProcedureToList(ProcedureName, parameters))
            {
                yield return o.ToString().Convert<T>();
            }
        }

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        public IEnumerable<object> ExecuteStoredProcedureToList(string ProcedureName, params string[] parameters)
        {
            using DataTable dt = ExecuteStoredProcedureToTable(ProcedureName, parameters);
            foreach (DataRow dr in dt.Rows)
            {
                yield return dr[0];
            }
        }

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        public IEnumerable<T> ExecuteStoredProcedureToList<T>(string ProcedureName, params object[] parameters)
        {
            return ExecuteStoredProcedureToList<T>(ProcedureName, parameters.Select(s => s?.ToString()).ToArray());
        }

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        public IEnumerable<object> ExecuteStoredProcedureToList(string ProcedureName, params object[] parameters)
        {
            return ExecuteStoredProcedureToList(ProcedureName, parameters.Select(s => s?.ToString()).ToArray());
        }

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>A datatable containing the results of the execution</returns>

        public DataTable ExecuteStoredProcedureToTable(string ProcedureName, params string[] parameters)
        {
            using SqlConnection conn = new(ConnectionString);
            conn.Open();
            DataTable dt = new();

            using (SqlCommand cmd = new(ProcedureName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = CommandTimeout;

                foreach (SqlParameter parameter in FormatSqlParameters(ProcedureName, parameters))
                {
                    _ = cmd.Parameters.Add(parameter);
                }

                // create data adapter
                SqlDataAdapter da = new(cmd);
                // this will query your database and return the result to your datatable
                _ = da.Fill(dt);
                conn.Close();
                da.Dispose();
            }
            return dt;
        }

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>A datatable containing the results of the execution</returns>

        public DataTable ExecuteStoredProcedureToTable(string ProcedureName, List<SqlParameter> parameters)
        {
            if (ProcedureName is null)
            {
                throw new ArgumentNullException(nameof(ProcedureName));
            }

            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            FormatSqlParameters(ProcedureName, parameters);

            using SqlConnection conn = new(ConnectionString);
            conn.Open();
            DataTable dt = new();

            using (SqlCommand cmd = new(ProcedureName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = CommandTimeout;

                foreach (SqlParameter parameter in parameters)
                {
                    _ = cmd.Parameters.Add(parameter);
                }

                // create data adapter
                SqlDataAdapter da = new(cmd);
                // this will query your database and return the result to your datatable
                _ = da.Fill(dt);
                conn.Close();
                da.Dispose();
            }
            return dt;
        }

        /// <summary>
        /// Executes a stored procedure and returns a dictionary where the Key is the ColumnName and the Value is the ColumnValue, requires that the procedure returns only one row
        /// </summary>
        /// <param name="Query">The query text to execute</param>
        /// <param name="args">The ordered values of any "@i" formatted parameters to replace in the query</param>
        /// <returns>A dictionary representing the result of the query</returns>
        public Dictionary<string, string> ExecuteToDictionary(string Query, params object[] args)
        {
            using DataTable dt = ExecuteToTable(Query, args);
            Dictionary<string, string> toReturn = new();

            if (dt.Rows.Count > 1)
            {
                throw new Exception(MULTIPLE_ROWS_MESSAGE);
            }
            else if (dt.Rows.Count == 1)
            {
                foreach (DataColumn dataColumn in dt.Columns)
                {
                    object val = dt.Rows[0][dataColumn];

                    toReturn.Add(dataColumn.ColumnName, val?.ToString());
                }
            }
            return toReturn;
        }

        /// <summary>
        /// Executes a query to a List of complex objects, binding each row to an instance by property name
        /// </summary>
        /// <param name="query">The name of the procedure to execute</param>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        public IEnumerable<T> ExecuteToComplex<T>(string query) where T : class
        {
            Dictionary<string, PropertyInfo> cachedProps = typeof(T).GetProperties().ToDictionary(k => k.Name, v => v, StringComparer.OrdinalIgnoreCase);

            ConstructorInfo chosenConstructor = null;

            List<ConstructorInfo> constructors = typeof(T).GetConstructors().ToList();

            foreach (ConstructorInfo c in constructors.OrderByDescending(c => c.GetParameters().Length))
            {
                bool picked = true;

                foreach (ParameterInfo pi in c.GetParameters())
                {
                    if (!cachedProps.TryGetValue(pi.Name, out _))
                    {
                        picked = false;
                    }
                }

                if (picked)
                {
                    chosenConstructor = c;
                    break;
                }
            }

            if (chosenConstructor is null)
            {
                throw new Exception("No parameterless constructor defined");
            }

            using TransientCommand Command = CommandBuilder.Build(query);
            foreach (IDictionary<string, object> row in Command.GetReader().GetRows())
            {
                Dictionary<string, object> newObjDict = new(StringComparer.OrdinalIgnoreCase);

                foreach (string cName in row.Keys)
                {
                    if (cachedProps.TryGetValue(cName, out PropertyInfo pi))
                    {
                        newObjDict.Add(cName, row[cName]);
                    }
                }

                List<object> parameters = new();

                foreach (ParameterInfo pi in chosenConstructor.GetParameters())
                {
                    object parameterValue = newObjDict[pi.Name];

                    if(parameterValue is System.DBNull)
                    {
                        if (pi.ParameterType.IsClass)
                        {
                            parameterValue = null;
                        }
                        else
                        {
                            parameterValue = Activator.CreateInstance(pi.ParameterType);
                        }
                    }

                    parameters.Add(parameterValue);
                    _ = newObjDict.Remove(pi.Name);
                }

                T toReturn = Activator.CreateInstance(typeof(T), parameters.ToArray()) as T;

                foreach (PropertyInfo pi in cachedProps.Select(v => v.Value))
                {
                    if (newObjDict.TryGetValue(pi.Name, out object val) && val is not DBNull)
                    {
                        if (val is string sv && pi.PropertyType != typeof(string))
                        {
                            val = sv.Convert(pi.PropertyType);
                        }

                        pi.SetValue(toReturn, val);
                    }
                }

                yield return toReturn;
            }
        }

        /// <summary>
        /// Executes a query to a List
        /// </summary>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        public IEnumerable<T> Enumerate<T>(string query, params string[] args)
        {
            foreach (object o in Enumerate(query, args))
            {
                yield return o is T t ? t : o.ToString().Convert<T>();
            }
        }

        /// <summary>
        /// Executes a query to a List
        /// </summary>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        public IEnumerable<object> Enumerate(string query, params string[] args)
        {
            using TransientCommand Command = CommandBuilder.Build(query, args);
            SqlDataReader reader = Command.GetReader();

            if (reader.VisibleFieldCount > 1)
            {
                throw new InvalidArgumentException("SQL reader returned {reader.VisibleFieldCount} fields when 1 was expected");
            }

            foreach (IDictionary<string, object> row in reader.GetRows())
            {
                yield return row.Single().Value;
            }
        }

        private readonly TransientCommandBuilder CommandBuilder;
        /// <summary>
        /// Executes a query to a data table with optional parameters.
        /// </summary>
        /// <param name="Query">The query text to execute</param>
        /// <param name="args">The ordered values of any "@i" formatted parameters to replace in the query</param>
        /// <returns>A data table representing the results of the query</returns>

        public DataTable ExecuteToTable(string Query, params object[] args)
        {
            using TransientCommand command = CommandBuilder.Build(Query, args);
            DataTable dt = new();

            using (SqlDataAdapter da = command.GetDataAdapter())
            {
                _ = da.Fill(dt);
            }

            return dt;
        }

        /// <summary>
        /// Ensures that data coming from the client is convertable for SQL.
        /// Created because HTML5 posts datetime with a "T" in the middle which
        /// SQL doesn't like
        /// </summary>
        /// <param name="ProdecureName">The procedure name that we will be running to gather parameter information</param>
        /// <param name="parameters">The SqlParameter list to ensure compatability</param>
        public void FormatSqlParameters(string ProdecureName, List<SqlParameter> parameters)
        {
            if (parameters is null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            List<SQLParameterInfo> procParams = GetParameters(ProdecureName);

            foreach (SqlParameter sqlParameter in parameters)
            {
                sqlParameter.Value = FormatParameter(procParams.First(p => p.PARAMETER_NAME == sqlParameter.ParameterName), sqlParameter.Value);
            }
        }

        /// <summary>
        /// Ensures that data coming from the client is convertable for SQL.
        /// Created because HTML5 posts datetime with a "T" in the middle which
        /// SQL doesn't like
        /// </summary>
        /// <param name="ProdecureName">The procedure name that we will be running to gather parameter information</param>
        /// <param name="parameters">The parameter string list to ensure compatability</param>
        public IEnumerable<SqlParameter> FormatSqlParameters(string ProdecureName, IEnumerable<string> parameters)
        {
            List<SQLParameterInfo> procParams = GetParameters(ProdecureName);

            for (int i = 0; i < parameters.Count(); i++)
            {
                SQLParameterInfo procParam = procParams.ElementAt(i);
                yield return new SqlParameter()
                {
                    Value = FormatParameter(procParam, parameters.ElementAt(i))?.ToString(),
                    DbType = TypeConverter.ToDbType(procParam.DATA_TYPE),
                    ParameterName = procParam.PARAMETER_NAME,
                    SqlDbType = procParam.DATA_TYPE
                };
            }
        }

        /// <summary>
        /// Retrieves Parameter information for a given stored procedure
        /// </summary>
        /// <param name="Name">The name of the procedure to retrieve parameter information for</param>
        /// <returns>A List of SQLParameterInfo representing the parameter information</returns>
        public List<SQLParameterInfo> GetParameters(string Name)
        {
            using DataTable dt = ExecuteToTable("select * from information_schema.parameters where SPECIFIC_NAME = @0", Name);
            List<SQLParameterInfo> parameters = new();

            foreach (DataRow dr in dt.Rows)
            {
                SQLParameterInfo thisParam = new(dr);
                using (DataTable dti = ExecuteToTable("exec [Tools\\_GetParamDefault] @0, @1", Name, thisParam.PARAMETER_NAME))
                {
                    thisParam.DEFAULT = dti.GetSingle<string>().Trim('\'');
                }

                parameters.Add(thisParam);
            }

            return parameters.OrderBy(p => p.ORDINAL_POSITION).ToList();
        }

        /// <summary>
        /// Returns a list of all the stored procedures in the database
        /// </summary>
        /// <returns>A list of all the stored procedures in the database</returns>
        public List<string> GetStoredProcedures()
        {
            using DataTable dt = ExecuteToTable("select * from sys.procedures");
            return dt.All<string>("name");
        }

        /// <summary>
        /// Executes a query and returns the [0][0] cell as the requested type
        /// </summary>
        /// <typeparam name="T">The type to format the result as</typeparam>
        /// <param name="Query">The query text to execute</param>
        /// <param name="args">The ordered values of any "@i" formatted parameters to replace in the query</param>
        /// <returns>The [0][0] result converted to the requested type</returns>
        public T GetValue<T>(string Query, params object[] args)
        {
            using DataTable dt = ExecuteToTable(Query, args);
            return dt.Rows == null || dt.Rows.Count == 0 ? default : dt.Rows[0][0].ToString().Convert<T>();
        }

        /// <summary>
        /// Imports a datatable into a SQL table
        /// </summary>
        /// <param name="ToImport">The DataTable to import</param>
        /// <param name="TableName">The name to give the new SQL table</param>
        /// <param name="EmptyStringAsNull">If true, String.Empty will be set as null in the new table</param>

        public void Import(DataTable ToImport, string TableName, bool EmptyStringAsNull = true)
        {
            if (ToImport is null)
            {
                throw new ArgumentNullException(nameof(ToImport));
            }

            List<string> Commands = new(ToImport.Rows.Count + 1);

            StringBuilder CreateTableSrc = new();

            _ = CreateTableSrc.Append($"CREATE TABLE {TableName} (");

            List<string> Columns = new();
            List<string> ColumnParameters = new();

            foreach (DataColumn dc in ToImport.Columns)
            {
                string ColumnName = dc.ColumnName ?? "Column_" + ToImport.Columns.IndexOf(dc);

                ColumnParameters.Add($"[{ColumnName}] {GetStringForType(dc.DataType)}");
                Columns.Add($"[{ColumnName}]");
            };

            _ = CreateTableSrc.Append(string.Join(",", ColumnParameters));

            _ = CreateTableSrc.Append(')');

            Commands.Add(CreateTableSrc.ToString());
            _ = CreateTableSrc.Clear();

            //Switch to Parameter binding at least
            foreach (DataRow dr in ToImport.Rows)
            {
                _ = CreateTableSrc.Append($"insert into [{TableName}] ({string.Join(",", Columns)}) Values (");

                List<string> Values = new();

                foreach (object o in dr.ItemArray)
                {
                    Values.Add(o is null || (EmptyStringAsNull && string.IsNullOrWhiteSpace(o.ToString())) ? "null" : $"'{o.ToString().Replace("'", "''")}'");
                }

                _ = CreateTableSrc.Append(string.Join(", ", Values));

                _ = CreateTableSrc.Append(')');

                Commands.Add(CreateTableSrc.ToString());
                _ = CreateTableSrc.Clear();
            }

            using SqlConnection connection = new(ConnectionString);
            connection.Open();

            foreach (string queryString in Commands)
            {
                // Create the Command and Parameter objects.
                using SqlCommand command = new(queryString, connection)
                {
                    CommandTimeout = CommandTimeout
                };
                _ = command.ExecuteNonQuery();
            }
            connection.Close();
        }

        /// <summary>
        /// Drops an existing procedure with a matching name and then recreates it
        /// </summary>
        /// <param name="proc">The StoredProcedure object to use as the source for the new procedure</param>
        public void ImportProcedure(StoredProcedure proc)
        {
            if (proc is null)
            {
                throw new ArgumentNullException(nameof(proc));
            }

            DropProcedure(proc.Name);
            ExecuteSingleQuery(proc.Body);
        }

        /// <summary>
        /// Truncates the current database and runs the SQL file at the provided path against the current database
        /// </summary>
        /// <param name="FileName">The file name to run</param>
        /// <param name="SplitOn">The batch delimeter, defaults to "GO"</param>
        /// <param name="encoding"></param>
        public async Task Restore(string FileName, string SplitOn = ScriptHelpers.DEFAULT_SPLIT, Encoding encoding = null)
        {
            await Restore(FileName, ConnectionString, CommandTimeout, SplitOn, encoding ?? Encoding.Default).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a count of the rows in the given table
        /// </summary>
        /// <param name="TableName">The table containing the rows to enumerate</param>
        /// <returns>A count of the table rows</returns>
        public int TableCount(string TableName)
        {
            using DataTable dt = ExecuteToTable($"select count(*) from {FormatProcedure(TableName)}");
            return int.Parse(dt.Rows[0].ItemArray[0].ToString(), NumberStyles.Integer, CultureInfo.CurrentCulture);
        }

        internal static void BackupOrTransfer(string ConnectionString, Action<Server, Transfer> toRun)
        {
            ConnectionString connection = new(ConnectionString);

            ServerConnection serverConnection = new()
            {
                LoginSecure = false,
                ServerInstance = connection.DataSource,
                StatementTimeout = 0,
                LockTimeout = 0,
            };

            if (string.IsNullOrWhiteSpace(connection.UserName))
            {
                serverConnection.Authentication = SqlConnectionInfo.AuthenticationMethod.ActiveDirectoryIntegrated;
                serverConnection.TrustServerCertificate = true;
            }
            else
            {
                serverConnection.Login = connection.UserName;
                serverConnection.Password = connection.Password;
            }

            serverConnection.DatabaseName = connection.Database;

            Server smoServer = new(serverConnection);

            smoServer.ConnectionContext.LockTimeout = 0;
            smoServer.ConnectionContext.StatementTimeout = 0;

            if (smoServer.Version is null)
            {
                throw new Exception("Can't find the instance $Datasource");
            }

            Microsoft.SqlServer.Management.Smo.Database db = smoServer.Databases[serverConnection.DatabaseName];

            if (db is null)
            {
                throw new Exception("Can't find the database '$Database' in $Datasource");
            }

            Transfer transfer = new(db);

            toRun.Invoke(smoServer, transfer);

            transfer.Options.BatchSize = 100;

            transfer.Options.ScriptBatchTerminator = true;

            transfer.BulkCopyTimeout = 0;

            transfer.Options.ScriptData = true;
            transfer.Options.DriAll = true;
            transfer.Options.ClusteredIndexes = true;
            transfer.Options.FullTextCatalogs = true;
            transfer.Options.FullTextIndexes = true;
            transfer.Options.FullTextStopLists = true;
            transfer.Options.Indexes = true;
            transfer.Options.Triggers = true;

            transfer.DataTransferEvent += (sender, e) =>
            {
                try
                {
                    Console.WriteLine($"{e.DataTransferEventType}: {e.Message}");
                }
                catch (Exception) { }
            };

            Console.WriteLine($"Starting Transfer of {connection.DataSource}\\{connection.Database}...");
            _ = transfer.EnumScriptTransfer();
            Console.WriteLine($"Transfer completed.");
        }

        private static object FormatParameter(SQLParameterInfo dbParam, object ParamValue)
        {
            if (ParamValue is null)
            {
                return null;
            }

            if (dbParam.DATA_TYPE is SqlDbType.DateTime or SqlDbType.DateTime2)
            {
                return DateTime.Parse(ParamValue.ToString(), CultureInfo.CurrentCulture).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.CurrentCulture);
            }
            else if (dbParam.DATA_TYPE == SqlDbType.Date)
            {
                return DateTime.Parse(ParamValue.ToString(), CultureInfo.CurrentCulture).ToString("yyyy-MM-dd", CultureInfo.CurrentCulture);
            }

            return ParamValue;
        }

        private static string GetStringForType(Type type)
        {
            SqlDbType toReturn = type == null ? SqlDbType.NVarChar : GetSqlType(type);

            string postFix = string.Empty;

            switch (toReturn)
            {
                case SqlDbType.NVarChar:
                    postFix = "(max)";
                    break;

                default:
                    break;
            }

            return $"{toReturn}{postFix}";
        }

        private void ExecuteSingleQuery(string Text)
        {
            using SqlConnection connection = new(ConnectionString);
            connection.Open();

            // Create the Command and Parameter objects.
            using (SqlCommand command = new(Text, connection)
            {
                CommandTimeout = CommandTimeout
            })
            {
                _ = command.ExecuteNonQuery();
            }

            connection.Close();
        }
    }
}