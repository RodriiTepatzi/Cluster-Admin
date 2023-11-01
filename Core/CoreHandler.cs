using P2P_UAQ_Server.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using P2P_UAQ_Server.Core.Events;
using P2P_UAQ_Server.ViewModels;
using P2P_UAQ_Server.Views;
using System.Windows;
using FFMpegCore;


namespace P2P_UAQ_Server.Core
{
    public class CoreHandler
    {
        private readonly static CoreHandler _instance = new CoreHandler();
		private string? _serverIP;
		private int _serverPort;
		private int _maxConnections;
		private TcpListener? _server;
        private Queue<Connection> _connectionsQueue = new Queue<Connection>();
		private List<Connection> _connections = new List<Connection>();

        // datos de path

        private static string ffmpegPathString = "ffmpeg.exe"; // asumido cmo variable del sistema

        public static string inputPath = "C:\\Users\\gr_mi\\Desktop\\Ing en Software\\5to semestre\\Sistemas Distribuidos\\Proyectos\\Proyecto 3\\VideoTextMix\\Resources\\memePingu.mp4";
        public static string outputPath = "C:\\Users\\gr_mi\\Desktop\\Ing en Software\\5to semestre\\Sistemas Distribuidos\\Proyectos\\Proyecto 3\\VideoTextMix\\Resources";

        public static string? mainPath;
        public static string? processedImgsPath;
        public static string framesPath = "C:\\Users\\gr_mi\\Desktop\\Ing en Software\\5to semestre\\Sistemas Distribuidos\\Proyectos\\Proyecto 3\\VideoTextMix\\Resources\\frames";
        public static string audioPath = "C:\\Users\\gr_mi\\Desktop\\Ing en Software\\5to semestre\\Sistemas Distribuidos\\Proyectos\\Proyecto 3\\VideoTextMix\\Resources\\audio";


        // datos video

        public static double videoFramerate;
        public static string? videoName;
        public static string? videoCodec;
        public static string? videoExtension;
        public static string? videoAudioExtension;

        // datos conexiones 
        private TcpClient? _client;
		private Connection _newConnection = new Connection(); // Variable reutilizable para los usuarios conectados

		private bool _isRunning = false;

		public event EventHandler<PrivateMessageReceivedEventArgs>? PrivateMessageReceived;
		public event EventHandler<MessageReceivedEventArgs>? PublicMessageReceived;


		private CoreHandler() {
			_serverIP = "";
			_serverPort = 0;
        }

        public static CoreHandler Instance { 
            get { return _instance; } 
        }

        public event Action<string>? ServerStatusUpdated;


        //Para actualizar el status del server en el dashboard, esta se tiene que quedar
        public void OnStatusUpdated(string status)
        {
            ServerStatusUpdated?.Invoke(status);
        }



        // PARA INICIAR SERVIDOR
        public async void InitializeLocalServer(string ip, int port, string maxConnections) 
		{
			_serverIP = ip;
			_serverPort = port;
			_maxConnections = int.Parse(maxConnections);


			_server = new TcpListener(IPAddress.Parse(_serverIP), _serverPort);
			_server.Start(_maxConnections);

			HandlerOnMessageReceived($"Server listo y esperando en: {_serverIP}:{_serverPort}");

			while (true)
			{
				_client = await _server.AcceptTcpClientAsync();
				// guardamos la conexión con sus datos

				_newConnection = new Connection();
				_newConnection.Stream = _client.GetStream();
				_newConnection.StreamWriter = new StreamWriter(_newConnection.Stream); // stream para enviar
				_newConnection.StreamReader = new StreamReader(_newConnection.Stream); // stream para recibir


				// confirmamos el nombre

				var dataReceived = _newConnection.StreamReader!.ReadLine();
				var message = JsonConvert.DeserializeObject<Message>(dataReceived!);
				string? json = message!.Data as string;
				var convertedData = JsonConvert.DeserializeObject<Connection>(json!);

				_newConnection.Nickname = convertedData!.Nickname;
				_newConnection.IpAddress = convertedData.IpAddress; // ip

				if (object.Equals(convertedData.IpAddress, "0.0.0.0")) _newConnection.IpAddress = "127.0.0.1";

				_newConnection.Port = convertedData.Port; // puerto

				HandlerOnMessageReceived($"En espera de aprovación de nombre: {_newConnection.Nickname} - {_newConnection.IpAddress}:{_newConnection.Port}.");

				if (message.Type == MessageType.UserConnected)
				{
					var existingConnection = _connections.FindAll(c => c.Nickname == _newConnection.Nickname);

					if (existingConnection.Count == 0)
					{
						var messageToSend = new Message();
						messageToSend.Type = MessageType.UsernameInUse;
						messageToSend.Data = false;

						_newConnection.StreamWriter.WriteLine(JsonConvert.SerializeObject(messageToSend));
						_newConnection.StreamWriter.Flush();

						_connections.Add(_newConnection);

						HandlerOnMessageReceived("Nombre disponible. Notificando al cliente.");
						HandlerOnMessageReceived($"Conexión agregada: {_newConnection.IpAddress}:{_newConnection.Port} y notificando a todos.");
						
						foreach (Connection c in _connections)
						{
							// Se les enviara un mensaje de que x usuario se ha conectado.
							var msgUserToBeSent = new Message { Type = MessageType.Message, Data = $"{_newConnection.Nickname} se ha conectado." };

							// Enviamos el mensaje al cliente.
							c.StreamWriter!.WriteLine(JsonConvert.SerializeObject(msgUserToBeSent));
							c.StreamWriter!.Flush();

							foreach (var con in _connections)
							{
								SendConnectionListToAll(c, con);
							}
						}

						Thread thread = new Thread(ListenToConnection);
						thread.Start();
					}
					else
					{
						// enviar error
						message = new Message(); // overwrite el mensaje
						message.Type = MessageType.UsernameInUse;
						message.Data = true; // envia como dato el nombre en uso

						string messageJson = JsonConvert.SerializeObject(message);

						_newConnection.StreamWriter.WriteLine(messageJson);
						_newConnection.StreamWriter.Flush();

						HandlerOnMessageReceived($"Conexión rechazada: {_newConnection.IpAddress}:{_newConnection.Port}. Notificando al cliente.");
					}
				}

			}
		}
        
        public async void ListenToConnection()
        {
            
            Connection connection = _newConnection;
            var connectionOpen = true;

            while (connectionOpen) 
            {
                try
                {

                    var dataReceived = await connection.StreamReader!.ReadLineAsync();
                    var message = JsonConvert.DeserializeObject<Message>(dataReceived!);

                    if (message!.Type == MessageType.UserDisconnected)
                    {
                       
                    }
                }
                catch
                {
					// disconnected user
					_connections.RemoveAll(c => c.Nickname == connection.Nickname && c.IpAddress == connection.IpAddress && c.Port == connection.Port);
					HandlerOnMessageReceived($"Usuario desconectado. Actualizando a todos: {connection.Nickname} - {connection.IpAddress}:{connection.Port}. Notificando al cliente.");

					foreach (Connection c in _connections)
					{
						try
						{
							var msgUserDisconnected = $"{connection.Nickname} se ha desconectado.";
							var msgUserToBeSent = new Message { Type = MessageType.Message, Data = msgUserDisconnected };

							c.StreamWriter!.WriteLine(JsonConvert.SerializeObject(msgUserToBeSent));
							c.StreamWriter!.Flush();

							SendDisconnectedUserToAll(c, connection);
						}
						catch
						{
						}
					}

					connectionOpen = false;
				}
            }
        }



        // ******* MÉTODOS DE CLUSTER

        public static void CreateTempFolders()
        {
            // create a main tmp folder

            string tmpPathMain = Path.GetTempPath();
            string tmpFolderMain = Path.Combine(tmpPathMain, "MAIN_VIDEOTEST");

            Directory.CreateDirectory(tmpFolderMain);

            Console.WriteLine($"Main tmp folder: {tmpFolderMain}");


            // create subfolders in main folder for frames, audio and processed images

            string tmpFolderFrames = Path.Combine(tmpFolderMain, "frames");
            string tmpFolderAudio = Path.Combine(tmpFolderMain, "audio");
            string tmpFolderProcessedImgs = Path.Combine(tmpFolderMain, "processed_imgs");

            Directory.CreateDirectory(tmpFolderAudio);
            Directory.CreateDirectory(tmpFolderFrames);
            Directory.CreateDirectory(tmpFolderProcessedImgs);

            Console.WriteLine($"Tmp Audio: {tmpFolderAudio}");
            Console.WriteLine($"Tmp Frames: {tmpFolderFrames}");
            Console.WriteLine($"Tmp Processed Images: {tmpFolderProcessedImgs}");

            mainPath = tmpFolderMain;
            framesPath = tmpFolderFrames;
            audioPath = tmpFolderAudio;
            processedImgsPath = tmpFolderProcessedImgs;

        }



        public static void DeleteTempFolders()
        {
            Directory.Delete(framesPath, true);
            Directory.Delete(audioPath, true);
            Directory.Delete(processedImgsPath, true);
            Directory.Delete(mainPath, true);
        }



        public static void GetVideoFrames(string inputPath, string framesPath)
        {
            FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile($"{framesPath}\\frame%08d.bmp") // indicates the nomeclature i.e. 15th frame = frame00000015.bmp
                .ProcessSynchronously();

            Console.WriteLine("Imágenes extraídas con éxito.");
        }



        public static (double, string, string, string, string) GetVideoMeta(string inputPath)
        {

            IMediaAnalysis mediaInfo = FFProbe.Analyse(inputPath);

            // if not data stablish default values

            double framerate = mediaInfo.PrimaryVideoStream?.FrameRate ?? 30;
            string name = Path.GetFileNameWithoutExtension(inputPath) ?? "Unknown_Video";
            string codec = mediaInfo.PrimaryVideoStream?.CodecName ?? "h264";
            string videoExtension = Path.GetExtension(inputPath).TrimStart('.') ?? "mp4";
            string audioExtension = mediaInfo.PrimaryAudioStream?.CodecName ?? "mp3";

            Console.WriteLine("Metada extradida");

            return (framerate, name, codec, videoExtension, audioExtension);
        }



        public static void GetVideoAudio(string inputPath, string outputAudioPath, string audioExtension)
        {
            // saving audio according to audio format compatible with video format

            string arguments = $"-i \"{inputPath}\" -vn -acodec copy \"{outputAudioPath}\\audio.{audioExtension}\"";

            Process ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPathString,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            ffmpegProcess.Start();
            ffmpegProcess.WaitForExit();
        }



        public static void CreateVideoWithFramesAndSound(string imagePath, string audioInputPath, string videoOutputPath, double frameRate, string videoExtension, string audioExtension)
        {
            string arguments = $"-framerate {frameRate} -i \"{imagePath}\\frame%08d.bmp\" -i \"{audioInputPath}\\audio.{audioExtension}\" -c:v libx264 -pix_fmt yuv420p -c:a {audioExtension} -strict experimental \"{videoOutputPath}\\NEW_VIDEO.{videoExtension}\"";

            Process ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPathString,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            ffmpeg.Start();
            string errorOutput = ffmpeg.StandardError.ReadToEnd();
            ffmpeg.WaitForExit();

            if (!string.IsNullOrEmpty(errorOutput))
            {
                Console.WriteLine("FFmpeg Error Output:");
                Console.WriteLine(errorOutput);
            }
        }



        public static List<byte[]> GetImagesInFolder(string folderPath)
        {
            List<byte[]> imageBytesList = new List<byte[]>();

            try
            {
                string[] imageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(s => s.EndsWith(".bmp")).ToArray();

                foreach (string imagePath in imageFiles)
                {
                    byte[] imageBytes = File.ReadAllBytes(imagePath);
                    imageBytesList.Add(imageBytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            return imageBytesList;
        }



        public static byte[] CompressByteArray(byte[] media)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, true))
                {
                    gzipStream.Write(media, 0, media.Length);
                }

                return memoryStream.ToArray();
            }
        }



        public static byte[] DecompressByteArray(byte[] compressedMedia)
        {
            using (MemoryStream compressedStream = new MemoryStream(compressedMedia))
            using (GZipStream decompressionStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (MemoryStream decompressedStream = new MemoryStream())
            {
                decompressionStream.CopyTo(decompressedStream);
                return decompressedStream.ToArray();
            }
        }



        public static void CheckCompressionOfImagesInFolder(string folderPath)
        {
            string[] imageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(s => s.EndsWith(".bmp")).ToArray();

            double sumCompressed = 0;
            double sumDecompressed = 0;

            foreach (string imagePath in imageFiles)
            {
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                byte[] imageCompressed = CompressByteArray(imageBytes);
                byte[] imageDecompressed = DecompressByteArray(imageCompressed);
                sumCompressed += GetSize(imageCompressed);
                sumDecompressed += GetSize(imageDecompressed);

                Console.WriteLine($"Original: {GetSize(imageBytes)}, Comprimida: {GetSize(imageCompressed)}, Descomprimida: {GetSize(imageDecompressed)}");
            }

            Console.WriteLine($"Promedio sin comprimir: {sumDecompressed / imageFiles.Length}, Promedio comprimido: {sumCompressed / imageFiles.Length}, Mejora: {sumCompressed / sumDecompressed}");
        }


        public static int GetSize(byte[] image)
        {
            return sizeof(byte) * image.Length;

        }

        // ******* MÉTODOS DE CLUSTER

        public void StopServer()
        {
            if (_isRunning)
            {
                _server!.Stop();
                _isRunning = false;
            }
			HandlerOnMessageReceived("Servidor cerrado.");
        }


		// Eventos de interfaz
		
		// Invokers

		private void OnMessageReceived(MessageReceivedEventArgs e) => PublicMessageReceived?.Invoke(this, e);
		

		// Handlers

		private void HandlerOnMessageReceived(string value) => OnMessageReceived(new MessageReceivedEventArgs(value));
		
	}
}
