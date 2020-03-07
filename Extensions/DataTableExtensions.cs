using Penguin.Persistence.Abstractions.Attributes.Rendering;
using Penguin.Reflection.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;

namespace Penguin.Persistence.Database.Extensions
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public static class DataTableExtensions
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        private const string TOO_MANY_RESULTS_MESSAGE = "Called Single on a data table with more than one result";

        /// <summary>
        /// Returns a typed list representing all values in a given column of the data table
        /// </summary>
        /// <typeparam name="T">The type of the list to return</typeparam>
        /// <param name="dt">The source data table</param>
        /// <param name="ColumnName">The name of the column to convert to a typed list</param>
        /// <returns>A typed list representing all values in a given column of the data table</returns>
        public static List<T> All<T>(this DataTable dt, string ColumnName)
        {
            Contract.Requires(dt != null);
            Contract.Requires(!string.IsNullOrWhiteSpace(ColumnName));

            List<T> toReturn = new List<T>();

            foreach (DataRow dr in dt.Rows)
            {
                object o = dr[ColumnName];

                if (o?.ToString() is null)
                {
                    toReturn.Add(default);
                }
                else
                {
                    toReturn.Add(o.ToString().Convert<T>());
                }
            }

            return toReturn;
        }

        /// <summary>
        /// Returns casted value from a [1][1] length datatable
        /// </summary>
        /// <typeparam name="T">The type to cast the result as</typeparam>
        /// <param name="dt">The [1][1] length data table to use as a source</param>
        /// <param name="IgnoreCase">When searching for type, ignore the source case (for enums)</param>
        /// <returns></returns>
        public static T GetSingle<T>(this DataTable dt, bool IgnoreCase = false)
        {
            Contract.Requires(dt != null);

            if (dt.Rows.Count > 1 || dt.Columns.Count > 1)
            { throw new Exception(TOO_MANY_RESULTS_MESSAGE); }

            return dt.Rows[0][0].ToString().Convert<T>(IgnoreCase);
        }

        /// <summary>
        /// Converts an IEnumerable of objects to a data table, with property names as headers and values as items
        /// </summary>
        /// <param name="objList">The IEnumerable of objects to use as a data source</param>
        /// <param name="ShowAllFields">if true, no logic is attempted to filter the results using display attributes</param>
        /// <returns>A data table containing the object values</returns>
        public static DataTable ToDataTable(this IEnumerable<object> objList, bool ShowAllFields = false)
        {
            Contract.Requires(objList != null);

            DataTable thisTable = new DataTable();

            Type objectType = objList.GetType().GenericTypeArguments[0];

            List<PropertyInfo> Properties = new List<PropertyInfo>();

            Dictionary<PropertyInfo, int> PropertyOrder = new Dictionary<PropertyInfo, int>();

            foreach (PropertyInfo thisProp in objectType.GetProperties().Reverse())
            {
                DisplayAttribute displayAttribute = thisProp.GetCustomAttribute<DisplayAttribute>();

                bool display = true;

                if (displayAttribute != null)
                {
                    display = display && displayAttribute.AutoGenerateField;
                }

                display = display || ShowAllFields;

                if (display)
                {
                    int index = 0;

                    while (index < Properties.Count && PropertyOrder[Properties.ElementAt(index)] > index)
                    {
                        index++;
                    }
                    Properties.Insert(index, thisProp);
                    PropertyOrder.Add(thisProp, index);
                }
            }

            foreach (PropertyInfo thisProperty in Properties)
            {
                DisplayNameAttribute displayNameAttribute = thisProperty.GetCustomAttribute<DisplayNameAttribute>();
                string DisplayName;
                if (displayNameAttribute != null)
                {
                    DisplayName = displayNameAttribute.DisplayName;
                }
                else
                {
                    DisplayName = thisProperty.Name;
                }

                thisTable.Columns.Add(DisplayName);
            }

            foreach (object thisObj in objList)
            {
                DataRow thisRow = thisTable.NewRow();
                int i = 0;
                foreach (PropertyInfo thisProperty in Properties)
                {
                    thisRow[i++] = thisProperty.GetValue(thisObj);
                }

                thisTable.Rows.Add(thisRow);
            }
            return thisTable;
        }

        /// <summary>
        /// Returns a casted value from a data row, by column name
        /// </summary>
        /// <typeparam name="T">The type to cast the result as</typeparam>
        /// <param name="dr">The data row to use as a source</param>
        /// <param name="ColumnName">The name of the column to use as a data source</param>
        /// <param name="IgnoreCase">Ignore the case of enums when attempting to cast</param>
        /// <returns>A casted representation of the requested value</returns>
        public static T Value<T>(this DataRow dr, string ColumnName, bool IgnoreCase = false)
        {
            Contract.Requires(dr != null);

            if (dr[ColumnName] is null)
            {
                return default;
            }

            return dr[ColumnName].ToString().Convert<T>(IgnoreCase);
        }
    }
}