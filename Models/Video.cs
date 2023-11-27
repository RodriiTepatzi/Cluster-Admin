using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2P_UAQ_Server.Models
{
	public class Video : IDisposable
	{
		private bool _disposed = false; // Para detectar llamadas redundantes

		public string Format { get; set; }

		public byte[] Data { get; set; }

		// Este código se agrega para implementar correctamente el patrón descartable.
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					// Libera el estado (objetos administrados).
					if (Data != null)
					{
						Data = null;
					}
				}

				// Libera recursos no administrados (objetos no administrados) y anula el finalizador
				// Establece campos grandes como nulos.

				_disposed = true;
			}
		}

		// Anula un finalizador solo si el método Dispose(bool disposing) anterior tiene código para liberar recursos no administrados.
		~Video()
		{
			// No cambie este código. Coloque el código de limpieza en el método Dispose(bool disposing) anterior.
			Dispose(false);
		}

	}
}
