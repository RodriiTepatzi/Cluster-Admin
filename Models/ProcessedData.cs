using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2P_UAQ_Server.Models
{
    public class ProcessedData
    {
		public int Part { get; set; }
        public object? Content { get; set; }

    }
}
