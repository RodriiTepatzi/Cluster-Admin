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
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;



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

        private Status _serverStatus = Status.Waiting;

        // datos de path

        private string _ffmpegPathString = "ffmpeg.exe"; // as os system variable

        private string _inputPath = "C:\\CLUSTER_FOLDER\\video."; // for received video
        private string _outputPath = "C:\\CLUSTER_FOLDER";

        private string? _mainPath; // temporal main folder 
        private string? _processedImgsPath; // subfolder in main folder, for images from SP
        private string? _framesPath; // subfolder in main folder, for frames
        private string? _audioPath; // subfolder in main folder, for audio


        // datos video

        private double _videoFramerate;
        private string? _videoName;
        private string? _videoCodec;
        private string? _videoExtension;
        private string? _videoAudioExtension;

        // datos conexiones 
        private TcpClient? _client;
        private Connection _newConnection = new Connection(); // Variable reutilizable para los usuarios conectados


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

                // save connection

                _newConnection = new Connection();
                _newConnection.Stream = _client.GetStream();
                _newConnection.StreamWriter = new StreamWriter(_newConnection.Stream); // stream para enviar
                _newConnection.StreamReader = new StreamReader(_newConnection.Stream); // stream para recibir


                var dataReceived = _newConnection.StreamReader!.ReadLine();
                Message message = JsonConvert.DeserializeObject<Message>(dataReceived!);
                Connection connection = JsonConvert.DeserializeObject<Connection>((string)message.Content);

                _newConnection.IpAddress = connection.IpAddress; // ip

                if (object.Equals(connection.IpAddress, "0.0.0.0")) _newConnection.IpAddress = "127.0.0.1";

                _newConnection.Port = connection.Port; // puerto


                switch (message.Type)
                {

                    case (MessageType.User):

                        _clientQueue.Add(_newConnection); // they gonna wait their turn
                        HandlerOnMessageReceived($"Nuevo cliente en espera {_newConnection.IpAddress}:{_newConnection.Port}");

                        SendTurnToClients(_clientQueue); // sends an int

                        if (_serverStatus == Status.Busy) { SendStatusToChosenClient(Status.Busy, _newConnection); }
                        else if (_serverStatus == Status.Waiting) { SendStatusToChosenClient(Status.Waiting, _newConnection); }

                        break;

                    case (MessageType.Processor):

                        _serversWaiting.Add(_newConnection);
                        HandlerOnMessageReceived($"Nuevo server en espera {_newConnection.IpAddress}:{_newConnection.Port}");

                        break;
                }

                if (_serverStatus == Status.Waiting)
                {
                    AddWaitingServers(); // adds and alerts the servers
                }

                if (_serverStatus == Status.Ready)
                {
                    // we gonna work only on the first client in the "queue" , others wait

                    AddWaitingServers(); // add servers if they're waiting 
                    UpdateStatus(Status.Busy);
                    Thread thread = new Thread(WorkOnVideo);
                    thread.Start();

                }
            }
        }



        public async void WorkOnVideo()
        {
            // working on the first if queue not empty

            if (_clientQueue.Count > 0)
            {
                // chose client to work with and dekete it from the list

                Connection connection = _clientQueue.ElementAt(0);
                _clientQueue.Remove(connection);

                // always send turn and status in that order
                SendTurnToClients(_clientQueue);
                SendStatusToClients(Status.Busy); // to all but not the chosen one
                SendStatusToChosenClient(Status.Ready, connection);

                // waiting for the video

                var dataReceived = connection.StreamReader!.ReadLine();
                var message = JsonConvert.DeserializeObject<Message>(dataReceived);
                var video = message.Content as Video;

                // save video 
                _inputPath += video.Format;
                File.WriteAllBytes(_inputPath, video.Data);

                CreateTempFolders();

                //GetVideoMeta(_inputPath);
                //GetVideoAudio(_inputPath, _audioPath, _videoAudioExtension);
                //GetVideoFrames(_inputPath, _framesPath);

                SendImagesToServers(_framesPath);
                WaitForProcessedImages(_expectedImages);

                //CreateVideoWithFramesAndSound(_processedImgsPath, _audioPath, _outputPath, _videoFramerate, _videoExtension, _videoAudioExtension);

                // send the processed video

                Video processedVideo = new Video {
                    Format = _videoAudioExtension,
                    Data = File.ReadAllBytes(_outputPath),
                };

                string json = JsonConvert.SerializeObject(processedVideo);
                connection.StreamWriter?.WriteLine(json);
                connection.StreamWriter?.Flush();

                DeleteTempFolders();
                DeleteVideos();
            }

            AddWaitingServers();
            UpdateStatus(Status.Ready);

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
                c.StreamWriter?.WriteLine(json);
                c.StreamWriter?.Flush();
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

            List<byte[]> images = new List<byte[]>(GetImagesInFolder(framesPath));
            int servers = _serversWorking.Count;

            int limit = servers;
            int numImages = images.Count;
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

                List<byte[]> imagesForServer = new List<byte[]>();

                // asignamos las imagenes a los servers

                for (int i = 0; i < imagesPerServer; i++)
                {
                    imagesForServer.Add(images[0]);
                    images.RemoveAt(0);
                    range++;
                }

                // se reparten las imagenes sobrantes entre los primeros servers

                if (remainingImages > 0)
                {
                    imagesForServer.Add(images[0]);
                    images.RemoveAt(0);
                    remainingImages--;
                    range++;
                }

                int finalRange = range;

                FramesData processedData = new FramesData
                {
                    Range = (initialRange, finalRange),
                    Content = imagesForServer,
                };

                message.Content = processedData;

                var json = JsonConvert.SerializeObject(message);

                _serversWorking[server].StreamWriter?.WriteLine(json);
                _serversWorking[server].StreamWriter?.Flush();
            }

            _expectedImages = limit; // sets the numbr of reponses expceted 
        }



        public void WaitForProcessedImages(int expected)
        {
            int expectedAnswers = expected;

            Thread[] serverThreads = new Thread[expectedAnswers];

            for (int i = 0; i < expectedAnswers; i++)
            {
                serverThreads[i] = new Thread(() => ListenToServers(_serversWorking[i]));
                serverThreads[i].Start();
            }

            foreach (Thread t in serverThreads)
            {
                t.Join();
            }
        }

        public async void ListenToServers(Connection c)
        {
            var messageStream = c.StreamReader?.ReadLine();
            var message = JsonConvert.DeserializeObject<Message>(messageStream);

            if (message.Type == MessageType.ProcessedData)
            {
                var processedData = message.Content as FramesData;
                var part = processedData.Range;
                var images = processedData.Content as List<byte[]>;

                for (int i = part.Item1; i <= part.Item2; i++)
                {
                    // for saving: path\frame%08d.bmp
                    string frameName = $"frame{FrameName(i)}.bmp";
                    File.WriteAllBytes(_processedImgsPath + FrameName, images[i]);
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

                HandlerOnMessageReceived($"Nuevos serves agregados {_serversWaiting.Count}");
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
        }



        public void DeleteVideos()
        {
            string[] videos = Directory.GetFiles(_outputPath);

            foreach (var v in videos)
            {
                File.Delete(v);
            }
        }



        public void CreateTempFolders()
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

            _mainPath = tmpFolderMain;
            _framesPath = tmpFolderFrames;
            _audioPath = tmpFolderAudio;
            _processedImgsPath = tmpFolderProcessedImgs;

        }



        public void DeleteTempFolders()
        {
            Directory.Delete(_framesPath, true);
            Directory.Delete(_audioPath, true);
            Directory.Delete(_processedImgsPath, true);
            Directory.Delete(_mainPath, true);
        }



        //public void GetVideoFrames(string inputPath, string framesPath)
        //{
        //    FFMpegArguments
        //        .FromFileInput(inputPath)
        //        .OutputToFile($"{framesPath}\\frame%08d.bmp") // indicates the nomeclature i.e. 15th frame = frame00000015.bmp
        //        .ProcessSynchronously();

        //    HandlerOnMessageReceived($"Imágenes extraídas con éxito.");
        //}



        //public void GetVideoMeta(string inputPath)
        //{

        //    IMediaAnalysis mediaInfo = FFProbe.Analyse(inputPath);

        //    // if not data return default values

        //    double framerate = mediaInfo.PrimaryVideoStream?.FrameRate ?? 30;
        //    string name = Path.GetFileNameWithoutExtension(inputPath) ?? "Unknown_Video";
        //    string codec = mediaInfo.PrimaryVideoStream?.CodecName ?? "h264";
        //    string videoExtension = Path.GetExtension(inputPath).TrimStart('.') ?? "mp4";
        //    string audioExtension = mediaInfo.PrimaryAudioStream?.CodecName ?? "mp3";

        //    HandlerOnMessageReceived($"Metada extradida");

        //    _videoFramerate = framerate;
        //    _videoName = name;
        //    _videoCodec = codec;
        //    _videoExtension = videoExtension;
        //    _videoAudioExtension = audioExtension;

        //}



        //public void GetVideoAudio(string inputPath, string outputAudioPath, string audioExtension)
        //{
        //    // saving audio according to audio format compatible with video format

        //    string arguments = $"-i \"{inputPath}\" -vn -acodec copy \"{outputAudioPath}\\audio.{audioExtension}\"";

        //    Process ffmpegProcess = new Process
        //    {
        //        StartInfo = new ProcessStartInfo
        //        {
        //            FileName = _ffmpegPathString,
        //            Arguments = arguments,
        //            UseShellExecute = false,
        //            CreateNoWindow = true,
        //            RedirectStandardError = true
        //        }
        //    };

        //    ffmpegProcess.Start();
        //    ffmpegProcess.WaitForExit();

        //    HandlerOnMessageReceived($"Audios extraido con éxito");
        //}



        //public void CreateVideoWithFramesAndSound(string imagePath, string audioInputPath, string videoOutputPath, double frameRate, string videoExtension, string audioExtension)
        //{
        //    string arguments = $"-framerate {frameRate} -i \"{imagePath}\\frame%08d.bmp\" -i \"{audioInputPath}\\audio.{audioExtension}\" -c:v libx264 -pix_fmt yuv420p -c:a {audioExtension} -strict experimental \"{videoOutputPath}\\NEW_VIDEO.{videoExtension}\"";

        //    Process ffmpeg = new Process
        //    {
        //        StartInfo = new ProcessStartInfo
        //        {
        //            FileName = _ffmpegPathString,
        //            Arguments = arguments,
        //            UseShellExecute = false,
        //            CreateNoWindow = true,
        //            RedirectStandardError = true
        //        }
        //    };

        //    ffmpeg.Start();
        //    string errorOutput = ffmpeg.StandardError.ReadToEnd();
        //    ffmpeg.WaitForExit();

        //    if (!string.IsNullOrEmpty(errorOutput))
        //    {
        //        Console.WriteLine("FFmpeg Error Output:");
        //        Console.WriteLine(errorOutput);
        //    }
        //}



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
