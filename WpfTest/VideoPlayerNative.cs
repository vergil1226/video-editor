using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media.Imaging;
using OpenCvSharp;
namespace WpfTest
{
    enum BitamapState { EMPTY = 0, NEW_MAT = 1, CONVERTED = 2, OLD = 3}
    class BitmapUnit
    {
        public BitmapUnit() {

        }
        public BitmapImage shot = new BitmapImage();
        public readonly object mut = new object();
        public Mat frame = new Mat();
        public BitamapState state = BitamapState.EMPTY;
    }

    class CircleBitmapArray
    {
        private BitmapUnit[] _frameBuff = new BitmapUnit[6];
        private int _curItem = 0;
        private int _lastItem = 0;
        private int _convertItem = 0;
        private object _mut = new object();

        public CircleBitmapArray()
        {
            for(int i = 0; i < 6; ++i)
            {
                _frameBuff[i] = new BitmapUnit();
            }
        }
        public int reserveForConvert()
        {
            if (_convertItem == _lastItem)
            {
                return -1;
            }
            lock (_mut)
            {
                if (_convertItem == _lastItem)
                {
                    return -1;
                }
                else
                {
                    int res = _convertItem;
                    _convertItem = (_convertItem + 1) % 6;
                    return res;
                }
            }
        }

        public ref BitmapUnit getForConvert(int num)
        {
            return ref _frameBuff[num];
        }

        public int getCurImage(ref BitmapImage shot)
        {
            if (_frameBuff[_curItem].state == BitamapState.CONVERTED)
            {
                shot = _frameBuff[_curItem].shot.Clone();
                //_frameBuff[_curItem].state = BitamapState.OLD;
                pop();
                return 0;
            }
            return -1;
        }

        public BitamapState getCurItemState()
        {
            return _frameBuff[_curItem].state;
        }

        public int getSize()
        {
            if(_curItem <= _lastItem)
            {
                return _lastItem - _curItem;
            } else
            {
                return 6 - (_curItem - _lastItem);
            }
        }

        public int push(Mat shot)
        {
            lock(_mut)
            {
                if (getSize() == 5)
                    return -1;
                _frameBuff[_lastItem].frame = shot;
                _frameBuff[_lastItem].state = BitamapState.NEW_MAT;
                _lastItem = (_lastItem + 1) % 6;
                return 0;
            }
        }

        public void pop()
        {
            _frameBuff[_curItem].state = BitamapState.EMPTY;
            Interlocked.Exchange(ref _curItem, (_curItem + 1) % 6);
        }

        public void clear()
        {
            lock (_mut)
            {
                _curItem = _lastItem = _convertItem = 0;
            }
        }
    }

    class VideoDecoderNative
    {
        private VideoCapture _videoCapture = new VideoCapture();
        int _convertThreadCount = 3;
        int _threadWaitCount = 0;
        int _threadEnd = 0;
        private CircleBitmapArray _frameBuff = new CircleBitmapArray();
        bool _stop = true;
        bool _seek = false;
        bool _isEnd = false;
        readonly object _wait = new object();
        public VideoDecoderNative()
        {   
        }

        public void setUrl(string url)
        {
            _videoCapture.Open(url);
        }

        public void start()
        {
            _stop = false;
            new Thread(new ThreadStart(readThread)).Start();
            new Thread(new ThreadStart(convertThread)).Start();
            new Thread(new ThreadStart(convertThread)).Start();
            new Thread(new ThreadStart(convertThread)).Start();
        }

        public void stop()
        {
            if (_stop)
                return;
            _stop = true;
            while (_threadEnd != _convertThreadCount + 1)
            {
                Thread.Sleep(20);
            }
            _threadEnd = 0;
            _frameBuff.clear();
        }

        public void seek(double _curTime)
        {
            _seek = true;
            while(_threadWaitCount != _convertThreadCount + 1)
            {
                Thread.Sleep(20);
            }
            _videoCapture.PosFrames = (int)(_curTime * _videoCapture.Fps);
            _frameBuff.clear();
            _threadWaitCount = 0;
            _seek = false;
            _isEnd = false;
            lock (_wait)
            {
                Monitor.PulseAll(_wait);
            }
        }

        public void readThread()
        {
            while (!_stop)
            {
                if (_seek)
                {
                    lock (_wait)
                    {
                        _threadWaitCount += 1;
                        Monitor.Wait(_wait);
                    }
                }
                else
                {
                    if(_isEnd)
                    {
                        Thread.Sleep(20);
                        continue;
                    }
                    int curSize = _frameBuff.getSize();
                    if (curSize != 5)
                    {
                        Mat frame = new Mat();
                        _videoCapture.Read(frame);
                        if (!frame.Empty())
                        {
                            _frameBuff.push(frame);
                        }
                        else
                        {
                            _isEnd = true;
                        }
                    }
                    else
                    {
                        Thread.Sleep(20);
                    }
                }
            }
            Interlocked.Increment(ref _threadEnd);
        }

        public int getCurFrame(ref BitmapImage image)
        {
            if(_frameBuff.getSize() != 0)
            {
                return _frameBuff.getCurImage(ref image);
            }
            else return -1;
        }

        public bool isEnd() { return _isEnd; }

        private void convertThread()
        {
            int curConvertItem = 0;
            while (!_stop)
            {
                if(_seek)
                {
                    lock(_wait)
                    {
                        _threadWaitCount += 1;
                        Monitor.Wait(_wait);
                    }
                }
                curConvertItem = _frameBuff.reserveForConvert();
                if(curConvertItem >= 0)
                {
                    BitmapUnit curUnit = _frameBuff.getForConvert(curConvertItem);
                    curUnit.shot = new BitmapImage();
                    curUnit.shot.BeginInit();
                    curUnit.shot.StreamSource = curUnit.frame.ToMemoryStream();
                    curUnit.shot.CacheOption = BitmapCacheOption.OnLoad;
                    curUnit.shot.EndInit();
                    curUnit.shot.Freeze();
                    curUnit.state = BitamapState.CONVERTED;
                } else
                {
                    Thread.Sleep(20);
                }
            }
            Interlocked.Increment(ref _threadEnd);
        }

        private void clear()
        {

        }

    }
}
