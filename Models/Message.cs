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
        public string? IpAddressRequester { get; set; }
        public string? NicknameRequester { get; set; }
        public object? Data { get; set; }
        public int PortRequester { get; set; }
    }
}
