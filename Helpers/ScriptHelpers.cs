using Penguin.Persistence.Database.Objects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Penguin.Threading;
using System.Text;
using System.Threading.Tasks;

namespace Penguin.Persistence.Database.Helpers
{
    internal static class ScriptHelpers
    {
        public const string DEFAULT_SPLIT = "\r\nGO\r\n";

        public static async Task RunSplitScript(string FilePath, string ConnectionString, string SplitOn = DEFAULT_SPLIT)
        {
            DateTime start = DateTime.Now;

            ConcurrentQueue<AsyncSqlCommand> Commands = new ConcurrentQueue<AsyncSqlCommand>();
            bool ReadComplete = false;

            BackgroundWorker SQLWorker = BackgroundWorker.Create((worker) =>
            {
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();

                    // Create the command and set its properties.
                    SqlCommand command = new SqlCommand
                    {
                        Connection = connection,
                        CommandType = CommandType.Text,
                        CommandTimeout = 600000
                    };

                    while (!ReadComplete)
                    {
                        if (Commands.Any())
                        {
                            if (Commands.TryDequeue(out AsyncSqlCommand cmd))
                            {
                                try
                                {
                                    Console.WriteLine($"Executing Command {cmd.CommandNumber} - {Math.Round(cmd.Progress, 2)}%");

                                    command.CommandText = cmd.Text;

                                    command.ExecuteNonQuery();
                                }
                                catch (Exception ex)
                                {
                                    List<string> AcceptableErrors = new List<string>()
                                    {
                                        "is not a valid login or you do not have permission",
                                        "User does not have permission to perform this action",
                                        "not found. Check the name again.",
                                        " already exists in the current database.",
                                        " 'GO'."
                                    };

                                    if (!AcceptableErrors.Any(ae => ex.Message.Contains(ae)))
                                    {
                                        Console.WriteLine(cmd.Text + Environment.NewLine + ex.Message);
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
                using (StreamReader reader = File.OpenText(FilePath))
                {
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
                            AsyncSqlCommand icmd = new AsyncSqlCommand(currentCommand.ToString(), ((reader.BaseStream.Position / (decimal)reader.BaseStream.Length) * 100), ++commandNumber);

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

                    AsyncSqlCommand lcmd = new AsyncSqlCommand(currentCommand.ToString(), ((reader.BaseStream.Position / (decimal)reader.BaseStream.Length) * 100), commandNumber);

                    Commands.Enqueue(lcmd);

                    ReadComplete = true;

                    while (SQLWorker.IsBusy)
                    {
                        System.Threading.Thread.Sleep(100);
                    };
                }
            });

            Task<bool> FileReadResult = FileReadWorker.RunWorkerAsync();
            Task<bool> SqlProcessResult = SQLWorker.RunWorkerAsync();

            await FileReadResult;
            await SqlProcessResult;
        }
    }
}