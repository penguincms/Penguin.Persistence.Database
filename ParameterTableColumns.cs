namespace Penguin.Persistence.Database
{
    /// <summary>
    /// This class simply maps const to string values to avoid magic strings throughout the code. Each const name is equal to its value
    /// </summary>
    public static class ParameterTableColumns
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public const string AS_LOCATOR = "AS_LOCATOR";
        public const string CHARACTER_MAXIMUM_LENGTH = "CHARACTER_MAXIMUM_LENGTH";
        public const string CHARACTER_OCTET_LENGTH = "CHARACTER_OCTET_LENGTH";
        public const string CHARACTER_SET_CATALOG = "CHARACTER_SET_CATALOG";
        public const string CHARACTER_SET_NAME = "CHARACTER_SET_NAME";
        public const string CHARACTER_SET_SCHEMA = "CHARACTER_SET_SCHEMA";
        public const string COLLATION_CATALOG = "COLLATION_CATALOG";
        public const string COLLATION_NAME = "COLLATION_NAME";
        public const string COLLATION_SCHEMA = "COLLATION_SCHEMA";
        public const string DATA_TYPE = "DATA_TYPE";
        public const string DATETIME_PRECISION = "DATETIME_PRECISION";
        public const string INTERVAL_PRECISION = "INTERVAL_PRECISION";
        public const string INTERVAL_TYPE = "INTERVAL_TYPE";
        public const string IS_RESULT = "IS_RESULT";
        public const string NUMERIC_PRECISION = "NUMERIC_PRECISION";
        public const string NUMERIC_PRECISION_RADIX = "NUMERIC_PRECISION_RADIX";
        public const string NUMERIC_SCALE = "NUMERIC_SCALE";
        public const string ORDINAL_POSITION = "ORDINAL_POSITION";
        public const string PARAMETER_MODE = "PARAMETER_MODE";
        public const string PARAMETER_NAME = "PARAMETER_NAME";
        public const string SCOPE_CATALOG = "SCOPE_CATALOG";
        public const string SCOPE_NAME = "SCOPE_NAME";
        public const string SCOPE_SCHEMA = "SCOPE_SCHEMA";
        public const string SPECIFIC_CATALOG = "SPECIFIC_CATALOG";
        public const string SPECIFIC_NAME = "SPECIFIC_NAME";
        public const string SPECIFIC_SCHEMA = "SPECIFIC_SCHEMA";
        public const string USER_DEFINED_TYPE_CATALOG = "USER_DEFINED_TYPE_CATALOG";
        public const string USER_DEFINED_TYPE_NAME = "USER_DEFINED_TYPE_NAME";
        public const string USER_DEFINED_TYPE_SCHEMA = "USER_DEFINED_TYPE_SCHEMA";
#pragma warning restore CA1707 // Identifiers should not contain underscores
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }
}