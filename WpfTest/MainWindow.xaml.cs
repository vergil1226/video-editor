using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using SharpVectors.Dom.Svg;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Video_Editor;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using static WpfTest.Utils;

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
        TimeSpan _position;
        DispatcherTimer _timer = new DispatcherTimer();
        BitmapImage _firstImage = new BitmapImage();
        private bool _run = false, _fullscreen = false, _altPressed = false, _mute = false, _maximize = false, _mediaLoaded = false;
        private double _clipWidth, _cutlinePosX, _duration, _curDuraiton;
        private System.Windows.Point _positionInBlock;
        private int[] _timeIntervals = new int[10] { 7200, 3600, 1200, 600, 300, 120, 60, 30, 20, 10 };
        private string _videoFile = null;

        public ObservableCollection<VideoClipControl> VideoClips { get; set; }
        public ObservableCollection<AudioClipControl> AudioClips { get; set; }

        public ObservableCollection<Line> Lines { get; private set; }

        public ObservableCollection<string> Times { get; private set; }

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
            AudioClips= new ObservableCollection<AudioClipControl>();
            Lines = new ObservableCollection<Line>();
            Times = new ObservableCollection<string>();
            DataContext = this;

            _timer.Interval = TimeSpan.FromMilliseconds(50);
            _timer.Tick += new EventHandler(ticktock);
            _timer.Start();

            for (int i = 0; i < 2000; i++)
            {
                Lines.Add(new Line { From = new System.Windows.Point(20 * i, 5 - (i % 5 == 0 ? 5 : 0)), To = new System.Windows.Point(20 * i, 10) });
            };
        }

        void ticktock(object sender, EventArgs e)
        {
            if (!_mediaLoaded) return;
            double sec = media.Position.TotalSeconds;

            if (ClipStack.Children.Count > 0 && ClipStack.ActualWidth > 0)
            {
                double value = _curDuraiton * (_cutlinePosX + TimeLineScroll.HorizontalOffset) / ClipStack.ActualWidth;
                TimeSlider.Value = value;
                Text1.Text = GetFormatTime((int)value);
                CutLabel.Content = GetFormatTime((int)value);
            }

            if (CutButton.IsMouseCaptured) return;

            int i;
            if (_run)
            {
                for (i = 0; i < VideoClips.Count - 1; i++)
                {
                    if (sec > VideoClips[i]._endPos[VideoClips[i]._endPos.Count - 1] && sec < VideoClips[i + 1]._startPos[0])
                    {
                        sec = VideoClips[i + 1]._startPos[0];
                        media.Position = new TimeSpan(0, 0, 0, (int)sec, (int)(sec * 1000) % 1000);
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
                break;
            }

            /*if (_cutlinePosX < 0 && _run)
            {
                _cutlinePosX += TimeLineScroll.ActualWidth;
                TimeLineScroll.ScrollToHorizontalOffset(TimeLineScroll.HorizontalOffset - TimeLineScroll.ActualWidth);
            }
            else if (_cutlinePosX > TimeLineScroll.ActualWidth && _run)
            {
                _cutlinePosX-= TimeLineScroll.ActualWidth;
                TimeLineScroll.ScrollToHorizontalOffset(TimeLineScroll.HorizontalOffset + TimeLineScroll.ActualWidth);
            }*/
            TranslateTransform _transform = new TranslateTransform(_cutlinePosX, 0);
            CutButton.RenderTransform = _transform;
            CutLine.RenderTransform = _transform;
            CutLabel.RenderTransform = _transform;
        }

        private void ImportVideo_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "Video Files (*.mp4, *.avi, *.wmv)|*.mp4;*.avi;*.wmv";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            if (openFileDialog.ShowDialog() == true)
            {
                m_toolbar.Visibility = Visibility.Hidden;
                mediaEditWindow.Visibility = Visibility.Visible;
                mediaInputWindow.Visibility = Visibility.Hidden;

                mediaInputTimeline.Visibility = Visibility.Hidden;
                mediaEditTimeline.Visibility = Visibility.Visible;

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
                    media.Source = new Uri(_videoFile);
                    await Task.Delay(100);
                    media.Play();
                    media.Pause();
                    _mediaLoaded = true;

                    _clipWidth = 50 * _capture.FrameWidth / _capture.FrameHeight;
                    _duration = _capture.FrameCount / _capture.Fps;
                    _curDuraiton = _duration;
                    Text2.Text = GetFormatTime((int)_duration);
                    GetFrame(0, _firstImage);
                    VideoClipControl clip = new VideoClipControl(_capture, _firstImage, 0, _duration, _clipWidth, _timeIntervals[(int)ZoomSlider.Value]);
                    VideoClips.Add(clip);
                    ClipStack.Children.Add(clip);
                    InitWidth();
                    await ConvertLoad();
                    BgGrid.Children.Clear();
                };
            }
        }

        private void InitWidth()
        {
            double _width = 200.0 / _timeIntervals[(int)ZoomSlider.Value] * _curDuraiton;
            LineControl.Width = ((int)(_width / TimeLineScroll.ActualWidth) + 1) * TimeLineScroll.ActualWidth;
            ClipScroll.Width = LineScroll.Width;
            TimeScroll.Width = LineControl.Width;
            //WaveStack.Width = _width;
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

        private async Task ConvertLoad()
        {
            string wavLocation = System.IO.Path.GetDirectoryName(Environment.ProcessPath) + "\\temp";
            while (File.Exists(wavLocation + ".wav"))
            {
                File.Delete(wavLocation + ".wav");
                /*if (Regex.IsMatch(wavLocation, @"_\d+$"))
                    wavLocation = Regex.Replace(wavLocation, @"_\d+$", "_" + (int.Parse(wavLocation.Split('_').Last()) + 1).ToString());
                else
                    wavLocation = wavLocation + "_1";*/
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
        private double downX = 0.0;
        private double downScroll = 0.0;
        private bool modifierCtrlPressed = false;
        private bool modifierShiftPressed = false;
        private bool drawWave = false;

        public async Task<bool> SetWaveStream(string fileName)
        {
            await Task.Yield();

            WaveStream = null;
            WaveFormData = null;
            waveSize = 0L;
            waveFormSize = 0;
            waveDuration = TimeSpan.Zero;
            //UpdateVisuals();

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

                //this.waveSize = waveStream.Length;
                waveSize = waveStream.Length;

                //ScrollOffset = 0.0;
                MaxZoom = Math.Max(wave.TotalTime.TotalSeconds / 4, MinZoom);
                //Zoom = MinZoom;
                //SelectionStart = 0;
                //SelectionEnd = 0;

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
                //Player.Init(waveStream);
                waveStream.Position = 0L;
                WaveStream = waveStream;
                WaveFormData = waveFormData.ToList();
                waveFormSize = waveFormData.Length;
                //PlayRangeEnd = WaveStream.TotalTime;
                var clip = new AudioClipControl(WaveFormData, waveFormSize, maxSpan, ClipStack.ActualWidth, 0, _duration, _duration);
                WaveStack.Children.Add(clip);
                AudioClips.Add(clip);
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

            drawWave = true;
            //Render();

            var success = (WaveStream != null);
            //btnPlayPause.IsEnabled = success;

            return success;
        }

        private string GetFormatTime(int t)
        {
            return "00:" + (t / 3600).ToString("D2") + " " + (t / 60).ToString("D2") + ":" + (t % 60).ToString("D2");
        }

        private void OnMediaOpend(object sender, RoutedEventArgs e)
        {
            _position = media.NaturalDuration.TimeSpan;
            TimeSlider.Minimum = 0;
            TimeSlider.Maximum = _position.TotalSeconds;
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e)
        {
            mediaElement.Position = new TimeSpan(0, 0, 0, 1, 0);
            _run = false; media.Stop();
            Play.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/me_play.png", UriKind.Relative));
            TimeLineScroll.ScrollToHome();
        }

        private void Button_Play(object sender, RoutedEventArgs e)
        {
            _run = !_run;
            if (_run)
            {
                media.Play();
                Play.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/me_pause.png", UriKind.Relative));
            }
            else
            {
                media.Pause();
                Play.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/me_play.png", UriKind.Relative));
            }
        }

        private void Button_Prev(object sender, RoutedEventArgs e)
        {
            int pos = Convert.ToInt32(TimeSlider.Value - 1);
            media.Position = new TimeSpan(0, 0, 0, pos, 0);
            TimeSlider.Value = pos;
        }

        private void Button_Forward(object sender, RoutedEventArgs e)
        {
            int pos = Convert.ToInt32(TimeSlider.Value + 1);
            media.Position = new TimeSpan(0, 0, 0, pos, 0);
            TimeSlider.Value = pos;
        }

        private void TimeSliderLButtonUp(object sender, MouseButtonEventArgs e)
        {
            int pos = Convert.ToInt32(TimeSlider.Value);
            media.Position = new TimeSpan(0, 0, 0, pos, 0);
        }

        private void OnMute(object sender, RoutedEventArgs e)
        {
            _mute= !_mute;
            media.IsMuted = _mute;
            if(_mute)
            {
                Mute.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/me_mute.png", UriKind.Relative));
            }
            else
            {
                Mute.Source = new BitmapImage(new Uri(@"/WpfTest;component/Resources/editor_tool_timeline_sound.png", UriKind.Relative));
            }
        }

        private void ZoomOut(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider.Value == ZoomSlider.Minimum) return;
            double zoomRate = ZoomSlider.Value - 1;
            if (zoomRate < ZoomSlider.Minimum) zoomRate = ZoomSlider.Minimum;
            ZoomSlider.Value = zoomRate;
            SetTimeLinePosition(1);
        }

        private void ZoomIn(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider.Value == ZoomSlider.Maximum) return;
            double zoomRate = ZoomSlider.Value + 1;
            if (zoomRate > ZoomSlider.Maximum) zoomRate = ZoomSlider.Maximum;
            ZoomSlider.Value = zoomRate;
            SetTimeLinePosition(-1);
        }

        private void SetTimeLinePosition(int s)
        {
            double zoomRate = (double)_timeIntervals[(int)(ZoomSlider.Value + s)] / _timeIntervals[(int)(ZoomSlider.Value)];
            TimeLineScroll.ScrollToHorizontalOffset((TimeLineScroll.HorizontalOffset + _cutlinePosX) * zoomRate - _cutlinePosX);
            InitWidth();

            for (int i = 0; i < VideoClips.Count; i++)
            {
                AudioClips[i].UpdateWidth(zoomRate);
                VideoClips[i].SetTimeInterval(_timeIntervals[(int)(ZoomSlider.Value)]);
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

        private void CutButtonDown(object sender, MouseButtonEventArgs e)
        {
            // when the mouse is down, get the position within the current control. (so the control top/left doesn't move to the mouse position)
            _positionInBlock = Mouse.GetPosition(CutButton);

            // capture the mouse (so the mouse move events are still triggered (even when the mouse is not above the control)
            CutButton.CaptureMouse();
            if (_run)
            {
                _run = false; media.Pause();
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
                if (x < 0) x = 0;
                if (x > TimeLineScroll.ActualWidth) x = TimeLineScroll.ActualWidth;
                SetCutLine(x);
            }
        }

        private void CutButtonUp(object sender, MouseButtonEventArgs e)
        {
            // release this control.
            CutButton.ReleaseMouseCapture();
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

                    var clip = new VideoClipControl(_capture, _firstImage, _startPos, _endPos, _clipWidth, _timeIntervals[(int)(ZoomSlider.Value)]);
                    ClipStack.Children.Insert(i + 1, clip);
                    VideoClips.Insert(i + 1, clip);
                    VideoClips[i].UpdateEndPos(_startPos);

                    var audioClip = new AudioClipControl(WaveFormData, waveFormSize, maxSpan, clip.ThumbnailControl.Width, _startPos, _endPos, _duration);
                    WaveStack.Children.Insert(i + 1, audioClip);
                    AudioClips.Insert(i + 1, audioClip);
                    AudioClips[i].UpdateEndPos(_startPos, VideoClips[i].ThumbnailControl.Width);
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

        private void OnDeleteClip(object sender, RoutedEventArgs e)
        {
            if (selectedClip == -1) return;
            _curDuraiton -= VideoClips[selectedClip]._duration;
            TimeSlider.Maximum = _curDuraiton;
            Text2.Text = GetFormatTime((int)_curDuraiton);
            VideoClips.RemoveAt(selectedClip);
            ClipStack.Children.RemoveAt(selectedClip);
            AudioClips.RemoveAt(selectedClip);
            WaveStack.Children.RemoveAt(selectedClip);
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
            CutButton.RenderTransform = new TranslateTransform(x, 0);
            CutLine.RenderTransform = new TranslateTransform(x, 0);
            CutLabel.RenderTransform = new TranslateTransform(x, 0);
            CutLabel.Content = GetFormatTime((int)sec);
            int mili = (int)(1000 * sec);
            media.Position = new TimeSpan(0, 0, 0, mili / 1000, mili % 1000);
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            ExportDialog exportDialog = new ExportDialog();
            exportDialog._capture = _capture;
            exportDialog.VideoClips = VideoClips;
            exportDialog.waveStream = WaveStream;
            exportDialog.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            exportDialog.VerticalAlignment= System.Windows.VerticalAlignment.Top;
            exportDialog.Margin = new Thickness(0, 10, 35, 0);
            exportDialog.ResolutionLabel.Content = _capture.FrameWidth.ToString() + '*' + _capture.FrameHeight.ToString();
            exportDialog.FrameRateLabel.Content = ((int)(_capture.Fps)).ToString() + "fps";
            exportDialog.DurationLabel.Content = GetFormatTime(_capture.FrameCount);
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
