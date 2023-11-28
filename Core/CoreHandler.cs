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
using P2P_UAQ_Server.Managers;
using System.Windows;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace P2P_UAQ_Server.Core
{
    public class CoreHandler
    {
        private readonly static CoreHandler _instance = new CoreHandler();
        private string? _serverIP;
        private int _serverPort;
        private int _maxConnections;
        private TcpListener? _server;
        private bool _isRunning = false;
        private int _expectedImages;

        private List<Connection> _clientQueue = new List<Connection>();
        private List<Connection> _serversWaiting = new List<Connection>();
        private List<Connection> _serversWorking = new List<Connection>();
		private List<Connection> _servers = new List<Connection>();

		private Status _serverStatus = Status.Waiting;

        // datos de path

        private string _ffmpegPathString = "ffmpeg.exe"; // as os system variable
		private VideoManager videoManager;

		private string _inputPath = "D:\\CLUSTER_FOLDER\\video."; // for received video
        private string _outputPath = "D:\\CLUSTER_FOLDER";

        private string? _mainPath; // temporal main folder 
        private string? _processedImgsPath; // subfolder in main folder, for images from SP
        private string? _framesPath; // subfolder in main folder, for frames
        private string? _audioPath; // subfolder in main folder, for audio
		private string? _videoFormat;

		private Dictionary<(int, int), List<byte[]>> _processedImages = new Dictionary<(int, int), List<byte[]>>();
		private int _serversFinished = 0;
		private int _totalServers = 0;

		// datos conexiones 
		private TcpClient? _client;
        private Connection _newConnection = new Connection(); // Variable reutilizable para los usuarios conectados
		private Connection _serverConnection = new Connection();

        public event EventHandler<PrivateMessageReceivedEventArgs>? PrivateMessageReceived;
        public event EventHandler<MessageReceivedEventArgs>? PublicMessageReceived;


        private CoreHandler()
        {
            _serverIP = "";
            _serverPort = 0;
        }

        public static CoreHandler Instance
        {
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
            // check if folder for IO video exists

            if (!Directory.Exists(_outputPath)) Directory.CreateDirectory(_outputPath);

            // create server

            _serverIP = ip;
            _serverPort = port;
            _server = new TcpListener(IPAddress.Parse(_serverIP), _serverPort);

            if (int.TryParse(maxConnections, out _maxConnections))
            {
                _server.Start(_maxConnections);
                HandlerOnMessageReceived($"Server listo y esperando en: {_serverIP}:{_serverPort} (Max.Usuarios: {_maxConnections})");
            }
            else
            {
                _server.Start();
                HandlerOnMessageReceived($"Server listo y esperando en: {_serverIP}:{_serverPort} (Max.Usuarios: Sin Límite)");
            }


            HandlerOnMessageReceived($"Status: Waiting");

            while (true)
            {
                _client = await _server.AcceptTcpClientAsync();

                _newConnection = new Connection();
                _newConnection.Stream = _client.GetStream();
                _newConnection.StreamWriter = new StreamWriter(_newConnection.Stream); // stream para enviar
                _newConnection.StreamReader = new StreamReader(_newConnection.Stream); // stream para recibir

                var dataReceived = _newConnection.StreamReader!.ReadLine();
                var message = JsonConvert.DeserializeObject<Message>(dataReceived!);
                var connection = JsonConvert.DeserializeObject<Connection>((string)message!.Content!);

                _newConnection.IpAddress = connection!.IpAddress; // ip

                if (object.Equals(connection.IpAddress, "0.0.0.0")) _newConnection.IpAddress = "127.0.0.1";

                _newConnection.Port = connection.Port; // puerto

                switch (message.Type)
                {
                    case (MessageType.User):
                        _clientQueue.Add(_newConnection); 
                        HandlerOnMessageReceived($"Nuevo cliente en espera {_newConnection.IpAddress}:{_newConnection.Port}");


                        SendTurnToClients(_clientQueue);

                        if (_serverStatus == Status.Busy) { SendStatusToChosenClient(Status.Busy, _newConnection); }
                        else if (_serverStatus == Status.Waiting) { SendStatusToChosenClient(Status.Waiting, _newConnection); }
						else
						{
							SendStatusToClients(Status.Ready);
						}

						Thread thread = new Thread(ListenClient);
						thread.Start();

						break;

                    case (MessageType.Processor):

                        _servers.Add(_newConnection);
						_serversWaiting.Add(_newConnection);
						HandlerOnMessageReceived($"Nuevo server en espera {_newConnection.IpAddress}:{_newConnection.Port}");
						AddWaitingServers();

						if (_serverStatus == Status.Waiting)
						{
							AddWaitingServers();
						}

						if (_serverStatus == Status.Ready && _clientQueue.Count > 0)
						{
							UpdateStatus(Status.Waiting);
						}

						SendStatusToClients(Status.Ready);

						Thread thread2 = new Thread(ListenToServers);
						thread2.Start();

						break;
				}
			}
        }

		public async void ListenClient()
		{
			var connection = _newConnection;

			while (true)
			{
				try
				{
					var dataReceived = await connection.StreamReader!.ReadLineAsync();
					var message = JsonConvert.DeserializeObject<Message>(dataReceived!);
					var video = JsonConvert.DeserializeObject<Video>((string)message!.Content!);

					_inputPath += video!.Format;
					_videoFormat = video!.Format;
					File.WriteAllBytes(_inputPath, video.Data);

					CreateTempFolders();

					videoManager = new VideoManager(_ffmpegPathString);

					videoManager.GetVideoMeta(_inputPath);
					videoManager.GetVideoAudio(_inputPath, _audioPath!);
					videoManager.GetVideoFrames(_inputPath, _framesPath!);

					HandlerOnMessageReceived("Información y metada obtenida");

					SendImagesToServers(_framesPath!);

					//WaitForProcessedImages(_expectedImages);
				}
				catch
				{
					//_clientQueue.Remove(connection);
					//HandlerOnMessageReceived($"Cliente con IP: {connection.IpAddress} se ha desconectado.");
					break;
				}
				
			}
		}

        // ******* MÉTODOS DE CLUSTER



        public void SendStatusToClients(Status status)
        {
            Message message = new Message
            {
                Type = MessageType.Status,
                Content = JsonConvert.SerializeObject(status),
            };

            string json = JsonConvert.SerializeObject(message);

            foreach (var c in _clientQueue)
            {
                c.StreamWriter!.WriteLine(json);
                c.StreamWriter!.Flush();
            }

        }



        public void SendStatusToServers(Status status)
        {
            Message message = new Message
            {
                Type = MessageType.Status,
                Content = JsonConvert.SerializeObject(status),
            };

            string json = JsonConvert.SerializeObject(message);

            foreach (var c in _serversWorking)
            {
                c.StreamWriter?.WriteLine(json);
                c.StreamWriter?.Flush();
            }
        }

		public void SendImagesToServers(string framesPath)
		{
			Message message = new Message
			{
				Type = MessageType.Data
			};

			var imageFiles = Directory.GetFiles(framesPath);
			int servers = _serversWorking.Count;

			int limit = servers;
			int numImages = imageFiles.Length;
			int range = 0;

			if (numImages < servers)
			{
				limit = numImages;
			}

			int imagesPerServer = numImages / servers;
			int remainingImages = numImages % servers;

			for (int server = 0; server < limit; server++)
			{
				int initialRange = range + 1;

				for (int i = 0; i < imagesPerServer; i++)
				{
					var image = File.ReadAllBytes(imageFiles[range]);
					SendImageToServer(image, server, initialRange, range);
					range++;
				}

				if (remainingImages > 0)
				{
					var image = File.ReadAllBytes(imageFiles[range]);
					SendImageToServer(image, server, initialRange, range);
					remainingImages--;
					range++;
				}

				HandlerOnMessageReceived("Imágenes enviadas para su procesamiento");
				int finalRange = range;

				Message endOfDataMessage = new Message
				{
					Type = MessageType.EndOfData,
					Content = "No hay más imágenes por enviar"
				};

				var json = JsonConvert.SerializeObject(endOfDataMessage);

				_serversWorking[server].StreamWriter?.WriteLine(json);
				_serversWorking[server].StreamWriter?.Flush();

				GC.Collect();
			}

			_expectedImages = limit;
		}


		private void SendImageToServer(byte[] image, int server, int initialRange, int finalRange)
		{
			FramesData processedData = new FramesData
			{
				Range = (initialRange, finalRange),
				Content = Encoding.ASCII.GetString(image),
			};

			Message message = new Message
			{
				Type = MessageType.Data,
				Content = processedData
			};

			var json = JsonConvert.SerializeObject(message);


			_serversWorking[server].StreamWriter?.WriteLine(json);
			_serversWorking[server].StreamWriter?.Flush();
		}



		public void WaitForProcessedImages(int expected)
		{
			int expectedAnswers = expected;
			List<Thread> threads = new List<Thread>();

			if (expected == 1)
			{
				_serverConnection = _serversWorking[0];
				Thread thread = new Thread(new ThreadStart(ListenToServers));
				threads.Add(thread);
				thread.Start();
			}
			else
			{
				for (int i = 0; i < _serversWorking.Count; i++)
				{
					_serverConnection = _serversWorking[i];
					Thread thread = new Thread(new ThreadStart(ListenToServers));
					threads.Add(thread);
					thread.Start();
				}
			}

			foreach (Thread thread in threads)
			{
				thread.Join();
			}
		}


		public async void ListenToServers()
		{
			var connection = _newConnection;

			while (true)
			{
				var data = await connection.StreamReader!.ReadLineAsync();

				if (data is not null)
				{
					var message = JsonConvert.DeserializeObject<Message>(data!);


					if (message!.Type == MessageType.ProcessedData)
					{

						var processedData = JsonConvert.DeserializeObject<FramesData>(message.Content.ToString());
						var part = processedData!.Range;
						var image = Encoding.ASCII.GetBytes(processedData.Content.ToString());

						// Guarda la imagen procesada en el diccionario
						if (!_processedImages.ContainsKey(part))
						{
							_processedImages[part] = new List<byte[]>();
						}
						_processedImages[part].Add(image);
					}
					else if (message.Type == MessageType.EndOfData)
					{
						_serversFinished++;

						if (_serversFinished == _totalServers)
						{
							HandlerOnMessageReceived("Imágenes procesadas recibidas");
							var orderedImages = _processedImages.OrderBy(pair => pair.Key.Item1)
																.SelectMany(pair => pair.Value)
																.ToList();

							for (int i = 0; i < orderedImages.Count; i++)
							{
								// Para guardar: path\frame%08d.bmp
								string frameName = $"frame{FrameName(i)}.bmp";
								File.WriteAllBytes(_processedImgsPath + "\\" + frameName, orderedImages[i]);
							}

							HandlerOnMessageReceived("Imágenes generadas. Procediendo a generar el video.");

							videoManager.CreateVideoWithFramesAndSound(_processedImgsPath!, _audioPath!, _outputPath);
							HandlerOnMessageReceived("Video creado. Enviando al cliente");


							Video processedVideo = new Video
							{
								Format = videoManager!.videoExtension!,
								Data = File.ReadAllBytes($"{_outputPath}\\NEW_VIDEO.{_videoFormat}"),
							};

							string json = JsonConvert.SerializeObject(processedVideo);
							connection.StreamWriter!.WriteLine(json);
							connection.StreamWriter!.Flush();

							HandlerOnMessageReceived("Video enviado. Limpiando información temporal");
							DeleteTempFolders();
							DeleteVideos();

							AddWaitingServers();
							UpdateStatus(Status.Ready);

							break;
						}

						break;
					}
				}
			}
		}


		public string FrameName(int index)
        {
            string numString = index.ToString();

            for (int i = 0; 0 < (8 - numString.Length); i++)
            {
                numString = "0" + numString;
            }

            return numString;
        }


        public void AddWaitingServers()
        {
            int serversWaiting = _serversWaiting.Count;

            if (serversWaiting > 0)
            {
                Message message = new Message
                {
                    Type = MessageType.Status,
                    Content = Status.Ready,
                };

                var json = JsonConvert.SerializeObject(message);

                foreach (var s in _serversWaiting)
                {
                    s.StreamWriter?.WriteLine(json);
                    s.StreamWriter?.Flush();

                    _serversWorking.Add(s);
                }

				_totalServers = _serversWorking.Count;

                HandlerOnMessageReceived($"Nuevo server agregados: {_serversWaiting.Count}");
                UpdateStatus(Status.Ready);
                _serversWaiting.Clear();

                return;
            }

        }


        public void SendStatusToChosenClient(Status status, Connection connection)
        {
            Message message = new Message
            {
                Type = MessageType.Status,
                Content = JsonConvert.SerializeObject(status),
            };

            string json = JsonConvert.SerializeObject(message);
            connection.StreamWriter?.WriteLine(json);
            connection.StreamWriter?.Flush();

            HandlerOnMessageReceived($"El cliente {connection.IpAddress}:{connection.Port} ha sido notificado");

        }

        public void SendTurnToClients(List<Connection> connections)
        {
            int clientsBeforeYou = 0;

            Message message = new Message
            {
                Type = MessageType.Turn,
            };

            foreach (var c in connections)
            {
                message.Content = clientsBeforeYou;
                string json = JsonConvert.SerializeObject(message);
                c.StreamWriter?.WriteLine(json);
                c.StreamWriter?.Flush();
                clientsBeforeYou++;
            }

            HandlerOnMessageReceived("Nuevos turnos enviados");
        }

        public void DeleteVideos()
        {
            string[] videos = Directory.GetFiles(_outputPath);

            foreach (var v in videos)
            {
                File.Delete(v);
            }

            HandlerOnMessageReceived("Videos eliminados");
        }



        public void CreateTempFolders()
        {
            // create a main tmp folder

            string tmpPathMain = Path.GetTempPath();
            string tmpFolderMain = Path.Combine(tmpPathMain, "MAIN_VIDEOTEST");

            Directory.CreateDirectory(tmpFolderMain);


            // create subfolders in main folder for frames, audio and processed images

            string tmpFolderFrames = Path.Combine(tmpFolderMain, "frames");
            string tmpFolderAudio = Path.Combine(tmpFolderMain, "audio");
            string tmpFolderProcessedImgs = Path.Combine(tmpFolderMain, "processed_imgs");

            Directory.CreateDirectory(tmpFolderAudio);
            Directory.CreateDirectory(tmpFolderFrames);
            Directory.CreateDirectory(tmpFolderProcessedImgs);

            _mainPath = tmpFolderMain;
            _framesPath = tmpFolderFrames;
            _audioPath = tmpFolderAudio;
            _processedImgsPath = tmpFolderProcessedImgs;


            HandlerOnMessageReceived("Folders temporales creados");

        }



        public void DeleteTempFolders()
        {
            Directory.Delete(_framesPath!, true);
            Directory.Delete(_audioPath!, true);
            Directory.Delete(_processedImgsPath!, true);
            Directory.Delete(_mainPath!, true);
            HandlerOnMessageReceived("Folders temporales eliminados");
        }



        public List<byte[]> GetImagesInFolder(string folderPath)
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



        public byte[] CompressByteArray(byte[] media)
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



        public byte[] DecompressByteArray(byte[] compressedMedia)
        {
            using (MemoryStream compressedStream = new MemoryStream(compressedMedia))
            using (GZipStream decompressionStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            using (MemoryStream decompressedStream = new MemoryStream())
            {
                decompressionStream.CopyTo(decompressedStream);
                return decompressedStream.ToArray();
            }
        }



        public int GetSize(byte[] image)
        {
            return sizeof(byte) * image.Length;

        }



        public void UpdateStatus(Status newStatus)
        {
            _serverStatus = newStatus;
            HandlerOnMessageReceived($"Nuevo Status: {newStatus}");
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
