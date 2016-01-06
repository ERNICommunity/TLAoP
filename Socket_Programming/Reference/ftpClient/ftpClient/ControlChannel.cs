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
        private TcpClient controlClient;
        private ManualResetEvent signal = new ManualResetEvent(false);
        private int responseCode;
        private string responseText;
        private ReturnCodes[] expectedCodes;
        private Task _responseLoopTask;

        public bool IsConnected
        {
            get { return !(_responseLoopTask.IsCompleted || _responseLoopTask.IsFaulted); } 
        }

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
            try
            {
                controlClient.Connect(host, 21);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

            expectedCodes = new [] { ReturnCodes.ServiceIsReady };

            // setup async receive
            _responseLoopTask = ControlChannelResponse();

            // wait for initial '220' response
            signal.WaitOne();
                        
            string response;
            var loginStatusOk = SendCommand($"USER {login}", out response, ReturnCodes.LoginReceivedSendPassword) == (int)ReturnCodes.LoginReceivedSendPassword;

            if (loginStatusOk)
            {
                loginStatusOk &= SendCommand($"PASS {password}", out response, ReturnCodes.UserLoggedIn) == (int)ReturnCodes.UserLoggedIn;
            }
                       
            // set binary mode - should be default mode / set as required?
            SendCommand("TYPE I", out response, ReturnCodes.ActionCompleted);
            
            return loginStatusOk;
        }

        public int SendCommand(string command, out string response, params ReturnCodes[] expectedReturnCodes)
        {
            Console.WriteLine($"SENDING... {command}");
                        
            signal.Reset();
            expectedCodes = expectedReturnCodes;
            responseCode = 0;
            responseText = string.Empty;

            // newline (0x0A)
            controlClient.Client.Send(Encoding.ASCII.GetBytes(command + "\n"));

            signal.WaitOne();
            response = responseText;
            return responseCode;
        }

        public Task<Tuple<int, string>> SendCommandDeferred(string command, params ReturnCodes[] expectedReturnCodes)
        {
            Console.WriteLine($"SENDING... {command}");
            signal.Reset();
            expectedCodes = expectedReturnCodes;
            responseCode = 0;
            responseText = string.Empty;
                        
            controlClient.Client.Send(Encoding.ASCII.GetBytes(command + "\n"));
                        
            return Task.Run(() => 
            {
                signal.WaitOne();
                return new Tuple<int, string>(responseCode, responseText);
            });
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
                    try
                    {
                        while ((receiveSize = controlClient.Client.Receive(buffer, offset, buffer.Length - offset, SocketFlags.None)) > 0)
                        {
                            var totalSize = offset + receiveSize;
                            offset = 0;
                            string responseString;

                            // process received response data
                            while ((responseString = ReadResponse(buffer, totalSize, ref offset)) != null)
                            {
                                // read response Code & print response
                                Console.WriteLine(responseString);

                                int code = (int)
                                (char.GetNumericValue(responseString, 0) * 100 +
                                char.GetNumericValue(responseString, 1) * 10 +
                                char.GetNumericValue(responseString, 2));

                                if (code == 421)
                                {
                                    // timeout
                                    return;
                                }

                                if ((expectedCodes.Length == 0 && code >= 200) || (expectedCodes.Any(ec => (int)ec == code) || code >= 400))
                                {
                                    responseText = responseString;
                                    responseCode = code;
                                    signal.Set();
                                }
                                //signal.Set();                         
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
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                    finally
                    {
                        signal.Set();
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
