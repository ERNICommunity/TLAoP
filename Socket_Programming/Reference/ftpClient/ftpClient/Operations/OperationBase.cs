using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ftpClient.Operations
{
    enum FtpOperation
    {
        // FILE/DIRECTORY list. Always ASCII mode
        LIST,

        // name-only list of items in the directory. Always ASCII mode
        NLST,

        // file download
        RETR,

        // file upload
        STOR
    }

    abstract class OperationBase
    {
        protected TcpClient dataClient;

        public abstract FtpOperation Operation { get; }

        public abstract bool Init(ControlChannel controlClient, TransferMode mode);

        public abstract Task Process(ControlChannel controlClient);

        protected abstract void ParseData(byte[] data, int size);

        protected abstract void Finish();

        protected async Task DownloadData(TcpClient dataClient)
        {
            await Task.Run(
                () =>
                {
                    var buffer = new byte[dataClient.ReceiveBufferSize];
                    int receivedSize;
                    while ((receivedSize = dataClient.Client.Receive(buffer)) > 0)
                    {
                        ParseData(buffer, receivedSize);
                    }

                    Finish();
                });
        }

        protected async Task UploadData(TcpClient dataClient, Stream dataStream)
        {
            await Task.Run(
                () =>
                {
                    var buffer = new byte[dataClient.SendBufferSize];
                    int readSize;
                    while ((readSize = dataStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        dataClient.Client.Send(buffer, readSize, SocketFlags.None);
                    }
                    Finish();                                        
                });
        }

        protected TcpClient PrepareDataChannel(ControlChannel controlChannel, TransferMode mode, string operationCommand)
        {
            if (mode == TransferMode.Active)
            {
                // ACTIVE mode
                var dataListener = new TcpListener(new IPEndPoint(controlChannel.LocalEndPoint.Address, 0));
                dataListener.Start();

                var endPoint = (IPEndPoint)dataListener.LocalEndpoint;
                var addressBytes = endPoint.Address.MapToIPv4().GetAddressBytes();

                string response;
                controlChannel.SendCommand($"PORT {addressBytes[0]},{addressBytes[1]},{addressBytes[2]},{addressBytes[3]},{endPoint.Port >> 8},{endPoint.Port & 0xff}", out response);
                controlChannel.SendCommand(operationCommand, out response);
                var dataClient = dataListener.AcceptTcpClient();
                dataListener.Stop();

                return dataClient;
            }
            else
            {
                string response;
                controlChannel.SendCommand($"PASV", out response);

                // extract address & port from response
                var mx = Regex.Match(response, @"\d{1,3},\d{1,3},\d{1,3},\d{1,3},\d{1,3},\d{1,3}");
                //mx.Success
                var values = mx.Value.Split(',').Select(val => Convert.ToByte(val)).ToArray();
                var addressBytes = values.Take(4).ToArray();
                var ep = new IPEndPoint(new IPAddress(addressBytes), (values[4] << 8) + values[5]);
                
                dataClient = new TcpClient(new IPEndPoint(controlChannel.LocalEndPoint.Address, 0));
                dataClient.Connect(ep);

                controlChannel.SendCommand(operationCommand, out response);

                return dataClient;
            }
        }        
    }
}
