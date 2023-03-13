using NAudio.Gui;
using NAudio.Wave;
using OpenCvSharp;
using OpenCvSharp.XFeatures2D;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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

        public List<BitmapImage> _buffer;
        public List<int> _bufferPos;
        public ObservableCollection<BitmapImage> Thumbnails { get; set; }

        private int initID = 0;

        public bool isMute = false;

        public VideoClipControl(VideoCapture capture, BitmapImage firstImage, double startPos, double endPos, double clipWidth,
            int timeInterval, List<float> _WaveFormData, int _waveFormSize, double _maxSpan,
            List<BitmapImage> buffer = null, List<int> bufferPos = null)
        {
            InitializeComponent();
            Thumbnails = new ObservableCollection<BitmapImage>();
            _capture = capture;
            _startPos = new List<double>();
            _endPos = new List<double>();
            if (buffer == null)
            {
                _buffer = new List<BitmapImage>();
            } else
            {
                _buffer = buffer;
            }
            if(bufferPos == null)
            {
                _bufferPos = new List<int>();
            } else
            {
                _bufferPos = bufferPos;
            }
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

        private async Task InitClip(int curID)
        {
            _duration = 0;
            for (int i = 0; i < _startPos.Count; i++)
            {
                _duration += _endPos[i] - _startPos[i];
            }
            ThumbnailControl.Width = 200.0 / _timeInterval * _duration;
            int length = (int)(ThumbnailControl.Width / _clipWidth) + 1;
            Thumbnails.Clear();
            for (int i = 0; i < length; i++)
            {
                if (curID != initID) return;
                Thumbnails.Add(_firstImage);
            }

            AudioStack.Children.Clear();

            await SyncThumbnails(length, curID);
        }

        private async Task SyncThumbnails(int length, int curID)
        {
            for (int i = 0; i < length; i++)
            {
                if (curID != initID) return;
                double interval = (_endPos[0] - _startPos[0]) / length;
                double pos = (_startPos[0] + interval * i + interval / 2) * _capture.Fps;
                await Task.Delay(100);
                if (curID != initID) return;
                Thumbnails[i] = GetBufferedFrame((int)pos);
                var audioClip = new AudioClipControl(WaveFormData, waveFormSize, maxSpan, ThumbnailControl.Width / length, 
                    _startPos[0] + i * interval, _startPos[0] + (i + 1) * interval, _capture.FrameCount / _capture.Fps);
                if (curID != initID) return;
                AudioStack.Children.Add(audioClip);
            }
        }

        private BitmapImage GetFrame(int pos)
        {
            BitmapImage bitmapImage = new BitmapImage();
            _capture.PosFrames = pos;
            Mat _image = new Mat();
            _capture.Read(_image);
            if (_image.Empty()) return bitmapImage;
            MemoryStream ms = _image.Resize(new OpenCvSharp.Size(_clipWidth, 50)).ToMemoryStream();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = ms;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            return bitmapImage;
        }

        int Compare(int x, int y)
        {
            return 0;
        }

        private BitmapImage GetBufferedFrame(int _pos)
        {
            int buffPosIndex = -1;
            for(int i = 0; i < _bufferPos.Count; ++i)
            {
                if (_bufferPos[i] == _pos)
                {
                    buffPosIndex = i;
                    break;
                }
                if(i > 0 && _bufferPos[i - 1] < _pos && _bufferPos[i] > _pos)
                {
                    if((_pos - _bufferPos[i - 1]) <= (_bufferPos[i] - _pos))
                    {
                        buffPosIndex = i - 1;
                    } else
                    {
                        buffPosIndex = i;
                    }
                    break;
                }
            }
            if(buffPosIndex == -1)
            {
                buffPosIndex = _bufferPos.Count - 1;
            }
            return _buffer[buffPosIndex];
        }

        public async Task Init(double _maxInterval)
        {
            _duration = _endPos[0] - _startPos[0];
            double maxLendWidth = 200.0 / _maxInterval * _duration;
            int length = (int)(maxLendWidth / _clipWidth) + 1;
            double interval = (_endPos[0] - _startPos[0]) / length;
            for (int i = 0; i < length; i++)
            {
                double pos = Math.Round((_startPos[0] + interval * i + interval / 2) * _capture.Fps);
                await Task.Delay(5);
                _bufferPos.Add((int)pos);
                _buffer.Add(GetFrame((int)pos));
            }
            await Task.Delay(5);
        }

        public async Task SetTimeInterval(int interval) {
            _timeInterval = interval;
            initID++;
            await InitClip(initID);
            await Task.Delay(100);
        }
        
        public async void UpdateEndPos(double pos)
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
            await InitClip(++initID);
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
