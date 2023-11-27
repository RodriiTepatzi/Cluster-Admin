using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2P_UAQ_Server.Models
{
    public class FramesData : IDisposable
    {
		private bool _disposed = false;
		public (int, int) Range { get; set; }
        public object? Content { get; set; }

		public void Dispose()
		{
			Dispose(true);
			GC.Collect();
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					if (Content != null)
					{
						Content = null;
					}
				}

				_disposed = true;
			}
		}

		~FramesData()
		{
			Dispose(false);
		}
	}
}
