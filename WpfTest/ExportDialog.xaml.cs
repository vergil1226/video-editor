using Microsoft.Win32;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Video_Editor;

namespace WpfTest
{
    /// <summary>
    /// Interaction logic for ExportDialog.xaml
    /// </summary>
    public partial class ExportDialog : System.Windows.Controls.UserControl
    {
        public VideoCapture _capture;
        private VideoWriter writer;
        private string _outputPath;

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

        private void OnExport(object sender, RoutedEventArgs e)
        {
            //int fcc = VideoWriter.FourCC(_capture.FourCC); //'M', 'J', 'P', 'G'
            int fcc = VideoWriter.FourCC('M', 'J', 'P', 'G');
            double fps = _capture.Fps;
            string outStr = _outputPath + '\\' + FileNameTextBox.Text + ".mp4";
            writer = new VideoWriter(outStr, fcc, fps, new OpenCvSharp.Size(_capture.FrameWidth, _capture.FrameHeight), true);

            ExportingGrid.Visibility = Visibility.Visible;
            Exporting.Maximum = _capture.FrameCount;

            DispatcherTimer time = new DispatcherTimer();
            time.Interval = TimeSpan.FromMilliseconds(1);
            time.Start();
            time.Tick += delegate
            {
                _capture.PosFrames = 0;
                for (int i = 0; i < _capture.FrameCount; i++)
                {
                    Mat _image = new Mat();
                    _capture.Read(_image);
                    if (_image.Empty()) continue;
                    writer.Write(_image);
                }
                writer.Dispose();
                ExportingGrid.Visibility = Visibility.Hidden;

                time.Stop();
            };
        }
    }
}
