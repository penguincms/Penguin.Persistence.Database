using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Penguin.Debugging;
using Penguin.Persistence.Database.Objects;
using Penguin.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Penguin.Persistence.Database.Helpers
{
    internal static class ScriptHelpers
    {
        public const string DEFAULT_SPLIT = "\r\nGO\r\n";

        public static async Task RunSplitScript(string FilePath, string ConnectionString, int TimeOut = 0, string SplitOn = DEFAULT_SPLIT, Encoding encoding = null, bool detectEncodingFromByteOrderMarks = true, int bufferSize = -1)
        {
            DateTime start = DateTime.Now;
            Exception toThrow = null;

            ConcurrentQueue<AsyncSqlCommand> Commands = new ConcurrentQueue<AsyncSqlCommand>();
            bool ReadComplete = false;

            BackgroundWorker SQLWorker = BackgroundWorker.Create((worker) =>
            {
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    Server server = new Server(new ServerConnection(connection));
                    server.ConnectionContext.StatementTimeout = TimeOut;
                    connection.Open();

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

                                    if (!AcceptableErrors.Any(ae => ex.Message.Contains(ae)) && (ex.InnerException == null || !AcceptableErrors.Any(ae => ex.InnerException.Message.Contains(ae))))
                                    {
                                        StaticLogger.Log(cmd.Text + Environment.NewLine + ex.Message);
                                        toThrow = ex;
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

                        if (streamLength == 0)
                        {
                            throw new Exception($"Stream length of 0 for file {FilePath}");
                        }

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
                                        if (toThrow != null)
                                        {
                                            throw toThrow;
                                        }
                                        else
                                        {
                                            throw new Exception("Sql worker ended unexpectedly");
                                        }
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