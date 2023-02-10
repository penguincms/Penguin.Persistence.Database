using System;
using System.Data.SqlClient;

namespace Penguin.Persistence.Database.Objects
{
    public class TransientCommand : IDisposable
    {
        private SqlConnection Connection;

        private SqlCommand Command;

        private bool disposedValue;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "<Pending>")]
        public SqlDataAdapter GetDataAdapter()
        {
            return new SqlDataAdapter(Command);
        }

        public SqlDataReader GetReader()
        {
            return Command.ExecuteReader();
        }

        public static TransientCommand Build(string Query, string ConnectionString, int CommandTimeout, params object[] args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            SqlConnection conn = new(ConnectionString);
            SqlCommand command = new(Query, conn);

            for (int i = 0; i < args.Length; i++)
            {
                SqlParameter param = new($"@{i}", args[i]);

                _ = command.Parameters.Add(param);
            }

            command.CommandTimeout = CommandTimeout;

            conn.Open();

            return new TransientCommand()
            {
                Command = command,
                Connection = conn
            };
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Connection.Close();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~TransientCommand()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}