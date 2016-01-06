using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ftpClient.Operations
{
    class UploadOperation : OperationBase
    {
        private FileInfo _fileInfo;
        private FileStream _stream;
        private string _serverFileName;
        public override FtpOperation Operation
        {
            get
            {
                return FtpOperation.STOR;
            }
        }
        
        public UploadOperation(DirectoryInfo workingDirectory, string localName, string remoteName = null)
        {
            _serverFileName = string.IsNullOrWhiteSpace(remoteName) ? localName : remoteName;

            var localPath = Path.Combine(workingDirectory.FullName, localName);
            _fileInfo = new FileInfo(localPath);
        }

        public override bool Init(ControlChannel controlClient, TransferMode mode)
        {
            if (!_fileInfo.Exists)
            {
                //file with given name already exists
                Console.WriteLine($"ERROR: {_fileInfo.FullName} does not exist");
                return false;
            }

            var command = $"STOR {_serverFileName}";
            _dataClient = PrepareDataChannel(controlClient, mode, command);

            return true;            
        }

        public override async Task Process(ControlChannel controlClient)
        {
            _stream = _fileInfo.OpenRead();

            await UploadData(_dataClient, _stream);

            if (_dataClient != null)
            {
                _dataClient.Client.Close(5);
            }

            await _deferredResponse;
        }

        public override void Finish()
        {   
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        protected override void ParseData(byte[] data, int dataSize)
        {            
        }
    }
}
