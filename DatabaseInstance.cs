using Penguin.Persistence.Database.Extensions;
using Penguin.Reflection.Extensions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

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
        /// Returns an SQL type representing the requested .Net Type
        /// </summary>
        /// <param name="type">The .Net type to get a value for</param>
        /// <returns>The equivalent SqlDbType to the provided .net type</returns>
        public static SqlDbType GetSqlType(Type type)
        {
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
        public int Execute(string Query, params object[] args)
        {
            SqlConnection conn = new SqlConnection(this.ConnectionString);
            SqlCommand command = new SqlCommand(Query, conn);

            for (int i = 0; i < args.Length; i++)
            {
                SqlParameter param = new SqlParameter($"@{i}", args[i]);

                command.Parameters.Add(param);
            }

            command.CommandTimeout = this.CommandTimeout;

            conn.Open();

            // create data adapter
            int affectedRows = command.ExecuteNonQuery();

            conn.Close();

            return affectedRows;
        }

        /// <summary>
        /// Executes a stored procedure by name
        /// </summary>
        /// <param name="ProcedureName">The name of the stored procedure to execute</param>
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
        /// <returns>A datatable containing the results of the execution</returns>
        public DataTable ExecuteStoredProcedureToTable(string ProcedureName, List<SqlParameter> parameters)
        {
            FormatSqlParameters(ProcedureName, parameters);

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(ProcedureName, conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = this.CommandTimeout;

                foreach (SqlParameter parameter in parameters)
                {
                    cmd.Parameters.Add(parameter);
                }

                DataTable dt = new DataTable();
                // create data adapter
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                // this will query your database and return the result to your datatable
                da.Fill(dt);
                conn.Close();
                da.Dispose();

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
            DataTable dt = this.ExecuteToTable(Query, args);

            Dictionary<string, string> toReturn = new Dictionary<string, string>();

            if (dt.Rows.Count > 1)
            {
                throw new Exception("Multiple rows returned for Query");
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
        /// Executes a query to a data table with optional parameters.
        /// </summary>
        /// <param name="Query">The query text to execute</param>
        /// <param name="args">The ordered values of any "@i" formatted parameters to replace in the query</param>
        /// <returns>A data table representing the results of the query</returns>
        public DataTable ExecuteToTable(string Query, params object[] args)
        {
            DataTable dt = new DataTable();
            SqlConnection conn = new SqlConnection(this.ConnectionString);
            SqlCommand command = new SqlCommand(Query, conn);

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

            return dt;
        }

        /// <summary>
        /// Ensures that procedure names always contain once set of braces so SQL parses them correctly
        /// </summary>
        /// <param name="ProdecureName">The name of the procedure to format</param>
        public string FormatProcedure(string ProdecureName)
        {
            return "[" + ProdecureName.Trim('[').Trim(']') + "]";
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
            List<SQLParameterInfo> procParams = this.GetParameters(ProdecureName);

            foreach (SqlParameter sqlParameter in parameters)
            {
                SQLParameterInfo matchingDbParam = procParams.First(p => p.PARAMETER_NAME == sqlParameter.ParameterName);

                if (matchingDbParam.DATA_TYPE == SqlDbType.DateTime || matchingDbParam.DATA_TYPE == SqlDbType.DateTime2)
                {
                    sqlParameter.Value = DateTime.Parse(sqlParameter.Value.ToString()).ToString("yyyy-MM-dd HH:mm:ss.fff");
                }
            }
        }

        /// <summary>
        /// Retrieves Parameter information for a given stored procedure
        /// </summary>
        /// <param name="Name">The name of the procedure to retrieve parameter information for</param>
        /// <returns>A List of SQLParameterInfo representing the parameter information</returns>
        public List<SQLParameterInfo> GetParameters(string Name)
        {
            DataTable dt = ExecuteToTable("select * from information_schema.parameters where SPECIFIC_NAME = @0", Name);

            List<SQLParameterInfo> parameters = new List<SQLParameterInfo>();

            foreach (DataRow dr in dt.Rows)
            {
                SQLParameterInfo thisParam = new SQLParameterInfo(dr);
                thisParam.DEFAULT = this.ExecuteToTable("exec [Tools\\_GetParamDefault] @0, @1", Name, thisParam.PARAMETER_NAME).Single<string>().Trim('\'');

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
            return ExecuteToTable("select * from sys.procedures").All<string>("name");
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
            DataTable dt = ExecuteToTable(Query, args);

            if (dt.Rows == null || dt.Rows.Count == 0)
            {
                return default;
            }

            string val = dt.Rows[0][0].ToString();

            return val.Convert<T>();
        }

        /// <summary>
        /// Imports a datatable into a SQL table
        /// </summary>
        /// <param name="ToImport">The DataTable to import</param>
        /// <param name="TableName">The name to give the new SQL table</param>
        /// <param name="EmptyStringAsNull">If true, String.Empty will be set as null in the new table</param>
        public void Import(DataTable ToImport, string TableName, bool EmptyStringAsNull = true)
        {
            List<string> Commands = new List<string>(ToImport.Rows.Count + 1);

            StringBuilder CreateTableSrc = new StringBuilder();

            CreateTableSrc.Append($"CREATE TABLE {TableName} (");

            List<string> Columns = new List<string>();
            List<string> ColumnParameters = new List<string>();

            foreach (DataColumn dc in ToImport.Columns)
            {
                string ColumnName = dc.ColumnName ?? "Column_" + ToImport.Columns.IndexOf(dc);

                ColumnParameters.Add($"[{ColumnName}] {this.GetStringForType(dc.DataType)}");
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
                    SqlCommand command = new SqlCommand(queryString, connection)
                    {
                        CommandTimeout = this.CommandTimeout
                    };
                    command.ExecuteNonQuery();
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
            this.DropProcedure(proc.Name);
            this.ExecuteSingleQuery(proc.Body);
        }

        /// <summary>
        /// Returns a count of the rows in the given table
        /// </summary>
        /// <param name="TableName">The table containing the rows to enumerate</param>
        /// <returns>A count of the table rows</returns>
        public int TableCount(string TableName)
        {
            return int.Parse(ExecuteToTable($"select count(*) from {FormatProcedure(TableName)}").Rows[0].ItemArray[0].ToString());
        }

        private void ExecuteSingleQuery(string Text)
        {
            using (SqlConnection connection = new SqlConnection(this.ConnectionString))
            {
                connection.Open();

                // Create the Command and Parameter objects.
                SqlCommand command = new SqlCommand(Text, connection)
                {
                    CommandTimeout = this.CommandTimeout
                };

                command.ExecuteNonQuery();

                connection.Close();
            }
        }

        private string GetStringForType(Type type)
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
    }
}