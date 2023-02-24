using FFMpegCore;
using FFMpegCore.Enums;
using FFMpegCore.Exceptions;
using FFMpegCore.Pipes;
using NAudio.Wave;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using static WpfTest.Utils;

namespace WpfTest
{
    /// <summary>
    /// Interaction logic for ExportDialog.xaml
    /// </summary>

    public partial class ExportDialog : System.Windows.Controls.UserControl
    {
        public VideoCapture _capture;
        private string _outputPath;
        public ObservableCollection<VideoClipControl> VideoClips { get; set; }
        public WaveStream waveStream { get; set; }

        public ExportDialog()
        {
            InitializeComponent();
        }

        private void OnClose(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void OnFile(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderDialog = new FolderBrowserDialog();
            folderDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            DialogResult result = folderDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                string _directory = folderDialog.SelectedPath;
                _outputPath = _directory;
                int i = _directory.LastIndexOf('\\');
                FolderNameLabel.Content = _directory.Substring(i + 1, _directory.Length - i - 1);
            }
        }

        private async void OnExport(object sender, RoutedEventArgs e)
        {
            string outStr = _outputPath + '\\' + FileNameTextBox.Text + ".mp4";
            //await FFMpegArguments.FromFileInput("C:\\1234.mp4").OutputToFile("D:\\temp.mp4", true, options => options.WithVideoBitrate(16000)).ProcessAsynchronously();

            ExportingGrid.Visibility = Visibility.Visible;

            await Task.Run(async () =>
            {
                var audioWriter = new WaveFileWriter("input.wav", waveStream.WaveFormat);
                var videoWriter = new VideoWriter("temp.mp4", (FourCC)_capture.Get(VideoCaptureProperties.FourCC), _capture.Fps, new OpenCvSharp.Size(_capture.FrameWidth, _capture.FrameHeight), true);

                for (int i = 0; i < VideoClips.Count; i++)
                {
                    for (int j = 0; j < VideoClips[i]._startPos.Count; j++)
                    {
                        _capture.PosFrames = (int)(_capture.Fps * VideoClips[i]._startPos[j]);
                        for (int k = _capture.PosFrames; k < _capture.Fps * VideoClips[i]._endPos[j]; k++)
                        {
                            Mat _image = new Mat();
                            _capture.Read(_image);
                            if (_image.Empty()) continue;
                            videoWriter.Write(_image);
                        }

                        var start = (long)((VideoClips[i]._startPos[j] / waveStream.TotalTime.TotalSeconds) * waveStream.Length);
                        var end = (long)((VideoClips[i]._endPos[j] / waveStream.TotalTime.TotalSeconds) * waveStream.Length);

                        Func<double> alignStart = () => start / (double)waveStream.WaveFormat.BlockAlign;
                        Func<double> alignEnd = () => end / (double)waveStream.WaveFormat.BlockAlign;

                        while (alignStart() != (int)alignStart())
                        {
                            start += 1;
                        }

                        while (alignEnd() != (int)alignEnd())
                        {
                            end += 1;
                        }

                        waveStream.Position = start;
                        byte[] buffer = new byte[1024];
                        while (waveStream.Position < end)
                        {
                            int bytesRequired = (int)(end - waveStream.Position);
                            if (bytesRequired > 0)
                            {
                                int bytesToRead = Math.Min(bytesRequired, buffer.Length);
                                int bytesRead = waveStream.Read(buffer, 0, bytesToRead);
                                if (bytesRead > 0)
                                {
                                    await audioWriter.WriteAsync(buffer, 0, bytesRead);
                                }
                            }
                        }
                    }
                }
                audioWriter.Dispose();
                videoWriter.Dispose();

                await FFMpegArguments.FromFileInput("temp.mp4").OutputToFile("input.mp4").ProcessAsynchronously();
                FFMpeg.ReplaceAudio("input.mp4", "input.wav", outStr);

                File.Delete("input.wav");
                File.Delete("input.mp4");
                File.Delete("temp.mp4");
            });
            ExportingGrid.Visibility = Visibility.Collapsed;
            this.Visibility = Visibility.Collapsed;
        }
    }
}
