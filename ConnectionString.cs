using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Penguin.Persistence.Database
{
    /// <summary>
    /// A class to convert a connection string into a solid object
    /// </summary>
    public class ConnectionString
    {
        /// <summary>
        /// A class representing the result of an attempt to validate a connection string
        /// </summary>
        public class TestResult
        {
            /// <summary>
            /// Any error occured while attempting to validate
            /// </summary>
            public Exception Error { get; set; }

            /// <summary>
            /// Whether or not the connection attempt was successfull
            /// </summary>
            public bool Success => Error == null;

            internal TestResult()
            {
            }

            internal TestResult(Exception ex)
            {
                Error = ex;
            }
        }

        private static readonly string[] databaseAliases = { "database", "initial catalog" };

        private static readonly string[] passwordAliases = { "password", "pwd" };

        private static readonly string[] serverAliases = { "server", "host", "data source", "datasource", "address", "addr", "network address" };

        private static readonly string[] usernameAliases = { "user id", "uid", "username", "user name", "user" };

        /// <summary>
        /// The database name for the connection string
        /// </summary>
        public string Database => GetAliasedValue(databaseAliases);

        /// <summary>
        /// The data source (or server) name for the connection string
        /// </summary>
        public string DataSource => GetAliasedValue(serverAliases);

        /// <summary>
        /// The password used to access this data source
        /// </summary>
        public string Password => GetAliasedValue(passwordAliases);

        /// <summary>
        /// The user name used to access this data source
        /// </summary>
        public string UserName => GetAliasedValue(usernameAliases);

        private string connectionString { get; set; }

        private Dictionary<string, string> ConnectionStringDictionary { get; set; }

        /// <summary>
        /// Creates a new instance of this object using the provided connection string
        /// </summary>
        /// <param name="connectionStringToTest">The connection string to be parsed</param>
        public ConnectionString(string connectionStringToTest)
        {
            connectionString = connectionStringToTest ?? throw new ArgumentNullException(nameof(connectionStringToTest));
            ConnectionStringDictionary = connectionStringToTest.Split(';')
                                         .Where(kvp => kvp.Contains('='))
                                         .Select(kvp => kvp.Split(new char[] { '=' }, 2))
                                         .ToDictionary(kvp => kvp[0].Trim(),
                                                      kvp => kvp[1].Trim(),
                                                      StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// Attempts to use regex to retrieve connection strings from a file
        /// </summary>
        /// <param name="FilePath">The path to the file</param>
        /// <returns>A list of connection string objects found in the file</returns>
        public static List<ConnectionString> FromFile(string FilePath)
        {
            List<ConnectionString> toReturn = new();

            string FileContents = File.ReadAllText(FilePath);

            Regex connectionString = new("(?i)ConnectionString=\"(.*?)\"");

            foreach (Match ThisMatch in connectionString.Matches(FileContents))
            {
                foreach (Group thisGroup in ThisMatch.Groups)
                {
                    if (!thisGroup.Value.Contains('"'))
                    {
                        toReturn.Add(new ConnectionString(thisGroup.Value));
                    }
                }
            }

            return toReturn;
        }

        /// <summary>
        /// Tests a connection string using SqlConnection and returns a test result representing the status
        /// </summary>
        /// <param name="connectionStringToTest">The connection string to test</param>
        /// <returns>The rest result</returns>
        public static TestResult Test(string connectionStringToTest)
        {
            try
            {
                using SqlConnection con = new(connectionStringToTest);
                con.Open();
                con.Close();

                return new TestResult();
            }
            catch (Exception ex)
            {
                return new TestResult(ex);
            }
        }

        /// <summary>
        /// Attempts to test this connection string using SqlConnection
        /// </summary>
        /// <returns></returns>
        public TestResult Test()
        {
            return Test(connectionString);
        }

        private string GetAliasedValue(string[] aliases)
        {
            for (int i = 0; i < aliases.Length; i++)
            {
                if (ConnectionStringDictionary.TryGetValue(aliases[i], out string value))
                {
                    return value;
                }
            }
            return string.Empty;
        }
    }
}