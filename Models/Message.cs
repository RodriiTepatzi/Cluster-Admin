using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2P_UAQ_Server.Models
{
    public class Message
    {
        public MessageType Type { get; set; }
        public object? Content { get; set; }
        public Connection? connection { get; set; }
    }
}
