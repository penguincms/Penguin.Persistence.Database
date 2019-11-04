using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Penguin.Debugging;
using Penguin.Persistence.Database.Extensions;
using Penguin.Persistence.Database.Helpers;
using Penguin.Reflection.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Penguin.Persistence.Database.Objects
{
    /// <summary>
    /// An object used to provide slightly better access to a database than using simple ADO
    /// </summary>
    public class DatabaseInstance
    {
        /// <summary>
        /// A global command timeout in seconds
        /// </summary>
        public int CommandTimeout { get; set; }

        /// <summary>
        /// The connection string used when constructing this object
        /// </summary>
        public string ConnectionString { get; internal set; }

        private const string MultipleRowsMessage = "Multiple rows returned for Query";

        /// <summary>
        /// Creates a new DatabaseInstance using the provided connection string
        /// </summary>
        /// <param name="connectionString">The connection string to use</param>
        /// <param name="commandTimeout">The global command timeout</param>
        public DatabaseInstance(string connectionString, int commandTimeout = 300)
        {
            this.ConnectionString = connectionString;
            this.CommandTimeout = commandTimeout;
        }

        /// <summary>
        /// Backs up an entire database schema to a file, including data
        /// </summary>
        /// <param name="ConnectionString">The database connection string</param>
        /// <param name="FileName">The file to output the data to</param>
        public static void Backup(string ConnectionString, string FileName)
        {
            ConnectionString connection = new ConnectionString(ConnectionString);

            ServerConnection serverConnection = new ServerConnection
            {
                LoginSecure = false,
                ServerInstance = connection.DataSource
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

            Server smoServer = new Server(serverConnection);

            if (smoServer.Version is null)
            {
                throw new Exception("Can't find the instance $Datasource");
            }

            Microsoft.SqlServer.Management.Smo.Database db = smoServer.Databases[serverConnection.DatabaseName];

            if (db is null)
            {
                throw new Exception("Can't find the database '$Database' in $Datasource");
            }

            Transfer transfer = new Transfer(db);

            transfer.Options.BatchSize = 100;

            transfer.Options.ScriptBatchTerminator = true;
            transfer.Options.ToFileOnly = true;
            transfer.Options.FileName = FileName;
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

            Console.WriteLine($"Starting backup of {connection.DataSource}\\{connection.Database}...");
            transfer.EnumScriptTransfer();
            Console.WriteLine($"Backup completed.");
        }

        /// <summary>
        /// Ensures that procedure names always contain once set of braces so SQL parses them correctly
        /// </summary>
        /// <param name="ProdecureName">The name of the procedure to format</param>
        public static string FormatProcedure(string ProdecureName)
        {
            Contract.Requires(!string.IsNullOrWhiteSpace(ProdecureName));
            return "[" + ProdecureName.Trim('[').Trim(']') + "]";
        }

        /// <summary>
        /// Returns an SQL type representing the requested .Net Type
        /// </summary>
        /// <param name="type">The .Net type to get a value for</param>
        /// <returns>The equivalent SqlDbType to the provided .net type</returns>
        public static SqlDbType GetSqlType(Type type)
        {
            Contract.Requires(type != null);

            if (type == typeof(string))
            {
                return SqlDbType.NVarChar;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            SqlParameter param = new SqlParameter("", Activator.CreateInstance(type));
            return param.SqlDbType;
        }

        /// <summary>
        /// Drops a stored procedure from the database
        /// </summary>
        /// <param name="ProcedureName">The name of the stored procedure to drop</param>
        public void DropProcedure(string ProcedureName)
        {
            string query = $"IF EXISTS ( SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'{ProcedureName}') AND type IN ( N'P', N'PC' ) ) BEGIN DROP PROCEDURE {FormatProcedure(ProcedureName)} END";

            this.ExecuteSingleQuery(query);
        }

        /// <summary>
        /// Drops a table from the database
        /// </summary>
        /// <param name="TableName">The name of the table to drop</param>
        public void DropTable(string TableName) => this.ExecuteSingleQuery($"Drop table [{TableName}]");

        /// <summary>
        /// Executes a string Query
        /// </summary>
        /// <param name="Query">The Query to execute</param>
        /// <param name="args">The ordered values of any "@i" formatted parameters to replace in the query</param>
        /// <returns>The number of rows affected</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public int Execute(string Query, params object[] args)
        {
            int affectedRows;

            SqlConnection conn = new SqlConnection(this.ConnectionString);
            using (SqlCommand command = new SqlCommand(Query, conn))
            {
                for (int i = 0; i < args.Length; i++)
                {
                    SqlParameter param = new SqlParameter($"@{i}", args[i]);

                    command.Parameters.Add(param);
                }

                command.CommandTimeout = this.CommandTimeout;

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
            await ScriptHelpers.RunSplitScript(FileName, ConnectionString, CommandTimeout, SplitOn, encoding, detectEncodingFromByteOrderMarks, bufferSize);
        }

        /// <summary>
        /// Executes a stored procedure by name
        /// </summary>
        /// <param name="ProcedureName">The name of the stored procedure to execute</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public void ExecuteStoredProcedure(string ProcedureName)
        {
            using (SqlConnection conn = new SqlConnection(this.ConnectionString))
            {
                using (SqlCommand command = new SqlCommand(ProcedureName, conn) { CommandType = CommandType.StoredProcedure })
                {
                    command.CommandTimeout = this.CommandTimeout;
                    conn.Open();
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public IEnumerable<object> ExecuteStoredProcedureToList(string ProcedureName, params string[] parameters)
        {
            using (DataTable dt = ExecuteStoredProcedureToTable(ProcedureName, parameters))
            {
                foreach (DataRow dr in dt.Rows)
                {
                    yield return dr[0];
                }
            }
        }

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public IEnumerable<T> ExecuteStoredProcedureToList<T>(string ProcedureName, params object[] parameters) => ExecuteStoredProcedureToList<T>(ProcedureName, parameters.Select(s => s?.ToString()).ToArray());

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>An IEnumerable of object representing the first value of each row </returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public IEnumerable<object> ExecuteStoredProcedureToList(string ProcedureName, params object[] parameters) => ExecuteStoredProcedureToList(ProcedureName, parameters.Select(s => s?.ToString()).ToArray());

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>A datatable containing the results of the execution</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public DataTable ExecuteStoredProcedureToTable(string ProcedureName, params string[] parameters)
        {
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                DataTable dt = new DataTable();

                using (SqlCommand cmd = new SqlCommand(ProcedureName, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = this.CommandTimeout;

                    foreach (SqlParameter parameter in FormatSqlParameters(ProcedureName, parameters))
                    {
                        cmd.Parameters.Add(parameter);
                    }

                    // create data adapter
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    // this will query your database and return the result to your datatable
                    da.Fill(dt);
                    conn.Close();
                    da.Dispose();
                }
                return dt;
            }
        }

        /// <summary>
        /// Executes a stored procedure to a datatable
        /// </summary>
        /// <param name="ProcedureName">The name of the procedure to execute</param>
        /// <param name="parameters">The parameters to pass into the stored procedure</param>
        /// <returns>A datatable containing the results of the execution</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public DataTable ExecuteStoredProcedureToTable(string ProcedureName, List<SqlParameter> parameters)
        {
            Contract.Requires(parameters != null);

            FormatSqlParameters(ProcedureName, parameters);

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                DataTable dt = new DataTable();

                using (SqlCommand cmd = new SqlCommand(ProcedureName, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandTimeout = this.CommandTimeout;

                    foreach (SqlParameter parameter in parameters)
                    {
                        cmd.Parameters.Add(parameter);
                    }

                    // create data adapter
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    // this will query your database and return the result to your datatable
                    da.Fill(dt);
                    conn.Close();
                    da.Dispose();
                }
                return dt;
            }
        }

        /// <summary>
        /// Executes a stored procedure and returns a dictionary where the Key is the ColumnName and the Value is the ColumnValue, requires that the procedure returns only one row
        /// </summary>
        /// <param name="Query">The query text to execute</param>
        /// <param name="args">The ordered values of any "@i" formatted parameters to replace in the query</param>
        /// <returns>A dictionary representing the result of the query</returns>
        public Dictionary<string, string> ExecuteToDictionary(string Query, params object[] args)
        {
            using (DataTable dt = this.ExecuteToTable(Query, args))
            {
                Dictionary<string, string> toReturn = new Dictionary<string, string>();

                if (dt.Rows.Count > 1)
                {
                    throw new Exception(MultipleRowsMessage);
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
        }

        /// <summary>
        /// Executes a query to a data table with optional parameters.
        /// </summary>
        /// <param name="Query">The query text to execute</param>
        /// <param name="args">The ordered values of any "@i" formatted parameters to replace in the query</param>
        /// <returns>A data table representing the results of the query</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public DataTable ExecuteToTable(string Query, params object[] args)
        {
            DataTable dt = new DataTable();
            SqlConnection conn = new SqlConnection(this.ConnectionString);
            using (SqlCommand command = new SqlCommand(Query, conn))
            {
                for (int i = 0; i < args.Length; i++)
                {
                    SqlParameter param = new SqlParameter($"@{i}", args[i]);

                    command.Parameters.Add(param);
                }
                command.CommandTimeout = this.CommandTimeout;

                conn.Open();

                // create data adapter
                SqlDataAdapter da = new SqlDataAdapter(command);
                // this will query your database and return the result to your datatable
                da.Fill(dt);
                conn.Close();
                da.Dispose();
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
            Contract.Requires(parameters != null);

            List<SQLParameterInfo> procParams = this.GetParameters(ProdecureName);

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
            Contract.Requires(parameters != null);

            List<SQLParameterInfo> procParams = this.GetParameters(ProdecureName);

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
            using (DataTable dt = ExecuteToTable("select * from information_schema.parameters where SPECIFIC_NAME = @0", Name))
            {
                List<SQLParameterInfo> parameters = new List<SQLParameterInfo>();

                foreach (DataRow dr in dt.Rows)
                {
                    SQLParameterInfo thisParam = new SQLParameterInfo(dr);
                    using (DataTable dti = this.ExecuteToTable("exec [Tools\\_GetParamDefault] @0, @1", Name, thisParam.PARAMETER_NAME))
                    {
                        thisParam.DEFAULT = dti.GetSingle<string>().Trim('\'');
                    }

                    parameters.Add(thisParam);
                }

                return parameters.OrderBy(p => p.ORDINAL_POSITION).ToList();
            }
        }

        /// <summary>
        /// Returns a list of all the stored procedures in the database
        /// </summary>
        /// <returns>A list of all the stored procedures in the database</returns>
        public List<string> GetStoredProcedures()
        {
            using (DataTable dt = ExecuteToTable("select * from sys.procedures"))
            {
                return dt.All<string>("name");
            }
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
            using (DataTable dt = ExecuteToTable(Query, args))
            {
                if (dt.Rows == null || dt.Rows.Count == 0)
                {
                    return default;
                }

                return dt.Rows[0][0].ToString().Convert<T>();
            }
        }

        /// <summary>
        /// Imports a datatable into a SQL table
        /// </summary>
        /// <param name="ToImport">The DataTable to import</param>
        /// <param name="TableName">The name to give the new SQL table</param>
        /// <param name="EmptyStringAsNull">If true, String.Empty will be set as null in the new table</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        public void Import(DataTable ToImport, string TableName, bool EmptyStringAsNull = true)
        {
            Contract.Requires(ToImport != null);

            List<string> Commands = new List<string>(ToImport.Rows.Count + 1);

            StringBuilder CreateTableSrc = new StringBuilder();

            CreateTableSrc.Append($"CREATE TABLE {TableName} (");

            List<string> Columns = new List<string>();
            List<string> ColumnParameters = new List<string>();

            foreach (DataColumn dc in ToImport.Columns)
            {
                string ColumnName = dc.ColumnName ?? "Column_" + ToImport.Columns.IndexOf(dc);

                ColumnParameters.Add($"[{ColumnName}] {GetStringForType(dc.DataType)}");
                Columns.Add($"[{ColumnName}]");
            };

            CreateTableSrc.Append(string.Join(",", ColumnParameters));

            CreateTableSrc.Append(")");

            Commands.Add(CreateTableSrc.ToString());
            CreateTableSrc.Clear();

            //Switch to Parameter binding at least
            foreach (DataRow dr in ToImport.Rows)
            {
                CreateTableSrc.Append($"insert into [{TableName}] ({string.Join(",", Columns)}) Values (");

                List<string> Values = new List<string>();

                foreach (object o in dr.ItemArray)
                {
                    Values.Add(o is null || (EmptyStringAsNull && string.IsNullOrWhiteSpace(o.ToString())) ? "null" : $"'{o.ToString().Replace("'", "''")}'");
                }

                CreateTableSrc.Append(string.Join(", ", Values));

                CreateTableSrc.Append(")");

                Commands.Add(CreateTableSrc.ToString());
                CreateTableSrc.Clear();
            }

            using (SqlConnection connection = new SqlConnection(this.ConnectionString))
            {
                connection.Open();

                foreach (string queryString in Commands)
                {
                    // Create the Command and Parameter objects.
                    using (SqlCommand command = new SqlCommand(queryString, connection)
                    {
                        CommandTimeout = this.CommandTimeout
                    })
                    {
                        command.ExecuteNonQuery();
                    }
                }
                connection.Close();
            }
        }

        /// <summary>
        /// Drops an existing procedure with a matching name and then recreates it
        /// </summary>
        /// <param name="proc">The StoredProcedure object to use as the source for the new procedure</param>
        public void ImportProcedure(StoredProcedure proc)
        {
            Contract.Requires(proc != null);
            this.DropProcedure(proc.Name);
            this.ExecuteSingleQuery(proc.Body);
        }

        /// <summary>
        /// Truncates the current database and runs the SQL file at the provided path against the current database
        /// </summary>
        /// <param name="FileName">The file name to run</param>
        /// <param name="SplitOn">The batch delimeter, defaults to "GO"</param>
        public async Task Restore(string FileName, string SplitOn = ScriptHelpers.DEFAULT_SPLIT)
        {
            TruncateDatabase();
            await ScriptHelpers.RunSplitScript(FileName, ConnectionString, CommandTimeout, SplitOn);
        }

        /// <summary>
        /// Returns a count of the rows in the given table
        /// </summary>
        /// <param name="TableName">The table containing the rows to enumerate</param>
        /// <returns>A count of the table rows</returns>
        public int TableCount(string TableName)
        {
            using (DataTable dt = ExecuteToTable($"select count(*) from {FormatProcedure(TableName)}"))
            {
                return int.Parse(dt.Rows[0].ItemArray[0].ToString(), NumberStyles.Integer, CultureInfo.CurrentCulture);
            }
        }

        /// <summary>
        /// Drops all non-security information from the database
        /// </summary>
        public void TruncateDatabase()
        {
            StaticLogger.Log("Truncating database...");
            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();

                // Create the command and set its properties.
                using (SqlCommand command = new SqlCommand
                {
                    Connection = connection,
                    CommandText = ResourceHelper.ReadEmbeddedScript("TruncateDatabase.sql"),
                    CommandType = CommandType.Text,
                    CommandTimeout = 600000
                })
                {
                    // Open the connection and execute the reader.

                    command.ExecuteNonQuery();
                }
            }
            StaticLogger.Log("Truncating Complete.");
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<Pending>")]
        private void ExecuteSingleQuery(string Text)
        {
            using (SqlConnection connection = new SqlConnection(this.ConnectionString))
            {
                connection.Open();

                // Create the Command and Parameter objects.
                using (SqlCommand command = new SqlCommand(Text, connection)
                {
                    CommandTimeout = this.CommandTimeout
                })
                {
                    command.ExecuteNonQuery();
                }

                connection.Close();
            }
        }

        private object FormatParameter(SQLParameterInfo dbParam, object ParamValue)
        {
            if(ParamValue is null)
            {
                return null;
            }

            if (dbParam.DATA_TYPE == SqlDbType.DateTime || dbParam.DATA_TYPE == SqlDbType.DateTime2)
            {
                return DateTime.Parse(ParamValue.ToString(), CultureInfo.CurrentCulture).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.CurrentCulture);
            }

            return ParamValue;
        }
    }
}