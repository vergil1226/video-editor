using NAudio.Wave;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
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
        private string _ext = ".mp4";

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
            //await FFMpegArguments.FromFileInput("C:\\1234.mp4").OutputToFile("D:\\temp.mp4", true, options => options.WithVideoBitrate(16000)).ProcessAsynchronously();

            ExportingGrid.Visibility = Visibility.Visible;

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

                            if (AudioClips[i].isMute)
                            {
                                acommand += "volume=enable='" + str + "':volume=0, ";
                            }
                        }
                    }

                    vcommand += "',setpts=N/FRAME_RATE/TB" + '"';
                    acommand += aselect + "',asetpts=N/SR/TB" + '"';
                    mainCommand += vcommand + acommand + " " + '"' + outStr + '"';

                    Process cutProcess = new Process();
                    cutProcess.StartInfo.FileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
                    cutProcess.StartInfo.Arguments = mainCommand;
                    cutProcess.StartInfo.UseShellExecute = true;
                    cutProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    cutProcess.Start();
                    cutProcess.WaitForExit();
                    if (cutProcess.ExitCode != 0) throw new Exception($"ffmpeg.exe exited with code {cutProcess.ExitCode}");

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
