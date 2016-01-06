using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ftpClient.Operations
{
    class DownloadOperation : OperationBase
    {
        private FileInfo _fileInfo;
        private FileStream _stream;
        private string _serverFileName;

        public override FtpOperation Operation
        {
            get
            {
                return FtpOperation.RETR;
            }
        }
        
        public DownloadOperation(DirectoryInfo workingDirectory, string remoteName, string localName = null)
        {
            localName = string.IsNullOrWhiteSpace(localName) ? remoteName : localName;
            _serverFileName = remoteName;

            var localPath = Path.Combine(workingDirectory.FullName, localName);
            _fileInfo = new FileInfo(localPath);
        }

        public override bool Init(ControlChannel controlClient, TransferMode mode)
        {
            if (Directory.Exists(_fileInfo.DirectoryName) && _fileInfo.Exists)
            {
                //file with given name already exists
                Console.WriteLine($"ERROR: {_fileInfo.FullName} already exists");
                return false;
            }

            var command = $"RETR {_serverFileName}";
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

            if (_stream != null)
            {
                _stream.Flush();
                _stream.Dispose();
                _stream = null;
            }
        }

        protected override void ParseData(byte[] data, int dataSize)
        {
            if (_stream == null)
            {
                if (Directory.Exists(_fileInfo.DirectoryName))
                {
                    Directory.CreateDirectory(_fileInfo.DirectoryName);
                }

                _stream = _fileInfo.Create();                
            }

            _stream.Write(data, 0, dataSize);
        }
    }
}
