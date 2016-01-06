using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ftpClient.Operations
{
    class DirectoryListingOperation : OperationBase
    {
        private string infoTargetName;
        private StringBuilder dirInfoData;

        public override FtpOperation Operation
        {
            get
            {
                return FtpOperation.LIST;
            }
        }

        public DirectoryListingOperation(string objectName)
        {
            infoTargetName = objectName;
            dirInfoData = new StringBuilder();
        }

        public override bool Init(ControlChannel controlClient, TransferMode mode)
        {
            var command = "LIST" + (string.IsNullOrWhiteSpace(infoTargetName) ? string.Empty : " " + infoTargetName);
            _dataClient = PrepareDataChannel(controlClient, mode, command);

            return true;            
        }

        public override async Task Process(ControlChannel controlClient)
        {
            var operation = DownloadData(_dataClient);
            await _deferredResponse;

            if (!operation.IsCompleted)
            {
                await Task.Delay(1000);
                _cancelToken.Cancel();
            }          
        }

        public override void Finish()
        {
            if (_dataClient != null)
            {
                _dataClient.Client.Close();
            }

            Console.WriteLine(dirInfoData.ToString());
        }

        protected override void ParseData(byte[] data, int dataSize)
        {
            var infoFragment = Encoding.ASCII.GetString(data, 0, dataSize);
            dirInfoData.Append(infoFragment);
        }
    }
}
