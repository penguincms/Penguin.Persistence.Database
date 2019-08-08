﻿using Penguin.Extensions.String;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;

namespace Penguin.Persistence.Database
{
    /// <summary>
    /// Used to extract data from the creation script for a stored procedure
    /// </summary>
    public class StoredProcedure
    {
        #region Properties

        /// <summary>
        /// The body of the script
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// Any additional connection strings that were found in the script using the format --@using CONNECTION_STRING
        /// </summary>
        public List<string> ConnectionStrings { get; set; }

        /// <summary>
        /// The name of the stored procedure as found in the source script
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Definitions for the parameters of this stored procedure
        /// </summary>
        public SqlParameter[] Parameters { get; set; }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Constructs an empty instance of this class
        /// </summary>
        public StoredProcedure()
        {
        }

        /// <summary>
        /// Constructs an instance of this class and attempts to parse the procedure text so that it can be accessed
        /// </summary>
        /// <param name="Script">The creation script for the stored procedure</param>
        public StoredProcedure(string Script)
        {
            ConnectionStrings = new List<string>();

            this.Body = this.RemoveComments(Script).From("create procedure ", true, StringComparison.CurrentCultureIgnoreCase).ToLast("\nGO", false, StringComparison.CurrentCultureIgnoreCase).Trim();

            List<string> Commands = Script.Split('\n').Where(s => s.Trim().StartsWith("--@")).Select(s => s.From("@")).ToList();

            foreach (string Command in Commands)
            {
                if (Command.To(" ").ToLower() == "using")
                {
                    this.ConnectionStrings.Add(Command.Trim().From(" ").Trim());
                }
            }

            Script = this.RemoveComments(Script);

            this.Name = Script.From("create procedure", false, StringComparison.CurrentCultureIgnoreCase).Trim().To(" ").To("\n").To("\r").Trim();

            string ParametersSection = Script.From("create procedure", false, StringComparison.CurrentCultureIgnoreCase).Trim().From(this.Name).Trim();

            string[] Chunks = ParametersSection.Split(',');

            List<string> Parameters = new List<string>();

            foreach (string Parameter in Chunks)
            {
                string WorkingParameter = Parameter;

                bool LastParameter = false;

                Match match = Regex.Match(Parameter, LAST_PARAMETER_DELIMETER, RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    LastParameter = true;

                    WorkingParameter = WorkingParameter.ToLower().Substring(0, match.Index);

                    if (string.IsNullOrWhiteSpace(WorkingParameter))
                    { break; }
                }

                SqlParameter thisParameter = new SqlParameter
                {
                    ParameterName = WorkingParameter.From("@").To(" ")
                };

                string ParameterTypeName = WorkingParameter.From("@").Trim().From(" ").Trim().To(" ");

                if (ParameterTypeName.Contains("("))
                {
                    ParameterTypeName = ParameterTypeName.To("(");
                }

                thisParameter.SqlDbType = Enum.GetValues(typeof(SqlDbType)).Cast<SqlDbType>().First(t => string.Equals(t.ToString(), ParameterTypeName, StringComparison.OrdinalIgnoreCase));

                if (WorkingParameter.Contains("="))
                {
                    thisParameter.IsNullable = true;
                }

                if (LastParameter)
                { break; }
            }
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Alters the name of the procedure, both property and body of the script
        /// </summary>
        /// <param name="newName">The new name to give the procedure</param>
        public void RenameProcedure(string newName)
        {
            string parsedNewName = "[" + newName.Trim('[').Trim(']') + "]";

            int NameIndex = Body.AllIndexesOf(this.Name).First(i => i > Body.IndexOf("create procedure ", StringComparison.OrdinalIgnoreCase));

            Body = Body.Substring(0, NameIndex) + parsedNewName + Body.Substring(NameIndex + this.Name.Length);

            this.Name = parsedNewName;
        }

        #endregion Methods

        #region Fields

        private const string LAST_PARAMETER_DELIMETER = @"([\s\r\n]+|^)as[\s\r\n]+(begin|set)[\s\r\n]+";

        #endregion Fields

        private string RemoveComments(string intext)
        {
            string[] lines = intext.Split('\n').Select(s => s.Trim()).ToArray();

            bool inQuote = false;
            bool inComment = false;

            for (int i = 0; i < lines.Count(); i++)
            {
                string newLine = string.Empty;

                for (int c = 0; c < lines[i].Length; c++)
                {
                    if (lines[i][c] == '\'')
                    {
                        inQuote = !inQuote;
                    }
                    else if (lines[i].Length > c + 1)
                    {
                        if (lines[i][c] == '/' && lines[i][c + 1] == '*')
                        {
                            inComment = true;
                        }
                        else if (lines[i][c] == '*' && lines[i][c + 1] == '/')
                        {
                            c++;
                            inComment = false;
                            continue;
                        }
                    }

                    if (!inQuote && c != lines[i].Length - 1 && lines[i][c] == '-' && lines[i][c + 1] == '-')
                    {
                        break;
                    }

                    if (!inComment)
                    {
                        newLine += lines[i][c];
                    }
                }

                lines[i] = newLine;
            }

            return string.Join("\n", lines);
        }
    }
}