using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Penguin.Persistence.Database.Extensions
{
    public static class SqlDataReaderExtensions
    {
        public static IEnumerable<IDictionary<string, object>> GetRows(this SqlDataReader reader)
        {
            if (reader is null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            DataTable schemaTable = reader.GetSchemaTable();

            List<string> Columns = new List<string>();

            DataColumn ColumnNameColumn = schemaTable.Columns.Cast<DataColumn>().Single(dc => dc.ColumnName == nameof(DataColumn.ColumnName));

            foreach (DataRow dr in schemaTable.Rows)
            {
                Columns.Add(dr[ColumnNameColumn]?.ToString());
            }

            while (reader.HasRows)
            {
                while (reader.Read())
                {
                    Dictionary<string, object> itemArray = new Dictionary<string, object>();

                    for (int ci = 0; ci < Columns.Count; ci++)
                    {
                        itemArray.Add(Columns[ci], reader.GetValue(ci));
                    }

                    yield return itemArray;
                }

                _ = reader.NextResult();
            }
        }

        public static IEnumerable<object[]> GetItems(this SqlDataReader reader)
        {
            foreach (Dictionary<string, object> di in reader.GetRows())
            {
                yield return di.Values.ToArray();
            }
        }
    }
}
