﻿using NAudio.Gui;
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

        public ObservableCollection<BitmapImage> Thumbnails { get; set; }

        public VideoClipControl(VideoCapture capture, BitmapImage firstImage, double startPos, double endPos, double clipWidth, int timeInterval)
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
            DataContext = this;
            InitClip();
        }


        private double syncPos = 0;
        private int syncPosId = 0;
        private int syncThumbId = 0;
        private double clipSec = 0;
        private void InitClip()
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
                Thumbnails.Add(_firstImage);
            }

            syncPos = _startPos[0] + clipSec / 2;
            syncPosId = 0;
            syncThumbId = 0;
            SyncThumbnails(length);
        }

        private void SyncThumbnails(int length)
        {
            for (int i = 0; i < length; i++)
            {
                DispatcherTimer time = new DispatcherTimer();
                time.Interval = TimeSpan.FromMilliseconds(10);
                time.Start();
                time.Tick += async delegate
                {
                    await SyncOne(length);
                    time.Stop();
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
            InitClip();
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

            ThumbnailControl.Width = 200.0 / _timeInterval * _duration;
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