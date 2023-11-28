using FFmpeg.AutoGen;
using FFMpegCore;
using FFMpegCore.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2P_UAQ_Server.Managers
{
    public class VideoManager
    {
        // Atributes
        private string _ffmpegPathString = "ffmpeg.exe";

        public double videoFramerate;
        public string? videoName;
        public string? videoCodec;
        public string? videoExtension;
        public string? videoAudioExtension;

        // Constructor

        public VideoManager(string ffmpegPath)
        {
            _ffmpegPathString = ffmpegPath;
        }

        // Methods

        public void GetVideoFrames(string inputPath, string framesPath)
        {
            FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile($"{framesPath}\\frame%08d.bmp") // indicates the nomeclature i.e. 15th frame = frame00000015.bmp
                .ProcessSynchronously();
        }

        public void GetVideoMeta(string inputPath)
        {

            IMediaAnalysis mediaInfo = FFProbe.Analyse(inputPath);

            // if not data return default values

            double framerate = mediaInfo.PrimaryVideoStream?.FrameRate ?? 30;
            string name = Path.GetFileNameWithoutExtension(inputPath) ?? "Unknown_Video";
            string codec = mediaInfo.PrimaryVideoStream?.CodecName ?? "h264";
            string videoEx = Path.GetExtension(inputPath).TrimStart('.') ?? "mp4";
            string audioExtension = mediaInfo.PrimaryAudioStream?.CodecName ?? "mp3";

            videoFramerate = framerate;
            videoName = name;
            videoCodec = codec;
            videoExtension = videoEx;
            videoAudioExtension = audioExtension;

        }

        public void GetVideoAudio(string inputPath, string outputAudioPath)
        {
            // saving audio according to audio format compatible with video format

            string arguments = $"-i \"{inputPath}\" -vn -acodec copy \"{outputAudioPath}\\audio.{videoAudioExtension}\"";
            
            Process ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPathString,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            //ffmpegProcess.Start();

            //if (ffmpegProcess.HasExited)
            //{
            //	ffmpegProcess.Close();
            //	ffmpegProcess.Dispose();
            //}

            //ffmpegProcess.WaitForExit();
            //ffmpegProcess.Close();
            //ffmpegProcess.Dispose();

            try
            {
                ffmpegProcess.Start();

                string output = ffmpegProcess.StandardOutput.ReadToEnd();
                string error = ffmpegProcess.StandardError.ReadToEnd();

                ffmpegProcess.WaitForExit();
                ffmpegProcess.Close();
                ffmpegProcess.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            finally
            {
                ffmpegProcess.Close();
                ffmpegProcess.Dispose();
            }
        }

        public void CreateVideoWithFramesAndSound(string imagePath, string audioInputPath, string videoOutputPath)
        {
            string arguments = $"-framerate {videoFramerate} -i \"{imagePath}\\frame%08d.bmp\" -i \"{audioInputPath}\\audio.{videoAudioExtension}\" -c:v libx264 -pix_fmt yuv420p -c:a {videoAudioExtension} -strict experimental \"{videoOutputPath}\\NEW_VIDEO.{videoExtension}\"";

            Process ffmpeg = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPathString,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                }
            };

            //ffmpeg.Start();
            //string errorOutput = ffmpeg.StandardError.ReadToEnd();

            ////if (ffmpeg.HasExited)
            ////{
            ////	ffmpeg.Close();
            ////	ffmpeg.Dispose();
            ////}
            //ffmpeg.WaitForExit();

            //if (!string.IsNullOrEmpty(errorOutput))
            //{
            //    Console.WriteLine("FFmpeg Error Output:");
            //    Console.WriteLine(errorOutput);
            //}

            try
            {
                ffmpeg.Start();

                string output = ffmpeg.StandardOutput.ReadToEnd();
                string error = ffmpeg.StandardError.ReadToEnd();

                ffmpeg.WaitForExit();
                ffmpeg.Close();
                ffmpeg.Dispose();

            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
            finally
            {
                ffmpeg.Close();
                ffmpeg.Dispose();
            }
        }
    }
}
