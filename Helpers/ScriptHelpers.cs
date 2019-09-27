using Penguin.Persistence.Database.Objects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Penguin.Threading;
using System.Text;
using System.Threading.Tasks;
using Penguin.Extensions.Strings;
using Penguin.Debugging;
using Microsoft.SqlServer.Management.Smo;
using System.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;

namespace Penguin.Persistence.Database.Helpers
{
    internal static class ScriptHelpers
    {
        public const string DEFAULT_SPLIT = "\r\nGO\r\n";

        public static async Task RunSplitScript(string FilePath, string ConnectionString, int TimeOut = 0, string SplitOn = DEFAULT_SPLIT, Encoding encoding = null, bool detectEncodingFromByteOrderMarks = true, int bufferSize = -1)
        {
            DateTime start = DateTime.Now;

            ConcurrentQueue<AsyncSqlCommand> Commands = new ConcurrentQueue<AsyncSqlCommand>();
            bool ReadComplete = false;

            BackgroundWorker SQLWorker = BackgroundWorker.Create((worker) =>
            {
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    Server server = new Server(new ServerConnection(connection));

                    connection.Open();

                    // Create the command and set its properties.
                    SqlCommand command = new SqlCommand
                    {
                        Connection = connection,
                        CommandType = CommandType.Text,
                        CommandTimeout = TimeOut
                    };

                    while (!ReadComplete || Commands.Any())
                    {
                        if (Commands.Any())
                        {
                            if (Commands.TryDequeue(out AsyncSqlCommand cmd))
                            {
                                try
                                {
                                    StaticLogger.Log($"Executing Command {cmd.CommandNumber} - {Math.Round(cmd.Progress, 2)}%");
   
                                    server.ConnectionContext.ExecuteNonQuery(cmd.Text);
                                    
                                }
                                catch (Exception ex)
                                {
                                    List<string> AcceptableErrors = new List<string>()
                                    {
                                        "is not a valid login or you do not have permission",
                                        "User does not have permission to perform this action",
                                        "not found. Check the name again.",
                                        " already exists in the current database."
                                    };

                                    if (!AcceptableErrors.Any(ae => ex.Message.Contains(ae)))
                                    {
                                        StaticLogger.Log(cmd.Text + Environment.NewLine + ex.Message);
                                        throw ex;
                                    }
                                }
                            }
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(10);
                        }
                    }
                }
            });

            BackgroundWorker FileReadWorker = BackgroundWorker.Create((worker) =>
            {
                
                using (FileStream stream = File.OpenRead(FilePath))
                {
                    using (StreamReader reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks, bufferSize, false)) 
                    {
                        decimal streamLength = reader.BaseStream.Length;
                        // Open the connection and execute the reader.

                        int BufferLength = SplitOn.Length;

                        char[] buffer = new char[BufferLength];
                        char[] splitArray = SplitOn.ToCharArray();

                        int bufferPointer = 0;

                        StringBuilder currentCommand = new StringBuilder(5000);

                        int commandNumber = 0;
                        bool wrapped = false;

                        while (!reader.EndOfStream)
                        {
                            char currentChar = (char)reader.Read();

                            buffer[bufferPointer] = currentChar;

                            bool breakScript = true;

                            for (int i = 0; i < BufferLength; i++)
                            {
                                if (buffer[(bufferPointer - ((buffer.Length - 1) - i) % buffer.Length + buffer.Length) % buffer.Length] != SplitOn[i])
                                {
                                    breakScript = false;
                                    break;
                                }
                            }

                            bufferPointer++;

                            if (bufferPointer == buffer.Length)
                            {
                                wrapped = true;
                                bufferPointer = 0;
                            }

                            if (breakScript)
                            {
                                AsyncSqlCommand icmd = new AsyncSqlCommand(currentCommand.ToString(), ((reader.BaseStream.Position / streamLength) * 100), ++commandNumber);

                                while (Commands.Count > 5)
                                {
                                    if (SQLWorker.IsBusy)
                                    {
                                        System.Threading.Thread.Sleep(100);
                                    }
                                    else
                                    {
                                        throw new Exception("Sql worker ended unexpectedly");
                                    }
                                }

                                Commands.Enqueue(icmd);

                                currentCommand.Clear();

                                buffer = new char[BufferLength];
                                bufferPointer = 0;
                                wrapped = false;
                            }
                            else
                            {
                                if (wrapped)
                                {
                                    currentCommand.Append(buffer[bufferPointer]);
                                }
                            }
                        }

                        int finalPointer = bufferPointer;
                        bool breakBuffer;
                        //Grab any straying characters from the buffer;

                        do
                        {

                            bufferPointer++;


                            if (bufferPointer == buffer.Length)
                            {
                                wrapped = true;
                                bufferPointer = 0;
                            }

                            breakBuffer = bufferPointer == finalPointer;

                            if (breakBuffer)
                            {
                                break;
                            }

                            currentCommand.Append(buffer[bufferPointer]);

                        } while (true);

                        AsyncSqlCommand lcmd = new AsyncSqlCommand(currentCommand.ToString(), ((reader.BaseStream.Position / streamLength) * 100), commandNumber);

                        Commands.Enqueue(lcmd);

                        ReadComplete = true;
                    }
                }
            });

            Task<bool> FileReadResult = FileReadWorker.RunWorkerAsync();
            Task<bool> SqlProcessResult = SQLWorker.RunWorkerAsync();

            await FileReadResult;
            await SqlProcessResult;
        }
    }
}