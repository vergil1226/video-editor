using NAudio.Gui;
using NAudio.Wave;
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
        public double _clipWidth;
        private int _timeInterval;
        private BitmapImage _firstImage;

        public List<double> _startPos { get; private set; }
        public List<double> _endPos{ get; private set; }
        public double _duration = 0;

        public List<float> WaveFormData { get; private set; }
        private int waveFormSize = 0;
        private double maxSpan = 0.0;

        public ObservableCollection<BitmapImage> Thumbnails { get; set; }

        private int initID = 0;

        public bool isMute = false;

        public VideoClipControl(VideoCapture capture, BitmapImage firstImage, double startPos, double endPos, double clipWidth, int timeInterval, List<float> _WaveFormData, int _waveFormSize, double _maxSpan)
        {
            InitializeComponent();
            Thumbnails = new ObservableCollection<BitmapImage>();
            _capture = capture;
            _startPos = new List<double>();
            _endPos = new List<double>();
            _startPos.Add(startPos);
            _endPos.Add(endPos);
            _clipWidth = clipWidth;
            _firstImage = firstImage;
            _timeInterval = timeInterval;

            WaveFormData = _WaveFormData;
            waveFormSize = _waveFormSize;
            maxSpan = _maxSpan;

            DataContext = this;
            InitClip(0);
        }


        private double syncPos = 0;
        private int syncPosId = 0;
        private int syncThumbId = 0;
        private double clipSec = 0;
        private void InitClip(int curID)
        {
            _duration = 0;
            for (int i = 0; i < _startPos.Count; i++)
            {
                _duration += _endPos[i] - _startPos[i];
            }
            ThumbnailControl.Width = 200.0 / _timeInterval * _duration;
            clipSec = _clipWidth * _timeInterval / 200.0;
            int length = (int)(ThumbnailControl.Width / _clipWidth) + 1;
            Thumbnails.Clear();
            for (int i = 0; i < length; i++)
            {
                if (curID != initID) return;
                Thumbnails.Add(_firstImage);
            }

            AudioStack.Children.Clear();

            syncPos = _startPos[0] + clipSec / 2;
            syncPosId = 0;
            syncThumbId = 0;
            SyncThumbnails(length, curID);
        }

        private void SyncThumbnails(int length, int curID)
        {
            for (int i = 0; i < length; i++)
            {
                if (curID != initID) return;
                int ti = i;
                DispatcherTimer time = new DispatcherTimer();
                time.Interval = TimeSpan.FromMilliseconds(1);
                time.Start();
                time.Tick += async delegate
                {
                    time.Stop();
                    if (curID != initID) return;
                    await SyncOne(length);
                    double interval = (_endPos[0] - _startPos[0]) / length;
                    var audioClip = new AudioClipControl(WaveFormData, waveFormSize, maxSpan, ThumbnailControl.Width / length, _startPos[0] + ti * interval, _startPos[0] + (ti + 1) * interval, _capture.FrameCount / _capture.Fps);
                    AudioStack.Children.Add(audioClip);
                };
            }
        }

        private async Task SyncOne(int length)
        {
            if (syncPos > _endPos[syncPosId])
            {
                if (syncPosId + 1 >= _startPos.Count) return;
                syncPos = _startPos[syncPosId + 1] + syncPos - _endPos[syncPosId];
                syncPosId++;
            }

            BitmapImage bitmapimage = new BitmapImage();
            await GetFrame((int)(_capture.Fps * syncPos), bitmapimage);
            if (syncThumbId >= Thumbnails.Count) return;
            Thumbnails[syncThumbId] = bitmapimage;
            syncThumbId++;
            syncPos += clipSec;
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
            initID++;
            InitClip(initID);
        }
        
        public void UpdateEndPos(double pos)
        {
            int i;
            for (i = _endPos.Count - 1; i >= 0; i--)
            {
                if (pos == _startPos[i])
                {
                    _startPos.Remove(i);
                    _endPos.Remove(i);
                    break;
                }
                if (pos == _endPos[i])
                {
                    break;
                }
                if (_endPos[i] > pos && pos > _startPos[i])
                {
                    _endPos[i] = pos;
                    break;
                }
                _startPos.RemoveAt(i);
                _endPos.RemoveAt(i);
            }

            _duration = 0;
            for (i = 0; i < _startPos.Count; i++)
            {
                _duration += _endPos[i] - _startPos[i];
            }

            //ThumbnailControl.Width = 200.0 / _timeInterval * _duration;
            //AudioStack.Width = ThumbnailControl.Width;
            InitClip(++initID);
        }

        public double GetCurrentSec(double pos)
        {
            double sec = pos * _timeInterval / 200.0;
            for (int i = 0; i < _startPos.Count; i++)
            {
                if (sec <= _endPos[i] - _startPos[i])
                {
                    return _startPos[i] + sec;
                }
            }
            return 0;
        }

        public double GetCurrentPos(double sec)
        {
            double pos = 0;
            for (int i = 0; i < _startPos.Count;i++)
            {
                if (_startPos[i] <= sec && sec <= _endPos[i])
                {
                    pos += sec - _startPos[i];
                    return ThumbnailControl.Width * pos / _duration;
                }
                pos += _endPos[i] - _startPos[i];
            }
            return -1;
        }
    }
}
