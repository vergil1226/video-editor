using NAudio.Gui;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfTest
{
    /// <summary>
    /// Interaction logic for VideoClipControl.xaml
    /// </summary>
    public partial class VideoClipControl : System.Windows.Controls.UserControl
    {
        private VideoCapture _capture;
        public double _startPos = 0, _endPos = 0, _clipWidth;
        private int _timeInterval;
        private BitmapImage _firstImage;

        public ObservableCollection<BitmapImage> Thumbnails { get; set; }

        public VideoClipControl(VideoCapture capture, BitmapImage firstImage, double startPos, double endPos, double clipWidth, int timeInterval)
        {
            InitializeComponent();
            Thumbnails = new ObservableCollection<BitmapImage>();
            _capture = capture;
            _startPos = startPos;
            _endPos = endPos;
            _clipWidth = clipWidth;
            _firstImage = firstImage;
            _timeInterval = timeInterval;
            DataContext = this;
            InitClip();
        }

        private void InitClip()
        {
            ThumbnailControl.Width = 200.0 / _timeInterval * (_endPos - _startPos);
            int length = (int)(ThumbnailControl.Width / _clipWidth) + 1;
            Thumbnails.Clear();
            for (int i = 0; i < length; i++)
            {
                Thumbnails.Add(_firstImage);
            }
            SyncThumbnails(length);
        }

        private void SyncThumbnails(int length)
        {
            for (int i = 0; i < length; i++)
            {
                DispatcherTimer time = new DispatcherTimer();
                time.Interval = TimeSpan.FromMilliseconds(10);
                time.Start();
                int j = i;
                time.Tick += async delegate
                {
                    await SyncOne(j);
                    time.Stop();
                };
            }
        }

        private async Task SyncOne(int i)
        {
            int cnt = (int)(ThumbnailControl.Width / _clipWidth) + 1;
            double _half = (double)_capture.FrameCount / cnt / 2;
            BitmapImage bitmapimage = new BitmapImage();
            await GetFrame((int)(i * _capture.FrameCount / cnt + _half), bitmapimage);
            if (i >= Thumbnails.Count) return;
            Thumbnails[i] = bitmapimage;
        }

        private async Task GetFrame(int pos, BitmapImage bitmapimage)
        {
            _capture.PosFrames = pos;
            Mat _image = new Mat();
            _capture.Read(_image);
            if (_image.Empty()) return;
            bitmapimage.BeginInit();
            bitmapimage.StreamSource = _image.Resize(new OpenCvSharp.Size(_clipWidth, 50)).ToMemoryStream();
            bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapimage.EndInit();
        }

        public void SetTimeInterval(int interval) {
            _timeInterval = interval;
            InitClip();
        }
        
        public void UpdateEndPos(double pos)
        {
            _endPos = pos;
            ThumbnailControl.Width = 200.0 / _timeInterval * (_endPos - _startPos);
            int length = (int)(ThumbnailControl.Width / _clipWidth) + 1;
            int curLen = Thumbnails.Count;
            for (int i = length; i < curLen; i++)
                Thumbnails.RemoveAt(length);
        }
    }
}
