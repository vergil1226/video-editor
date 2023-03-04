using NAudio.Wave;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
        private string _outputPath = "";
        public string _videoPath;
        public ObservableCollection<VideoClipControl> VideoClips { get; set; }
        public WaveStream waveStream { get; set; }
        private string _ext = ".mp4";
        public double _duration = 0.0;

        public ExportDialog()
        {
            InitializeComponent();
            Mp4Radio.IsChecked = true;
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

            if (_ext == "")
            {
                System.Windows.MessageBox.Show("Please Select Video Type!", "Program Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (FileNameTextBox.Text.Length == 0)
            {
                System.Windows.MessageBox.Show("Please Enter Video Name!", "Program Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_outputPath == "")
            {
                System.Windows.MessageBox.Show("Please Select Directory!", "Program Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string outStr = _outputPath + '\\' + FileNameTextBox.Text + _ext;

            ExportingGrid.Visibility = Visibility.Visible;
            Exporting.Maximum = (int)(_capture.Fps * _duration);
            Exporting.Value = 0;

            int exportFrameCount = 1;
            DispatcherTimer time = new DispatcherTimer();
            time.Interval = TimeSpan.FromMilliseconds(10);
            time.Start();
            time.Tick += delegate
            {
                Exporting.Value = exportFrameCount;
            };

            List<string> paths = new List<string>();

            try
            {
                await Task.Run(async () =>
                {
                    string mainCommand = "-y -i " + '"' + _videoPath + '"';
                    string vcommand = " -vf " + '"' + "select='";
                    string acommand = " -af " + '"';
                    string aselect = "aselect='";

                    for (int i = 0; i < VideoClips.Count; i++)
                    {
                        for (int j = 0; j < VideoClips[i]._startPos.Count; j++)
                        {
                            if (i != 0 || j != 0)
                            {
                                vcommand += '+';
                                aselect += "+";
                            }
                            string str = "between(t," + VideoClips[i]._startPos[j].ToString() + "," + VideoClips[i]._endPos[j].ToString() + ")";
                            vcommand += str;
                            aselect += str;

                            if (VideoClips[i].isMute)
                            {
                                acommand += "volume=enable='" + str + "':volume=0, ";
                            }
                        }
                    }

                    vcommand += "',setpts=N/FRAME_RATE/TB" + '"';
                    acommand += aselect + "',asetpts=N/SR/TB" + '"';
                    mainCommand += acommand + " " + "output.wav";
                    //mainCommand += vcommand + acommand + " " + '"' + outStr + '"';

                    Process cutProcess = new Process();
                    cutProcess.StartInfo.FileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                    cutProcess.StartInfo.Arguments = mainCommand;
                    cutProcess.StartInfo.UseShellExecute = true;
                    cutProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    cutProcess.Start();
                    cutProcess.WaitForExit();
                    if (cutProcess.ExitCode != 0) throw new Exception($"ffmpeg.exe exited with code {cutProcess.ExitCode}");


                    Process proc = new Process();
                    proc.StartInfo.FileName = @"ffmpeg.exe";
                    proc.StartInfo.Arguments = String.Format("-i output.wav -f image2pipe -framerate {1} -i pipe:.bmp -maxrate {0}k -r {1} -y {2}", 1000, _capture.Fps, '"' + outStr + '"');
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.StartInfo.RedirectStandardInput = true;
                    proc.StartInfo.RedirectStandardOutput = true;

                    proc.Start();

                    for (int i = 0; i < VideoClips.Count; i++)
                    {
                        for (int j = 0; j < VideoClips[i]._startPos.Count; j++)
                        {
                            for (int k = (int)(_capture.Fps * VideoClips[i]._startPos[j]); k < _capture.Fps * VideoClips[i]._endPos[j]; k++)
                            {
                                _capture.PosFrames = k;
                                Mat image = new Mat();
                                _capture.Read(image);
                                if (image.Empty()) continue;

                                Bitmap bmp = image.ToBitmap();
                                if (exportFrameCount >= 10 && exportFrameCount < 20)
                                {
                                    Graphics g = Graphics.FromImage(bmp);
                                    g.FillRectangle(System.Drawing.Brushes.Red, new Rectangle(10, 10, 200, 200));
                                }

                                using (var ms = new MemoryStream())
                                {
                                    bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                                    ms.WriteTo(proc.StandardInput.BaseStream);
                                }
                                exportFrameCount++;
                            }
                        }
                    }

                    proc.StandardInput.Flush();
                    proc.StandardInput.Close();
                    proc.Close();
                    proc.Dispose();

                    System.Windows.MessageBox.Show("Successfully Exported!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                System.Windows.MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                File.Delete("output.wav");
                time.Stop();
            }

            ExportingGrid.Visibility = Visibility.Collapsed;
            this.Visibility = Visibility.Collapsed;
        }

        private void OnExtChecked(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.RadioButton rb = sender as System.Windows.Controls.RadioButton;
            _ext = "." + rb.Content.ToString().ToLower();
        }
    }
}
