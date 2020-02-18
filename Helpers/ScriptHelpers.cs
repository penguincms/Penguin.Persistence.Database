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
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Penguin.Persistence.Database.Helpers
{
    public static class ScriptHelpers
    {
        private const long BUFFER_SIZE = 26214400;
        public const string DEFAULT_SPLIT = "\r\nGO\r\n";

        public static void Decompress(string FilePath, string OutputFile = null)
        {
            OutputFile = OutputFile ?? $"{new FileInfo(FilePath).Directory.FullName}\\{Path.GetFileNameWithoutExtension(FilePath)}.sql";

            using (FileStream fs = new FileStream(OutputFile, FileMode.Create, FileAccess.ReadWrite))
            {
                foreach (byte[] bytes in ReadCompressedFile(FilePath))
                {
                    fs.Write(bytes, 0, bytes.Length);
                }
            }

            File.Delete(FilePath);
        }

        private static GZipStream GetDecompressStream(string FilePath)
        {
            FileStream inputFile = new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite);
            return new GZipStream(inputFile, CompressionMode.Decompress);

        }

        public static IEnumerable<Byte[]> ReadCompressedFile(string FilePath)
        {
            using (GZipStream compressionStream = GetDecompressStream(FilePath))
            {

                    int read = 0;
                    byte[] toReturn = new byte[BUFFER_SIZE];

                    while ((read = compressionStream.Read(toReturn, 0, toReturn.Length)) != 0)
                    {
                        if (read == BUFFER_SIZE)
                        {
                            yield return toReturn;
                        }
                        else
                        {
                            byte[] ra = new byte[read];

                            for (int i = 0; i < read; i++)
                            {
                                ra[i] = toReturn[i];
                            }

                            yield return ra;
                        }
                    }
                }
            }
        

        private static void StreamThrough(Stream source, Stream dest, Func<byte[], byte[]> toInvoke = null)
        {
            while (source.Length > 0)
            {
                Console.WriteLine(source.Length);

                long thisBlock = Math.Min(source.Length, BUFFER_SIZE);

                source.Seek(-1 * thisBlock, SeekOrigin.End);

                byte[] buffer = new byte[thisBlock];

                source.Read(buffer, 0, buffer.Length);

                if (toInvoke != null)
                {
                    buffer = toInvoke(buffer);
                }

                dest.Write(buffer, 0, buffer.Length);

                source.SetLength(source.Length - thisBlock);
            }
        }
        public static void CompressScript(string sqlPath, string outputPath = null)
        {
            string OutputFile = outputPath ?? $"{new FileInfo(sqlPath).Directory.FullName}\\{Path.GetFileNameWithoutExtension(sqlPath)}.zql";
            string ToutputFile = $"{new FileInfo(sqlPath).Directory.FullName}\\{Path.GetFileNameWithoutExtension(sqlPath)}.rql";

            if (File.Exists(OutputFile))
            {
                File.Delete(OutputFile);
            }

            if (File.Exists(ToutputFile))
            {
                File.Delete(ToutputFile);
            }

            using (FileStream RFileStream = new FileStream(ToutputFile, FileMode.CreateNew, FileAccess.ReadWrite))
            {
                using (FileStream inputFile = new FileStream(sqlPath, FileMode.Open, FileAccess.ReadWrite))
                {
                    //Reverse the input file
                    StreamThrough(inputFile, RFileStream, (b) => b.Reverse().ToArray());
                }

                File.Delete(sqlPath);

                using (FileStream compressedFileStream = new FileStream(OutputFile, FileMode.CreateNew, FileAccess.ReadWrite))
                {

                    using (GZipStream compressionStream = new GZipStream(compressedFileStream, CompressionLevel.Optimal))
                    {
                        StreamThrough(RFileStream, compressionStream, (b) => b.Reverse().ToArray());
                    }
                }
            }
            File.Delete(ToutputFile);
        }
        internal static async Task RunSplitScript(Stream stream, string ConnectionString, int TimeOut = 0, string SplitOn = DEFAULT_SPLIT, Encoding encoding = null, bool detectEncodingFromByteOrderMarks = true, int bufferSize = 4096)
        {
            DateTime start = DateTime.Now;
            Exception toThrow = null;

            encoding = encoding ?? Encoding.Default;

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
                                        throw;
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
            });

            Task<bool> FileReadResult = FileReadWorker.RunWorkerAsync();
            Task<bool> SqlProcessResult = SQLWorker.RunWorkerAsync();

            await FileReadResult;
            await SqlProcessResult;
        }

        internal static async Task RunSplitScript(string FilePath, string ConnectionString, int TimeOut = 0, string SplitOn = DEFAULT_SPLIT, Encoding encoding = null, bool detectEncodingFromByteOrderMarks = true, int bufferSize = 4096)
        {
            Stream s = null;

            if (!File.Exists(FilePath))
            {
                throw new Exception($"SQL File not found: {FilePath}");
            }

            if (new FileInfo(FilePath).Length == 0)
            {
                throw new Exception($"Stream length of 0 for file {FilePath}");
            }

            if (Path.GetExtension(FilePath).Trim('.').Equals("zql", StringComparison.OrdinalIgnoreCase))
            {
                GZipStream gz = GetDecompressStream(FilePath);
            }
            else
            {
                s = File.OpenRead(FilePath);
            }

            await RunSplitScript(s, ConnectionString, TimeOut, SplitOn, encoding, detectEncodingFromByteOrderMarks, bufferSize);

            s?.Dispose();
        }
    }
}