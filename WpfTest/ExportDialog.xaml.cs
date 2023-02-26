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
using System.Windows.Controls;
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
        public ObservableCollection<AudioClipControl> AudioClips { get; set; }
        public WaveStream waveStream { get; set; }
        private string _ext = "";

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
            //await FFMpegArguments.FromFileInput("C:\\1234.mp4").OutputToFile("D:\\temp.mp4", true, options => options.WithVideoBitrate(16000)).ProcessAsynchronously();

            ExportingGrid.Visibility = Visibility.Visible;

            List<string> paths = new List<string>();

            try
            {
                await Task.Run(async () =>
                {                
                    int c = 1;

                    for (int i = 0; i < VideoClips.Count; i++)
                    {
                        for (int j = 0; j < VideoClips[i]._startPos.Count; j++)
                        {
                            string command = "-y ";
                            command += " -i " + '"' + _videoPath + '"';
                            command += " -ss ";
                            command += VideoClips[i]._startPos[j].ToString();
                            command += " -to ";
                            command += (VideoClips[i]._endPos[j]).ToString();
                            if (AudioClips[i].isMute) command += " -an";
                            command += " part";
                            command += c.ToString() + _ext;
                            c++;

                            Process cutProcess = new Process();
                            cutProcess.StartInfo.FileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                            cutProcess.StartInfo.Arguments = command;
                            cutProcess.StartInfo.UseShellExecute = true;
                            cutProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            cutProcess.Start();
                            cutProcess.WaitForExit();
                            if (cutProcess.ExitCode != 0) throw new Exception($"ffmpeg.exe exited with code {cutProcess.ExitCode}");
                        }
                    }

                    string joinCommand = "-y";

                    for (int i = 0; i < VideoClips.Count; i++)
                    {
                        if (AudioClips[i].isMute)
                        {
                            string str = "part_" + (i + 1).ToString() + _ext;
                            Process muteProcess = new Process();
                            muteProcess.StartInfo.FileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                            muteProcess.StartInfo.Arguments = "-y -i part" + (i + 1).ToString() + _ext + " -f lavfi -i aevalsrc=0:c=6:s=48000 -shortest " + str;
                            muteProcess.StartInfo.UseShellExecute = true;
                            muteProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                            muteProcess.Start();
                            muteProcess.WaitForExit();
                            paths.Add(str);
                            File.Delete("part" +(i + 1).ToString() + _ext);

                            if (muteProcess.ExitCode != 0) throw new Exception($"ffmpeg.exe exited with code {muteProcess.ExitCode}");
                        }
                        else
                        {
                            paths.Add("part" + (i + 1).ToString() + _ext);
                        }
                        joinCommand += " -i " + paths[i];
                    }

                    c = paths.Count;
                    joinCommand += " -filter_complex " + '"';
                    for (int i = 0; i < c; i++)
                    {
                        joinCommand += '[' + i.ToString() + ":v][" + i.ToString() + ":a]";
                    }
                    joinCommand += "concat=n=" + c.ToString() + ":v=1:a=1" + '"' + " " + '"' + outStr + '"';

                    File.WriteAllLines("join.txt", paths);
                    Process joinProcess = new Process();
                    joinProcess.StartInfo.FileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                    //joinProcess.StartInfo.Arguments = "-y -f concat -safe 0 -i join.txt -c copy " + outStr;
                    joinProcess.StartInfo.Arguments = joinCommand;
                    joinProcess.StartInfo.UseShellExecute = true;
                    joinProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    joinProcess.Start();
                    joinProcess.WaitForExit();
                    if (joinProcess.ExitCode != 0) throw new Exception($"ffmpeg.exe exited with code {joinProcess.ExitCode}");

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
                for (int i = 0; i < paths.Count; i++)
                {
                    File.Delete(paths[i]);
                }
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
