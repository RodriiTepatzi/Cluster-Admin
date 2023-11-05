﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2P_UAQ_Server.Models
{
    public class FramesData
    {
		public (int, int) Range { get; set; }
        public object? Content { get; set; }

    }
}