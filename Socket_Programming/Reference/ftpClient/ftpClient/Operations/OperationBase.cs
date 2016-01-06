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
        protected TcpClient _dataClient;
        protected Task<Tuple<int, string>> _deferredResponse;
        protected CancellationTokenSource _cancelToken;

        protected OperationBase()
        {
            _cancelToken = new CancellationTokenSource();
        }

        public abstract FtpOperation Operation { get; }

        public abstract bool Init(ControlChannel controlClient, TransferMode mode);

        public abstract Task Process(ControlChannel controlClient);
        
        public abstract void Finish();

        protected abstract void ParseData(byte[] data, int size);

        protected Task DownloadData(TcpClient dataClient)
        {
            return Task.Run(
                () =>
                {
                    var buffer = new byte[dataClient.ReceiveBufferSize];
                    int receivedSize;
                    while (!_cancelToken.IsCancellationRequested && (receivedSize = dataClient.Client.Receive(buffer)) > 0)
                    {
                        ParseData(buffer, receivedSize);
                    }                    
                }, _cancelToken.Token);
        }

        protected Task UploadData(TcpClient dataClient, Stream dataStream)
        {
            return Task.Run(
                () =>
                {
                    var buffer = new byte[dataClient.SendBufferSize];
                    int readSize;
                    
                    while (!_cancelToken.IsCancellationRequested && (readSize = dataStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        dataClient.Client.Send(buffer, readSize, SocketFlags.None);
                    }
                }, _cancelToken.Token);
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
                var respCode = controlChannel.SendCommand($"PORT {addressBytes[0]},{addressBytes[1]},{addressBytes[2]},{addressBytes[3]},{endPoint.Port >> 8},{endPoint.Port & 0xff}", out response, ReturnCodes.ActionCompleted);

                TcpClient dataClient = null;
                if (respCode < 300)
                {
                    _deferredResponse = controlChannel.SendCommandDeferred(operationCommand);
                    dataClient = dataListener.AcceptTcpClient();
                    dataListener.Stop();
                }
                return dataClient;
            }
            else
            {
                string response;
                var returnCode = controlChannel.SendCommand($"PASV", out response, ReturnCodes.EnteringPassiveMode);

                if (returnCode == (int)ReturnCodes.EnteringPassiveMode)
                {
                    // extract address & port from response
                    var mx = Regex.Match(response, @"\d{1,3},\d{1,3},\d{1,3},\d{1,3},\d{1,3},\d{1,3}");
                    //mx.Success
                    var values = mx.Value.Split(',').Select(val => Convert.ToByte(val)).ToArray();
                    var addressBytes = values.Take(4).ToArray();
                    var ep = new IPEndPoint(new IPAddress(addressBytes), (values[4] << 8) + values[5]);

                    _dataClient = new TcpClient(new IPEndPoint(controlChannel.LocalEndPoint.Address, 0));
                    _dataClient.Connect(ep);

                    _deferredResponse = controlChannel.SendCommandDeferred(operationCommand);
                    return _dataClient;
                }
                else
                {
                    Console.WriteLine("Passive mode failed, attempt with active mode");
                    return PrepareDataChannel(controlChannel, TransferMode.Active, operationCommand);
                }               
            }
        }        
    }
}
