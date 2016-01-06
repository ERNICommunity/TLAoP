using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ftpClient
{
    enum ReturnCodes
    {
        ActionCompleted = 200,
        ServiceIsReady = 220,
        LoginReceivedSendPassword = 331,
        DataConnectionOpen = 225,
        FileActionSuccessful = 226,
        EnteringPassiveMode = 227,
        UserLoggedIn = 230,      
    }
}
