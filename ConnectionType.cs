namespace Penguin.Persistence.Database
{
    /// <summary>
    /// Represents the method by which a connection string was passed into an application
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>
        /// The configuration file was specified
        /// </summary>
        File,

        /// <summary>
        /// The connection string was passed in via command line
        /// </summary>
        String
    }
}