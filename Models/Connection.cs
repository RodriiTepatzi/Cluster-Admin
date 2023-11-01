using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2P_UAQ_Server.Models
{
    public class Connection
    {
		[JsonIgnore]
		public Stream? Stream { get; set; }

		[JsonIgnore]
		public StreamWriter? StreamWriter { get; set; }

		[JsonIgnore]
		public StreamReader? StreamReader { get; set; }
        
        
        //public string? Nickname { get; set; }
        public int Port { get; set; }
        public string? IpAddress { get; set; }

        public Connection() { }

        public Dictionary<string, object> ToDictionary()
        {
            var dictionary = new Dictionary<string, object>();

            //dictionary.Add(nameof(Nickname), string.IsNullOrEmpty(Nickname) ? "" : Nickname);
            dictionary.Add(nameof(Port), Port);
            dictionary.Add(nameof(IpAddress), string.IsNullOrEmpty(IpAddress) ? "" : IpAddress);

            return dictionary;
        }
    }
}
