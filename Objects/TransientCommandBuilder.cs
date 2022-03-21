namespace Penguin.Persistence.Database.Objects
{
    public class TransientCommandBuilder
    {
        public TransientCommandBuilder(string connectionString, int timeout)
        {
            this.ConnectionString = connectionString;
            this.CommandTimeout = timeout;
        }

        public int CommandTimeout { get; private set; }
        public string ConnectionString { get; private set; }

        public TransientCommand Build(string Query, params object[] args) => TransientCommand.Build(Query, this.ConnectionString, this.CommandTimeout, args);
    }
}
