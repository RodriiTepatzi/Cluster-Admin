using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2P_UAQ_Server.Core.Events
{
    public class PrivateMessageReceivedEventArgs : EventArgs
    {
        public string Message { get; set; }
        public PrivateMessageReceivedEventArgs(string message) { 
            this.Message = message;
        }
    }
}
