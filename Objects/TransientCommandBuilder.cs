namespace Penguin.Persistence.Database.Objects
{
    public class TransientCommandBuilder
    {
        public TransientCommandBuilder(string connectionString, int timeout)
        {
            ConnectionString = connectionString;
            CommandTimeout = timeout;
        }

        public int CommandTimeout { get; private set; }
        public string ConnectionString { get; private set; }

        public TransientCommand Build(string Query, params object[] args)
        {
            return TransientCommand.Build(Query, ConnectionString, CommandTimeout, args);
        }
    }
}