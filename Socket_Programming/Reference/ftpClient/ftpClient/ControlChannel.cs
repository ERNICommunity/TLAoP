using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ftpClient
{
    class ControlChannel
    {
        TcpClient controlClient;
        ManualResetEvent signal = new ManualResetEvent(true);
        int responseCode;
        string responseText;
        DateTime lastResponse;

        public IPEndPoint LocalEndPoint
        {
            get
            {
                return (IPEndPoint)controlClient.Client.LocalEndPoint;
            }
        }

        public bool Init(string host, string login, string password)
        {
            controlClient = new TcpClient();
            controlClient.Connect(host, 21);

            // setup async receive
            ControlChannelResponse();

            string response;
            SendCommand($"USER {login}", out response);
            SendCommand($"PASS {password}", out response);

            // set binary mode
            SendCommand("TYPE I", out response);

            return true;
        }

        public int SendCommand(string command, out string response)
        {
            Console.WriteLine($"SENDING... {command}");

            // 'hack' to avoid need to evaluate return codes
            while(DateTime.Now - lastResponse < new TimeSpan(0, 0, 0, 0, 500))
            {
                Thread.Sleep(100);
            } 

            signal.Reset();
            responseCode = int.MinValue;

            // newline (0x0A)
            controlClient.Client.Send(Encoding.ASCII.GetBytes(command + "\n"));

            signal.WaitOne();
            response = responseText;
            return responseCode;
        }

        public void Close()
        {
            controlClient.Close();
        }

        private Task ControlChannelResponse()
        {
            return Task.Run(() =>
                {
                    var buffer = new byte[controlClient.ReceiveBufferSize];
                    int receiveSize;
                    int offset = 0;
                    while ((receiveSize = controlClient.Client.Receive(buffer, offset, buffer.Length - offset, SocketFlags.None)) > 0)
                    {
                        var totalSize = offset + receiveSize;
                        offset = 0;
                        string responseString;

                        // process received response data
                        while ((responseString = ReadResponse(buffer, totalSize, ref offset)) != null)
                        {
                            // read response Code & print response
                            var code =
                            char.GetNumericValue(responseString, 0) * 100 +
                            char.GetNumericValue(responseString, 1) * 10 +
                            char.GetNumericValue(responseString, 2);

                            Console.WriteLine(responseString);

                            // first response to command
                            if (responseCode == int.MinValue)
                            {
                                responseText = responseString;
                                responseCode = (int)code;
                                signal.Set();
                            }
                        }

                        // shift remaining data
                        if (offset > 0 && offset < totalSize)
                        {
                            Array.Copy(buffer, offset, buffer, 0, totalSize - offset);
                            offset = totalSize - offset;
                        }
                        else
                        {
                            offset = 0;
                        }
                    }
                });
        }

        private string ReadResponse(byte[] dataBuffer, int dataSize, ref int dataOffset)
        {
            // find end of line
            for (int idx = dataOffset; idx < dataSize; idx++)
            {
                if (dataBuffer[idx] == 0x0A)
                {
                    var response = Encoding.ASCII.GetString(dataBuffer, dataOffset, idx - dataOffset);

                    dataOffset = idx + 1;
                    return response;
                }
            }

            return null;
        } 
    }
}
