﻿using System;
using System.Collections;
using System.Data;

namespace Penguin.Persistence.Database
{
    /// <summary>
    /// Convert a base data type to another base data type
    /// </summary>
    public sealed class TypeConverter
    {
        #region Constructors

        static TypeConverter()
        {
            DbTypeMapEntry dbTypeMapEntry
            = new DbTypeMapEntry(typeof(bool), DbType.Boolean, SqlDbType.Bit);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(byte), DbType.Double, SqlDbType.TinyInt);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(byte[]), DbType.Binary, SqlDbType.Image);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(DateTime), DbType.DateTime, SqlDbType.DateTime);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(decimal), DbType.Decimal, SqlDbType.Decimal);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(double), DbType.Double, SqlDbType.Float);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(Guid), DbType.Guid, SqlDbType.UniqueIdentifier);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(short), DbType.Int16, SqlDbType.SmallInt);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(int), DbType.Int32, SqlDbType.Int);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(long), DbType.Int64, SqlDbType.BigInt);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(object), DbType.Object, SqlDbType.Variant);
            _DbTypeList.Add(dbTypeMapEntry);

            dbTypeMapEntry
            = new DbTypeMapEntry(typeof(string), DbType.String, SqlDbType.VarChar);
            _DbTypeList.Add(dbTypeMapEntry);
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Convert .Net type to Db type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static DbType ToDbType(Type type)
        {
            DbTypeMapEntry entry = Find(type);
            return entry.DbType;
        }

        /// <summary>
        /// Convert TSQL data type to DbType
        /// </summary>
        /// <param name="sqlDbType"></param>
        /// <returns></returns>
        public static DbType ToDbType(SqlDbType sqlDbType)
        {
            DbTypeMapEntry entry = Find(sqlDbType);
            return entry.DbType;
        }

        /// <summary>
        /// Convert db type to .Net data type
        /// </summary>
        /// <param name="dbType"></param>
        /// <returns></returns>
        public static Type ToNetType(DbType dbType)
        {
            DbTypeMapEntry entry = Find(dbType);
            return entry.Type;
        }

        /// <summary>
        /// Convert TSQL type to .Net data type
        /// </summary>
        /// <param name="sqlDbType"></param>
        /// <returns></returns>
        public static Type ToNetType(SqlDbType sqlDbType)
        {
            DbTypeMapEntry entry = Find(sqlDbType);
            return entry.Type;
        }

        /// <summary>
        /// Convert .Net type to TSQL data type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static SqlDbType ToSqlDbType(Type type)
        {
            DbTypeMapEntry entry = Find(type);
            return entry.SqlDbType;
        }

        /// <summary>
        /// Convert DbType type to TSQL data type
        /// </summary>
        /// <param name="dbType"></param>
        /// <returns></returns>
        public static SqlDbType ToSqlDbType(DbType dbType)
        {
            DbTypeMapEntry entry = Find(dbType);
            return entry.SqlDbType;
        }

        #endregion Methods

        #region Fields

        private static ArrayList _DbTypeList = new ArrayList();

        #endregion Fields

        #region Structs

        private struct DbTypeMapEntry
        {
            #region Fields

            public DbType DbType;
            public SqlDbType SqlDbType;
            public Type Type;

            #endregion Fields

            #region Constructors

            public DbTypeMapEntry(Type type, DbType dbType, SqlDbType sqlDbType)
            {
                this.Type = type;
                this.DbType = dbType;
                this.SqlDbType = sqlDbType;
            }

            #endregion Constructors
        };

        #endregion Structs

        private TypeConverter()
        {
        }

        private static DbTypeMapEntry Find(Type type)
        {
            object retObj = null;
            for (int i = 0; i < _DbTypeList.Count; i++)
            {
                DbTypeMapEntry entry = (DbTypeMapEntry)_DbTypeList[i];
                if (entry.Type == type)
                {
                    retObj = entry;
                    break;
                }
            }
            if (retObj == null)
            {
                throw
                new ApplicationException("Referenced an unsupported Type");
            }

            return (DbTypeMapEntry)retObj;
        }

        private static DbTypeMapEntry Find(DbType dbType)
        {
            object retObj = null;
            for (int i = 0; i < _DbTypeList.Count; i++)
            {
                DbTypeMapEntry entry = (DbTypeMapEntry)_DbTypeList[i];
                if (entry.DbType == dbType)
                {
                    retObj = entry;
                    break;
                }
            }
            if (retObj == null)
            {
                throw
                new ApplicationException("Referenced an unsupported DbType");
            }

            return (DbTypeMapEntry)retObj;
        }

        private static DbTypeMapEntry Find(SqlDbType sqlDbType)
        {
            object retObj = null;
            for (int i = 0; i < _DbTypeList.Count; i++)
            {
                DbTypeMapEntry entry = (DbTypeMapEntry)_DbTypeList[i];
                if (entry.SqlDbType == sqlDbType)
                {
                    retObj = entry;
                    break;
                }
            }
            if (retObj == null)
            {
                throw
                new ApplicationException("Referenced an unsupported SqlDbType");
            }

            return (DbTypeMapEntry)retObj;
        }
    }
}