using NAudio.Wave;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Video_Editor;
using static WpfTest.VideoDecoderNative;
using static WpfTest.Utils;
using System.Text;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WpfTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public class Line
    {
        public System.Windows.Point From { get; set; }

        public System.Windows.Point To { get; set; }
    }

    public partial class MainWindow : System.Windows.Window
    {
        private VideoCapture _capture;
        private VideoCapture _captureForPlay;
        DispatcherTimer _timer  = new DispatcherTimer();
        DispatcherTimer _player = new DispatcherTimer();
        BitmapImage _firstImage = new BitmapImage();
        BitmapImage _curImage   = new BitmapImage();
        long _curPts = 0;
        private bool _run = false, _fullscreen = false, _altPressed = false, _mute = false, _maximize = false, _mediaLoaded = false;
        private double _clipWidth, _cutlinePosX, _duration, _curDuraiton, _curSec = 0.0, _seekTime = -1.0;
        private System.Windows.Point _positionInBlock;
        private int[] _timeIntervals = new int[10] { 7200, 3600, 1200, 600, 300, 120, 60, 30, 20, 5 };
        private string _videoFile = "";
        private bool _threadClose = false;
        private VideoDecoderNative _decoder = new VideoDecoderNative();
        private long _prevWatch = -1;
        Stopwatch stopwatch;

        public ObservableCollection<VideoClipControl> VideoClips { get; set; }

        public ObservableCollection<Line> Lines { get; private set; }

        public ObservableCollection<string> Times { get; private set; }

        public WaveOut Player { get; } = new WaveOut()
        {
            NumberOfBuffers = 112,
            DesiredLatency = 10
        };

        public MainWindow()
        {
            InitializeComponent();

            mediaInputWindowInit.Visibility = Visibility.Visible;

            m_toolbar.Visibility = Visibility.Hidden;
            mediaInputWindow.Visibility = Visibility.Hidden;
            mediaEditWindow.Visibility = Visibility.Hidden;

            mediaInputTimeline.Visibility = Visibility.Hidden;
            mediaEditTimeline.Visibility = Visibility.Hidden;

            VideoClips = new ObservableCollection<VideoClipControl>();
            Lines = new ObservableCollection<Line>();
            Times = new ObservableCollection<string>();
            DataContext = this;

            _timer.Interval = TimeSpan.FromMilliseconds(50);
            _timer.Tick += new EventHandler(ticktock);
            _timer.Start();

            for (int i = 0; i < 10000; i++)
            {
                Lines.Add(new Line { From = new System.Windows.Point(20 * i, 5 - (i % 5 == 0 ? 5 : 0)), To = new System.Windows.Point(20 * i, 10) });
            };
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _threadClose = true;
        }

        private void ImportVideo_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Video Files (*.mp4, *.avi, *.wmv)|*.mp4;*.avi;*.wmv";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            if (openFileDialog.ShowDialog() == true)
            {
                BgGrid.Children.Add(new ImportingModalDialog());
                DispatcherTimer time = new DispatcherTimer();
                time.Interval = TimeSpan.FromMilliseconds(100);
                time.Start();
                time.Tick += async delegate
                {
                    time.Stop();
                    // Import the video file
                    _videoFile = openFileDialog.FileName;
                    _capture = new VideoCapture(_videoFile);
                    _captureForPlay = new VideoCapture(_videoFile);
                    _mediaLoaded = true;

                    _clipWidth = 50 * _capture.FrameWidth / _capture.FrameHeight;
                    _duration = _capture.FrameCount / _capture.Fps;
                    _curDuraiton = _duration;
                    Text2.Text = GetFormatTime(_duration);
                    TimeSlider.Minimum = 0;
                    TimeSlider.Maximum = _duration;

                    int mi = 0;
                    for (int i = 1; i < 10; i++)
                    {
                        if (Math.Abs(_timeIntervals[mi] - _duration) > Math.Abs(_timeIntervals[i] - _duration))
                        {
                            mi = i;
                        }
                    }
                    ZoomSlider.Value = Math.Min(mi + 2, 9);

                    BitmapImage _First = new BitmapImage();
                    GetFrame(_captureForPlay, 0, _First, true);
                    _firstImage = _First;
                    InitWidth();
                    await ConvertLoad();
                    BgGrid.Children.Clear();

                    stopwatch = new Stopwatch();
                    BitmapImage _first = new BitmapImage();
                    GetFrame(_captureForPlay, 0, _first, false);
                    VideoShow.Source = _first;

                    m_toolbar.Visibility = Visibility.Hidden;
                    mediaEditWindow.Visibility = Visibility.Visible;
                    mediaInputWindow.Visibility = Visibility.Hidden;

                    mediaInputTimeline.Visibility = Visibility.Hidden;
                    mediaEditTimeline.Visibility = Visibility.Visible;
                    _decoder.stop();
                    _decoder.setUrl(_videoFile);
                    _decoder.start();
                    _threadClose = false;
                    new Thread(new ThreadStart(ShowPlayed)).Start();
                };
            }
        }

        private void ShowPlayed()
        {
            Stopwatch tm = new Stopwatch();
            long oldStp = 0;
            bool setaudio = false;
            bool showOne = false;
            while (!_threadClose) {
                if(_seekTime >= 0.0)
                {
                    _decoder.seek(_seekTime);
                    oldStp = (long)(_seekTime*1000);
                    _curPts = (long)(_seekTime * _captureForPlay.Fps);
                    if (_run)
                        tm.Restart();
                    else
                    {
                        tm.Stop();
                        showOne = true;
                    }
                    _seekTime = -1.0;
                    setaudio = true;
                }
                if (_run || showOne)
                {
                    if(showOne)
                    {
                        if (_decoder.getCurFrame(ref _curImage) == 0)
                        {
                            _curImage.Freeze();
                            Dispatcher.Invoke((Action)(() =>
                            {
                                VideoShow.Source = _curImage;
                                _curSec = _curPts / _captureForPlay.Fps;
                                if (setaudio)
                                {
                                    WaveStream.CurrentTime = new TimeSpan(0, 0, 0, (int)_curSec, (int)(_curSec * 1000) % 1000);
                                    setaudio = false;
                                }
                                
                                _curPts += 1;
                            }));
                            showOne = false;
                        }
                        else
                        {
                            if (_decoder.isEnd())
                            {
                                Thread.Sleep((int)20);
                                showOne = false;
                            }
                        }
                        continue;
                    }
                    if(!tm.IsRunning)
                    {
                        tm.Restart();
                    }
                    long curSec = oldStp + tm.ElapsedMilliseconds;
                    long delay = (long)(_curPts * 1000 / _captureForPlay.Fps) - curSec;
                    if (delay > 0)
                    {
                        Thread.Sleep((int)delay);
                    }

                    if (_decoder.getCurFrame(ref _curImage) == 0)
                    {
                        _curImage.Freeze();
                        Dispatcher.Invoke((Action)(() =>
                        {
                            VideoShow.Source = _curImage;
                            _curPts += 1;
                            _curSec = _curPts / _captureForPlay.Fps;
                            if(setaudio)
                            {
                                WaveStream.CurrentTime = new TimeSpan(0, 0, 0, (int)_curSec, (int)(_curSec * 1000) % 1000);
                                setaudio = false;
                            }
                        }));
                    } else
                    {
                        if(_decoder.isEnd())
                        {
                            Thread.Sleep((int)20);
                        }
                    }
                } else
                {
                    if(tm.IsRunning)
                    {
                        oldStp += tm.ElapsedMilliseconds;
                        tm.Stop();
                    }
                    Thread.Sleep(10);
                }
                //WaveStream.CurrentTime = new TimeSpan(0, 0, 0, (int)_curSec, (int)(_curSec * 1000) % 1000);
            }
            _decoder.stop();
        }

        private int GetFrame(VideoCapture capture, int pos, BitmapImage bitmapimage, bool isFirst)
        {
            if(pos != -1)
                capture.PosFrames = pos;
            Mat _image = new Mat();
            capture.Read(_image);
            if (_image.Empty()) return -1;
            bitmapimage.BeginInit();
            if (isFirst)
                bitmapimage.StreamSource = _image.Resize(new OpenCvSharp.Size(_clipWidth, 50)).ToMemoryStream();
            else 
                bitmapimage.StreamSource = _image.ToMemoryStream();
            bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapimage.EndInit();
            return 0;
        }

        void ticktock(object sender, EventArgs e)
        {
            if (!_mediaLoaded) return;

            if ((_duration - _curSec) < 0.00001)
            {
                _run = false;
                Player.Stop();
                _seekTime = 0.0;
                Play.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/me_play.png", UriKind.Relative));
            }

            double sec = _curSec;
            TimeSlider.Value = _curSec;
            Text1.Text = GetFormatTime(sec);
            CutLabel.Content = GetFormatTime(sec);
            

            if (CutButton.IsMouseCaptured) return;

            int i;
            if (_run)
            {
                for (i = 0; i < VideoClips.Count - 1; i++)
                {
                    if (sec > VideoClips[i]._endPos[^1] && sec < VideoClips[i + 1]._startPos[0])
                    {
                        sec = VideoClips[i + 1]._startPos[0];
                        _seekTime = sec;
                        break;
                    }
                }
            }

            for (i = 0; i < VideoClips.Count; i++)
            {
                double pos = VideoClips[i].GetCurrentPos(sec);
                if (pos < 0) continue;
                System.Windows.Point relativePoint = VideoClips[i].TransformToAncestor(ClipStack).Transform(new System.Windows.Point(0, 0));
                _cutlinePosX = relativePoint.X + pos - TimeLineScroll.HorizontalOffset;
                Player.Volume = VideoClips[i].isMute ? 0 : 1;
                break;
            }

            /*if (_cutlinePosX < 0)
            {
                _cutlinePosX = 0;
            }
            if (_cutlinePosX > TimeLineScroll.ActualWidth)
            {
                _cutlinePosX = TimeLineScroll.ActualWidth;
            }*/

            TranslateTransform _transform = new TranslateTransform(_cutlinePosX, 0);
            CutButton.RenderTransform = _transform;
            CutLine.RenderTransform = _transform;
            CutLabel.RenderTransform = _transform;
        }

        private void InitWidth()
        {
            double _width = 200.0 / _timeIntervals[(int)ZoomSlider.Value] * _curDuraiton;
            LineControl.Width = ((int)(_width / TimeLineScroll.ActualWidth) + 1) * TimeLineScroll.ActualWidth;
            ClipScroll.Width = LineScroll.Width;
            TimeScroll.Width = LineControl.Width;

            SyncTimeLine();
            ticktock(null, null);
        }

        private void SyncTimeLine()
        {
            Times.Clear();
            for (int i = 0; i < 1000; i++)
            {
                Times.Add(GetFormatTime(i * _timeIntervals[(int)ZoomSlider.Value]));
            }
        }

        private async Task ConvertLoad()
        {
            string wavLocation = System.IO.Path.GetDirectoryName(Environment.ProcessPath) + "\\temp";
            while (File.Exists(wavLocation + ".wav"))
            {
                File.Delete(wavLocation + ".wav");
            }
            GC.Collect();

            wavLocation = wavLocation + ".wav";
            var exitCode = -1;
            
            try
            {
                await Task.Run(() =>
                {
                    var ffmpeg = new Process();
#if false //DEBUG
                    ffmpeg.StartInfo.FileName = "cmd.exe";
                    ffmpeg.StartInfo.Arguments = $"/K ffmpeg.exe -y -i \"{dlg.FileName}\" -c copy -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{chunkSaveLocation}.wav\"";
                    ffmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
#else
                    ffmpeg.StartInfo.FileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe ");
                    ffmpeg.StartInfo.Arguments = $"-y -i \"{_videoFile}\" -c copy -vn -acodec pcm_s16le -ar 16000 -ac 1 \"{wavLocation}\"";
                    ffmpeg.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
#endif
                    ffmpeg.StartInfo.UseShellExecute = true;
                    ffmpeg.Start();
                    ffmpeg.WaitForExit();
                    exitCode = ffmpeg.ExitCode;
                });


                if (exitCode == 0)
                {
                    await SetWaveStream(wavLocation);
                }
                else
                    throw new Exception($"ffmpeg.exe exited with code {exitCode}");
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                System.Windows.MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public WaveStream WaveStream { get; private set; }
        public List<float> WaveFormData { get; private set; }

        public double MaxZoom { get; private set; } = 1.0;
        public double MinZoom { get; private set; } = 1.0;

        private long waveSize = 0L;
        private int waveFormSize = 0;
        private TimeSpan waveDuration = TimeSpan.Zero;
        private double maxSpan = 0.0;

        public async Task<bool> SetWaveStream(string fileName)
        {
            await Task.Yield();

            WaveStream = null;
            WaveFormData = null;
            waveSize = 0L;
            waveFormSize = 0;
            waveDuration = TimeSpan.Zero;

            try
            {
                var waveStream = new WaveFileReader(fileName);
                if (waveStream.WaveFormat.Channels != 1 || waveStream.WaveFormat.SampleRate != 16000)
                {
                    throw new FileFormatException("Input should be 16kHz Mono WAV file.");
                }
                var wave = new WaveChannel32(waveStream);

                if (wave == null)
                    return false;

                waveStream.Position = 0L;
                waveDuration = waveStream.TotalTime;

                int sampleSize = 0;

                sampleSize = (from i in Utils.Range(1, 512)
                              let align = wave.WaveFormat.BlockAlign * (double)i
                              let v = wave.Length / align
                              where v == (int)v
                              select (int)align).Max();

                var bufferSize = (int)(wave.Length / (double)sampleSize);
                int read = 0;

                waveSize = waveStream.Length;

                MaxZoom = Math.Max(wave.TotalTime.TotalSeconds / 4, MinZoom);

                var maxWidth = Math.Min(WpfScreen.AllScreens().OrderByDescending(s => s.WorkingArea.Width).First().WorkingArea.Width * MaxZoom, waveSize);

                var iter = 0;
                var waveFormData = new float[(int)(maxWidth * 2)];

                await Task.Run(() =>
                {
                    while (wave.Position < wave.Length)
                    {
                        var rwaIndex = 0;
                        var rawWaveArray = new float[bufferSize / 4];

                        var buffer = new byte[bufferSize];
                        read = wave.Read(buffer, 0, bufferSize);

                        for (int i = 0; i < read / 4; i++)
                        {
                            var point = BitConverter.ToSingle(buffer, i * 4);
                            rawWaveArray[rwaIndex++] = point;
                        }
                        buffer = null;

                        var wl = rawWaveArray.ToList();
                        var rwaCount = rawWaveArray.Length;

                        var samplesPerPixel = (rwaCount / (maxWidth / sampleSize));

                        var writeOffset = (int)((maxWidth / sampleSize) * iter);
                        for (int i = 0; i < (int)(maxWidth / sampleSize); i++)
                        {
                            var offset = (int)(samplesPerPixel * i);
                            var drawableSample = wl.GetRange(offset, Math.Min((int)samplesPerPixel, read)).ToArray();
                            waveFormData[(i + writeOffset) * 2] = drawableSample.Max();
                            waveFormData[((i + writeOffset) * 2) + 1] = drawableSample.Min();
                            drawableSample = null;
                        }

                        wl.Clear();
                        wl = null;
                        iter++;
                    }
                });

                maxSpan = waveFormData.Max() - waveFormData.Min();
                Player.Init(waveStream);
                waveStream.Position = 0L;
                WaveStream = waveStream;
                WaveFormData = waveFormData.ToList();
                waveFormSize = waveFormData.Length;

                VideoClipControl clip = new VideoClipControl(_capture, _firstImage, 0, _duration, _clipWidth, _timeIntervals[(int)ZoomSlider.Value], WaveFormData, waveFormSize, maxSpan);
                VideoClips.Add(clip);
                ClipStack.Children.Add(clip);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                WaveStream = null;
                WaveFormData = null;
                waveSize = 0L;
                waveFormSize = 0;
                waveDuration = TimeSpan.Zero;
                Logger.Log($"{ex.Message}:\n{ex.StackTrace}", Logger.Type.Error);
                System.Windows.MessageBox.Show(ex.Message, "Program Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            GC.Collect();

            var success = (WaveStream != null);

            return success;
        }

        private string GetFormatTime(double t)
        {
            int tt = (int)t;
            return (tt / 3600).ToString("D2") + ":" + ((tt % 3600) / 60).ToString("D2") + ":" + (tt % 60).ToString("D2") + ":" + (((int)(t * 100)) % 100).ToString("D2");
        }

        private void Button_Play(object sender, RoutedEventArgs e)
        {
            _run = !_run;
            if (_run)
            {
                _prevWatch = -1;
                Play.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/me_pause.png", UriKind.Relative));
                Player.Play();
            }
            else
            {
                Play.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/me_play.png", UriKind.Relative));
                Player.Pause();
            }
        }

        private void Button_Prev(object sender, RoutedEventArgs e)
        {
            _seekTime = _curSec - 0.5;
            TimeSlider.Value = _curSec;
            ticktock(null, null);
        }

        private void Button_Forward(object sender, RoutedEventArgs e)
        {
            _seekTime = _curSec + 0.5;
            TimeSlider.Value = _curSec;
            ticktock(null, null);
        }

        private void TimeSliderLButtonUp(object sender, MouseButtonEventArgs e)
        {
            _seekTime = TimeSlider.Value;
        }

        private void OnMute(object sender, RoutedEventArgs e)
        {
            _mute= !_mute;
            if(_mute)
            {
                Mute.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/me_mute.png", UriKind.Relative));
                Player.Volume = 0;
            }
            else
            {
                Mute.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/editor_tool_timeline_sound.png", UriKind.Relative));
                Player.Volume = 1F;
            }
        }

        private double _lastZoomValue = -1;
        private async void ZoomOut(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider.Value == ZoomSlider.Minimum) return;
            double zoomRate = ZoomSlider.Value - 1;
            if (zoomRate < ZoomSlider.Minimum) zoomRate = ZoomSlider.Minimum;
            _lastZoomValue = ZoomSlider.Value;
            ZoomSlider.Value = zoomRate;
            await SetTimeLinePosition(1);
        }

        private async void ZoomIn(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider.Value == ZoomSlider.Maximum) return;
            double zoomRate = ZoomSlider.Value + 1;
            if (zoomRate > ZoomSlider.Maximum) zoomRate = ZoomSlider.Maximum;
            _lastZoomValue = ZoomSlider.Value;
            ZoomSlider.Value = zoomRate;
            await SetTimeLinePosition(-1);
        }

        private async void SliderValueChange(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double newValue = Math.Round(e.NewValue, 0);
            if(Math.Round(e.OldValue, 0) != newValue)
            {
                if (_lastZoomValue == -1)
                    _lastZoomValue = e.OldValue;
                ZoomSlider.Value = newValue;
                if (TimeLineScroll != null)
                {
                    int dif = (int)(Math.Round(_lastZoomValue - ZoomSlider.Value, 0));
                    _lastZoomValue = -1;
                    await SetTimeLinePosition(dif);
                }
            } else
            {
                if(ZoomSlider.Value != Math.Round(e.OldValue, 0))
                    ZoomSlider.Value = Math.Round(e.OldValue, 0);
            }
        }

        private async void SliderPreviewClick(object sender, MouseButtonEventArgs e)
        {
            //if (((Slider)sender).SelectionEnd != Math.Round(e.NewValue, 0))
            //{
            //  ((Slider)sender).SelectionEnd = Math.Round(e.NewValue, 0);
            var mousePosition = e.GetPosition(ZoomSlider);
            double newValue = Math.Round((mousePosition.X - 5) / (ZoomSlider.Width - 10) * 9, 0);
            if(ZoomSlider.Value != newValue)
            {
                double dif = ZoomSlider.Value - newValue;
                ZoomSlider.Value = newValue;
                await SetTimeLinePosition((int)Math.Round(Math.Round(dif), 0));
            }
            //}
        }

        private async Task SetTimeLinePosition(int s)
        {
            double zoomRate = (double)_timeIntervals[(int)(ZoomSlider.Value + s)] / _timeIntervals[(int)(ZoomSlider.Value)];
            TimeLineScroll.ScrollToHorizontalOffset((TimeLineScroll.HorizontalOffset + _cutlinePosX) * zoomRate - _cutlinePosX);
            InitWidth();
            selectedClip = -1;
            SelectedBorder.Visibility = Visibility.Hidden;

            for (int i = 0; i < VideoClips.Count; i++)
            {
                await VideoClips[i].SetTimeInterval(_timeIntervals[(int)(ZoomSlider.Value)]);
                await Task.Delay(100);
            }
        }

        private void FormKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key.ToString() == "LeftCtrl")
            {
                _altPressed = true;
            }
        }

        private void FormKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key.ToString() == "LeftCtrl")
            {
                _altPressed = false;
            }
        }

        private void ClipMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_altPressed)
            {
                if (e.Delta > 0)
                {
                    ZoomIn(sender, null);
                }
                else
                {
                    ZoomOut(sender, null);
                }
            }
            else
            {
                TimeLineScroll.ScrollToHorizontalOffset(TimeLineScroll.HorizontalOffset - e.Delta / 2);
            }
        }

        private bool cutlineMoved = false;
        private double prevCutlinePos = -1;
        private void CutButtonDown(object sender, MouseButtonEventArgs e)
        {
            // when the mouse is down, get the position within the current control. (so the control top/left doesn't move to the mouse position)
            _positionInBlock = Mouse.GetPosition(CutButton);

            // capture the mouse (so the mouse move events are still triggered (even when the mouse is not above the control)
            cutlineMoved = false;
            prevCutlinePos = _cutlinePosX;
            CutButton.CaptureMouse();
            if (_run)
            {
                _run = false;
            }
        }

        private void CutButtonMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (CutButton.IsMouseCaptured)
            {
                // get the parent container
                var container = VisualTreeHelper.GetParent(CutButton) as UIElement;

                if (container == null)
                    return;

                // get the position within the container
                var mousePosition = e.GetPosition(container);

                // move the user control.
                double x = mousePosition.X - _positionInBlock.X + 12;
                if (Math.Abs(prevCutlinePos - x) > 0.001) cutlineMoved = true;
                if (x < 0) x = 0;
                if (x > TimeLineScroll.ActualWidth) x = TimeLineScroll.ActualWidth;
                SetCutLine(x);
            }
        }

        private void CutButtonUp(object sender, MouseButtonEventArgs e)
        {
            // release this control.
            if (CutButton.IsMouseCaptured)
            {
                CutButton.ReleaseMouseCapture();
                if (!cutlineMoved)
                {
                    OnCutButtonClick(null, null);
                }
            }
        }

        private void OnTimelineDown(object sender, MouseButtonEventArgs e)
        {
            var mousePosition =  e.GetPosition(LineScroll);
            SetCutLine(mousePosition.X - TimeLineScroll.HorizontalOffset);
        }

        private void OnTimeLineScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ticktock(null, null);
            var curX = SelectedBorder.RenderTransform.Value.OffsetX;
            SelectedBorder.RenderTransform = new TranslateTransform(curX - e.HorizontalChange, 62);
        }

        private void OnTimeSliderLButtonDown(object sender, MouseButtonEventArgs e)
        {
            TimeSlider.CaptureMouse();
        }

        private void OnTimeSliderMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (TimeSlider.IsMouseCaptured)
            {

                var mousePosition = e.GetPosition(TimeSlider);
                double sec = (mousePosition.X - 3) * _curDuraiton / TimeSlider.ActualWidth;
                for (int i = 0; i < VideoClips.Count; i++)
                {
                    if (sec <= VideoClips[i]._endPos[0] - VideoClips[i]._startPos[0])
                    {
                        _seekTime = VideoClips[i]._startPos[0] + sec;
                        ticktock(null, null);
                        return;
                    }
                    sec -= VideoClips[i]._endPos[0] - VideoClips[i]._startPos[0];
                }
            }
        }

        private void OnTimeSliderLButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (TimeSlider.IsMouseCaptured)
            {
                TimeSlider.ReleaseMouseCapture();
                ticktock(null, null);
                if (_cutlinePosX < 0 || _cutlinePosX > TimeLineScroll.ActualWidth)
                {
                    _cutlinePosX = TimeLineScroll.ActualWidth / 2;
                    TimeLineScroll.ScrollToHorizontalOffset(_curSec / _curDuraiton * TimeScroll.Width - TimeLineScroll.ActualWidth / 2);
                    ticktock(null, null);
                }
            }
        }

        private int selectedClip = -1;

        private void OnCutButtonClick(object sender, RoutedEventArgs e)
        {
            int i;
            for (i = 0; i < VideoClips.Count; i++)
            {
                System.Windows.Point relativePoint = VideoClips[i].TransformToAncestor(ClipStack).Transform(new System.Windows.Point(0, 0));
                double clipEndPos = relativePoint.X + VideoClips[i].ActualWidth;
                if (Math.Abs(relativePoint.X - _cutlinePosX - TimeLineScroll.HorizontalOffset) < 0.001 || Math.Abs(clipEndPos - _cutlinePosX - TimeLineScroll.HorizontalOffset) < 0.001) return;
                if (_cutlinePosX + TimeLineScroll.HorizontalOffset > relativePoint.X && _cutlinePosX + TimeLineScroll.HorizontalOffset < clipEndPos)
                {
                    if (selectedClip == i)
                    {
                        selectedClip = -1;
                        SelectedBorder.Visibility = Visibility.Hidden;
                    }

                    var _startPos = VideoClips[i].GetCurrentSec(_cutlinePosX + TimeLineScroll.HorizontalOffset - relativePoint.X);
                    var _endPos = VideoClips[i]._endPos[VideoClips[i]._endPos.Count - 1];

                    VideoClips[i].UpdateEndPos(_startPos);
                    var clip = new VideoClipControl(_capture, _firstImage, _startPos, _endPos, _clipWidth, _timeIntervals[(int)(ZoomSlider.Value)], WaveFormData, waveFormSize, maxSpan);
                    clip.isMute = VideoClips[i].isMute;
                    clip.MuteMask.Visibility = clip.isMute ? Visibility.Visible : Visibility.Collapsed;
                    ClipStack.Children.Insert(i + 1, clip);
                    VideoClips.Insert(i + 1, clip);
                    break;
                }
            }
        }

        private void OnClipMouseDown(object sender, MouseButtonEventArgs e)
        {
            int i = 0;
            var pos = e.GetPosition(ClipStack);
            for (i = 0; i < VideoClips.Count; i++)
            {
                var relativePoint = VideoClips[i].TransformToAncestor(ClipStack).Transform(new System.Windows.Point(0, 0));
                double clipEndPos = relativePoint.X + VideoClips[i].ActualWidth;
                if (Math.Abs(relativePoint.X - pos.X) < 0.001 || Math.Abs(clipEndPos - pos.X) < 0.001) return;
                if (pos.X > relativePoint.X && pos.X < clipEndPos)
                {
                    if (selectedClip == i)
                    {
                        SelectedBorder.Visibility = Visibility.Hidden;
                        selectedClip = -1;
                    }
                    else
                    {
                        SelectedBorder.Width = VideoClips[i].ActualWidth;
                        SelectedBorder.Height = 50;
                        SelectedBorder.RenderTransform = new TranslateTransform(relativePoint.X - TimeLineScroll.HorizontalOffset, 62);
                        SelectedBorder.Visibility = Visibility.Visible;
                        selectedClip = i;
                    }
                    return;
                }
            }
        }

        private void OnClipMute(object sender, RoutedEventArgs e)
        {
            if (selectedClip == -1) return;
            VideoClips[selectedClip].isMute = !VideoClips[selectedClip].isMute;
            VideoClips[selectedClip].MuteMask.Visibility = VideoClips[selectedClip].isMute ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnDeleteClip(object sender, RoutedEventArgs e)
        {
            if (selectedClip == -1) return;
            _curDuraiton -= VideoClips[selectedClip]._duration;
            TimeSlider.Maximum = _curDuraiton;
            Text2.Text = GetFormatTime(_curDuraiton);
            VideoClips.RemoveAt(selectedClip);
            ClipStack.Children.RemoveAt(selectedClip);
            selectedClip = -1;
            SelectedBorder.Visibility = Visibility.Hidden;
        }

        private void SetCutLine(double x)
        {
            _cutlinePosX = x;
            double sec = 0;
            int i;
            for (i = 0; i < VideoClips.Count; i++)
            {
                var relativePoint = VideoClips[i].TransformToAncestor(ClipStack).Transform(new System.Windows.Point(0, 0));
                double clipEndPos = relativePoint.X + VideoClips[i].ActualWidth;
                if (_cutlinePosX + TimeLineScroll.HorizontalOffset >= relativePoint.X && _cutlinePosX + TimeLineScroll.HorizontalOffset <= clipEndPos)
                {
                    sec = VideoClips[i].GetCurrentSec(_cutlinePosX - relativePoint.X + TimeLineScroll.HorizontalOffset);
                    break;
                }
            }
            if (i == VideoClips.Count)
            {
                return;
                //sec = _duration;
            }
            //CutButton.RenderTransform = new TranslateTransform(x, 0);
            //CutLine.RenderTransform = new TranslateTransform(x, 0);
            //CutLabel.RenderTransform = new TranslateTransform(x, 0);
            //CutLabel.Content = GetFormatTime(sec);
            _seekTime = sec;
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            ExportDialog exportDialog = new ExportDialog();
            exportDialog._capture = _capture;
            exportDialog._videoPath = _videoFile;
            exportDialog.VideoClips = VideoClips;
            exportDialog.waveStream = WaveStream;
            exportDialog._duration = _curDuraiton;
            exportDialog.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            exportDialog.VerticalAlignment= System.Windows.VerticalAlignment.Top;
            exportDialog.Margin = new Thickness(0, 10, 35, 0);
            exportDialog.ResolutionLabel.Content = _capture.FrameWidth.ToString() + '*' + _capture.FrameHeight.ToString();
            exportDialog.FrameRateLabel.Content = ((int)(_capture.Fps)).ToString() + "fps";
            exportDialog.DurationLabel.Content = GetFormatTime(_curDuraiton);
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = new FileInfo(_videoFile).Length;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            exportDialog.SizeLabel.Content = String.Format("{0:0.##} {1}", len, sizes[order]);
            BgGrid.Children.Add(exportDialog);
        }

        private void OnFullScreen(object sender, RoutedEventArgs e)
        {
            _fullscreen = !_fullscreen;
            if (_fullscreen)
            {
                MainGrid.RowDefinitions.ToArray()[0].Height = new GridLength(0);
                MainGrid.RowDefinitions.ToArray()[2].Height = new GridLength(0);
                BtnExport.Visibility = Visibility.Hidden;
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                MainGrid.RowDefinitions.ToArray()[0].Height = new GridLength(35);
                MainGrid.RowDefinitions.ToArray()[2].Height = new GridLength(250);
                BtnExport.Visibility = Visibility.Visible;
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Normal;
            }
        }

        private void window_close(object sender, RoutedEventArgs e)
        {
            _decoder.stop();
            Close();
        }

        private void window_move(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void windows_maximize(object sender, RoutedEventArgs e)
        {
            _maximize = !_maximize;
            if (_maximize)
            {
                this.WindowState = WindowState.Maximized;
            }
            else
            {
                this.WindowState = WindowState.Normal;
            }
        }

        private void windows_minimize(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}
