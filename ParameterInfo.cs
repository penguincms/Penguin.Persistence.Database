using Penguin.Persistence.Database.Extensions;
using System.Data;

namespace Penguin.Persistence.Database
{
    /// <summary>
    /// Parameter info pulled from an SQL database. No further comments since I dont even know what most of these are
    /// </summary>
    public class SQLParameterInfo
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        public string AS_LOCATOR { get; set; }

        public int? CHARACTER_MAXIMUM_LENGTH { get; set; }

        public int? CHARACTER_OCTET_LENGTH { get; set; }

        public string CHARACTER_SET_CATALOG { get; set; }

        public string CHARACTER_SET_NAME { get; set; }

        public string CHARACTER_SET_SCHEMA { get; set; }

        public string COLLATION_CATALOG { get; set; }

        public string COLLATION_NAME { get; set; }

        public string COLLATION_SCHEMA { get; set; }

        public SqlDbType DATA_TYPE { get; set; }

        public int? DATETIME_PRECISION { get; set; }

        public string DEFAULT { get; set; }

        public bool HAS_DEFAULT => DEFAULT != "NO DEFAULT SPECIFIED";

        public string INTERVAL_PRECISION { get; set; }

        public string INTERVAL_TYPE { get; set; }

        public string IS_RESULT { get; set; }

        public int? NUMERIC_PRECISION { get; set; }

        public int? NUMERIC_PRECISION_RADIX { get; set; }

        public int? NUMERIC_SCALE { get; set; }

        public int? ORDINAL_POSITION { get; set; }

        public string PARAMETER_MODE { get; set; }

        public string PARAMETER_NAME { get; set; }

        public string SCOPE_CATALOG { get; set; }

        public string SCOPE_NAME { get; set; }

        public string SCOPE_SCHEMA { get; set; }

        public string SPECIFIC_CATALOG { get; set; }

        public string SPECIFIC_NAME { get; set; }

        public string SPECIFIC_SCHEMA { get; set; }

        public string USER_DEFINED_TYPE_CATALOG { get; set; }

        public string USER_DEFINED_TYPE_NAME { get; set; }

        public string USER_DEFINED_TYPE_SCHEMA { get; set; }

        public SQLParameterInfo(DataRow dr)
        {
            SPECIFIC_CATALOG = dr.Value<string>(ParameterTableColumns.SPECIFIC_CATALOG);
            SPECIFIC_SCHEMA = dr.Value<string>(ParameterTableColumns.SPECIFIC_SCHEMA);
            SPECIFIC_NAME = dr.Value<string>(ParameterTableColumns.SPECIFIC_NAME);
            ORDINAL_POSITION = dr.Value<int?>(ParameterTableColumns.ORDINAL_POSITION);
            PARAMETER_MODE = dr.Value<string>(ParameterTableColumns.PARAMETER_MODE);
            IS_RESULT = dr.Value<string>(ParameterTableColumns.IS_RESULT);
            AS_LOCATOR = dr.Value<string>(ParameterTableColumns.AS_LOCATOR);
            PARAMETER_NAME = dr.Value<string>(ParameterTableColumns.PARAMETER_NAME);
            DATA_TYPE = dr.Value<SqlDbType>(ParameterTableColumns.DATA_TYPE, true);
            CHARACTER_MAXIMUM_LENGTH = dr.Value<int?>(ParameterTableColumns.CHARACTER_MAXIMUM_LENGTH);
            CHARACTER_OCTET_LENGTH = dr.Value<int?>(ParameterTableColumns.CHARACTER_OCTET_LENGTH);
            COLLATION_CATALOG = dr.Value<string>(ParameterTableColumns.COLLATION_CATALOG);
            COLLATION_SCHEMA = dr.Value<string>(ParameterTableColumns.COLLATION_SCHEMA);
            COLLATION_NAME = dr.Value<string>(ParameterTableColumns.COLLATION_NAME);
            CHARACTER_SET_CATALOG = dr.Value<string>(ParameterTableColumns.CHARACTER_SET_CATALOG);
            CHARACTER_SET_SCHEMA = dr.Value<string>(ParameterTableColumns.CHARACTER_SET_SCHEMA);
            CHARACTER_SET_NAME = dr.Value<string>(ParameterTableColumns.CHARACTER_SET_NAME);
            NUMERIC_PRECISION = dr.Value<int?>(ParameterTableColumns.NUMERIC_PRECISION);
            NUMERIC_PRECISION_RADIX = dr.Value<int?>(ParameterTableColumns.NUMERIC_PRECISION_RADIX);
            NUMERIC_SCALE = dr.Value<int?>(ParameterTableColumns.NUMERIC_SCALE);
            DATETIME_PRECISION = dr.Value<int?>(ParameterTableColumns.DATETIME_PRECISION);
            INTERVAL_TYPE = dr.Value<string>(ParameterTableColumns.INTERVAL_TYPE);
            INTERVAL_PRECISION = dr.Value<string>(ParameterTableColumns.INTERVAL_PRECISION);
            USER_DEFINED_TYPE_CATALOG = dr.Value<string>(ParameterTableColumns.USER_DEFINED_TYPE_CATALOG);
            USER_DEFINED_TYPE_SCHEMA = dr.Value<string>(ParameterTableColumns.USER_DEFINED_TYPE_SCHEMA);
            USER_DEFINED_TYPE_NAME = dr.Value<string>(ParameterTableColumns.USER_DEFINED_TYPE_NAME);
            SCOPE_CATALOG = dr.Value<string>(ParameterTableColumns.SCOPE_CATALOG);
            SCOPE_SCHEMA = dr.Value<string>(ParameterTableColumns.SCOPE_SCHEMA);
            SCOPE_NAME = dr.Value<string>(ParameterTableColumns.SCOPE_NAME);
        }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}