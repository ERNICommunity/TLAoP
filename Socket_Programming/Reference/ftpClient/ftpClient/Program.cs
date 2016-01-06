using ftpClient.Operations;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ftpClient
{
    enum TransferMode
    {
        Active,
        Passive
    }

    class Program
    {   
        static void Main(string[] args)
        {
            string login = string.Empty;
            string pass = string.Empty;
            string host = string.Empty;
            var correctParameters = args.Length == 1;
            if (correctParameters)
            {
                try
                {
                    var connectString = args[0];
                    var passDelimiter = connectString.IndexOf(":");
                    var hostDelimiter = connectString.IndexOf("@");

                    login = connectString.Substring(0, passDelimiter);
                    pass = connectString.Substring(passDelimiter + 1, hostDelimiter - passDelimiter - 1);
                    host = connectString.Substring(hostDelimiter + 1, connectString.Length - hostDelimiter - 1);
                    correctParameters = !string.IsNullOrWhiteSpace(login) && !string.IsNullOrWhiteSpace(pass) && !string.IsNullOrWhiteSpace(host);
                }
                catch
                {
                    correctParameters = false;
                }
            }

            if (!correctParameters)
            {
                Console.WriteLine("Incorrect parameter. Usage: ftpClient <login>:<password>@<host>");
                return;
            }

            Console.WriteLine($"Connecting to: {host} using LOGIN: {login} & PASSWORD: {pass}");

            var mode = TransferMode.Active;
            var controlChannel = new ControlChannel();
            if (!controlChannel.Init(host, login, pass))
            {
                return;
            }

            OperationBase currentOperation = null;
            string cmd = null;
            do
            {
                Console.Write("ftpClient> ");
                var cmdLine = Console.ReadLine();
                var cmdParams = cmdLine.Split(' ');
                cmd = cmdParams.Length > 0 ? cmdParams[0].ToLower() : string.Empty;

                currentOperation = null;

                switch (cmd)
                {
                    case "ls":
                    case "dir":
                        {
                            currentOperation = new DirectoryListingOperation(cmdParams.Length > 1 ? cmdParams[1] : null);
                            break;
                        }
                    case "recv":
                    case "get":
                        {
                            if (cmdParams.Length < 2 || cmdParams.Length > 3)
                            {
                                Console.WriteLine($"Incorrect command usage! Type: {cmdParams[0].ToUpper()} remote_file_name [local_file_name]");
                                continue;
                            }
                            currentOperation =
                                new DownloadOperation(
                                    new DirectoryInfo(Environment.CurrentDirectory),
                                    cmdParams[1],
                                    cmdParams.Length > 2 ? cmdParams[2] : null);
                            break;
                        }
                    case "send":
                    case "put":
                        {
                            if (cmdParams.Length < 2 || cmdParams.Length > 3)
                            {
                                Console.WriteLine($"Incorrect command usage! Type: {cmdParams[0].ToUpper()} local_file_name [remote_file_name]");
                                continue;
                            }
                            currentOperation =
                                new UploadOperation(
                                    new DirectoryInfo(Environment.CurrentDirectory),
                                    cmdParams[1],
                                    cmdParams.Length > 2 ? cmdParams[2] : null);
                            break;
                        }
                    case "delete":
                        {
                            if (cmdParams.Length != 2)
                            {
                                Console.WriteLine($"Incorrect command usage! Type: DELETE remote_file_name");
                                continue;
                            }
                            string response;
                            controlChannel.SendCommand($"DELE {cmdParams[1]}", out response);
                            break;
                        }
                    case "mode":
                        {
                            if (cmdParams.Length != 2 || (cmdParams[1].ToLower() != "a" && cmdParams[1].ToLower() != "p"))
                            {
                                Console.WriteLine("Incorrect command usage! Type: MODE [a|p]");
                                continue;
                            }
                            
                            mode = cmdParams[1].ToLower() == "a" ? TransferMode.Active : TransferMode.Passive;
                            Console.WriteLine($"{mode.ToString().ToUpper()} data transfer mode set.");
                            break;
                        }
                    case "mkdir":
                        {
                            if (cmdParams.Length != 2)
                            {
                                Console.WriteLine("Incorrect command usage! Type: MKDIR directory_name");
                                continue;
                            }

                            string response;
                            controlChannel.SendCommand($"MKD {cmdParams[1]}", out response);
                            break;
                        }
                    case "rmdir":
                        {
                            if (cmdParams.Length != 2)
                            {
                                Console.WriteLine("Incorrect command usage! Type: RMDIR directory_name");
                                continue;
                            }

                            string response;
                            controlChannel.SendCommand($"RMD {cmdParams[1]}", out response);
                            break;
                        }
                    case "cd":
                        {
                            if (cmdParams.Length != 2)
                            {
                                Console.WriteLine("Incorrect command usage! Type: CD directory_name");
                                continue;
                            }

                            string response;
                            controlChannel.SendCommand($"CWD {cmdParams[1]}", out response);
                            break;
                        }
                    case "cdup":
                        {
                            string response;
                            controlChannel.SendCommand($"CDUP", out response);
                            break;
                        }
                    case "site":
                        {
                            if (cmdParams.Length > 2)
                            {
                                Console.WriteLine($"Incorrect command usage! Type: SITE [site_specific_command]");
                                continue;
                            }

                            string response;
                            controlChannel.SendCommand($"SITE {(cmdParams.Length > 1 ? cmdParams[1] : string.Empty)}", out response);
                            currentOperation = null;
                            break;
                        }
                    case "quit":
                        {
                            string response;
                            controlChannel.SendCommand("QUIT", out response);
                            currentOperation = null;
                            break;
                        }
                    case "help":
                        {
                            Console.WriteLine("List of supported commands:");
                            Console.WriteLine("DIR(LS) [directory_name] - list content of the specified directory.");
                            Console.WriteLine("RECV(GET) <server_file_name> [local_file_name] - download file from the server.");
                            Console.WriteLine("SEND(PUT) <local_file_name> [server_file_name] - upload file to the server.");
                            Console.WriteLine("DELETE <server_file_name> - delete file on the server.");
                            Console.WriteLine("MODE <a|p> - set active or passive mode for the data channel.");
                            Console.WriteLine("MKDIR <directory_name> - create directory on the server.");
                            Console.WriteLine("RMDIR <directory_name> - remove directory on the server.");
                            Console.WriteLine("CD <directory_name> - change current server directory.");
                            Console.WriteLine("CDUP - change current server directory one level up.");
                            Console.WriteLine("SITE [custom_parameter] - site specific command.");
                            Console.WriteLine("QUIT - close ftpClient");
                            break; 
                        }
                    default:
                        {
                            Console.WriteLine("Unknown command! Type HELP for the list of supported commands.");
                            break;
                        }
                }

                if (currentOperation != null && currentOperation.Init(controlChannel, mode))
                {
                    try
                    {
                        currentOperation.Process(controlChannel).Wait();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    finally
                    {
                        currentOperation.Finish();
                    }
                }

            } while (cmd != "quit" && controlChannel.IsConnected);

            controlChannel.Close();            
        }        
    }
}
