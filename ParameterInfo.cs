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
        public bool HAS_DEFAULT => this.DEFAULT != "NO DEFAULT SPECIFIED";
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
            this.SPECIFIC_CATALOG = dr.Value<string>(ParameterTableColumns.SPECIFIC_CATALOG);
            this.SPECIFIC_SCHEMA = dr.Value<string>(ParameterTableColumns.SPECIFIC_SCHEMA);
            this.SPECIFIC_NAME = dr.Value<string>(ParameterTableColumns.SPECIFIC_NAME);
            this.ORDINAL_POSITION = dr.Value<int?>(ParameterTableColumns.ORDINAL_POSITION);
            this.PARAMETER_MODE = dr.Value<string>(ParameterTableColumns.PARAMETER_MODE);
            this.IS_RESULT = dr.Value<string>(ParameterTableColumns.IS_RESULT);
            this.AS_LOCATOR = dr.Value<string>(ParameterTableColumns.AS_LOCATOR);
            this.PARAMETER_NAME = dr.Value<string>(ParameterTableColumns.PARAMETER_NAME);
            this.DATA_TYPE = dr.Value<SqlDbType>(ParameterTableColumns.DATA_TYPE, true);
            this.CHARACTER_MAXIMUM_LENGTH = dr.Value<int?>(ParameterTableColumns.CHARACTER_MAXIMUM_LENGTH);
            this.CHARACTER_OCTET_LENGTH = dr.Value<int?>(ParameterTableColumns.CHARACTER_OCTET_LENGTH);
            this.COLLATION_CATALOG = dr.Value<string>(ParameterTableColumns.COLLATION_CATALOG);
            this.COLLATION_SCHEMA = dr.Value<string>(ParameterTableColumns.COLLATION_SCHEMA);
            this.COLLATION_NAME = dr.Value<string>(ParameterTableColumns.COLLATION_NAME);
            this.CHARACTER_SET_CATALOG = dr.Value<string>(ParameterTableColumns.CHARACTER_SET_CATALOG);
            this.CHARACTER_SET_SCHEMA = dr.Value<string>(ParameterTableColumns.CHARACTER_SET_SCHEMA);
            this.CHARACTER_SET_NAME = dr.Value<string>(ParameterTableColumns.CHARACTER_SET_NAME);
            this.NUMERIC_PRECISION = dr.Value<int?>(ParameterTableColumns.NUMERIC_PRECISION);
            this.NUMERIC_PRECISION_RADIX = dr.Value<int?>(ParameterTableColumns.NUMERIC_PRECISION_RADIX);
            this.NUMERIC_SCALE = dr.Value<int?>(ParameterTableColumns.NUMERIC_SCALE);
            this.DATETIME_PRECISION = dr.Value<int?>(ParameterTableColumns.DATETIME_PRECISION);
            this.INTERVAL_TYPE = dr.Value<string>(ParameterTableColumns.INTERVAL_TYPE);
            this.INTERVAL_PRECISION = dr.Value<string>(ParameterTableColumns.INTERVAL_PRECISION);
            this.USER_DEFINED_TYPE_CATALOG = dr.Value<string>(ParameterTableColumns.USER_DEFINED_TYPE_CATALOG);
            this.USER_DEFINED_TYPE_SCHEMA = dr.Value<string>(ParameterTableColumns.USER_DEFINED_TYPE_SCHEMA);
            this.USER_DEFINED_TYPE_NAME = dr.Value<string>(ParameterTableColumns.USER_DEFINED_TYPE_NAME);
            this.SCOPE_CATALOG = dr.Value<string>(ParameterTableColumns.SCOPE_CATALOG);
            this.SCOPE_SCHEMA = dr.Value<string>(ParameterTableColumns.SCOPE_SCHEMA);
            this.SCOPE_NAME = dr.Value<string>(ParameterTableColumns.SCOPE_NAME);
        }

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}
