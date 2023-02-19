using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Video_Editor;
using static System.Net.Mime.MediaTypeNames;

namespace WpfTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private VideoCapture _capture;
        TimeSpan _position;
        DispatcherTimer _timer = new DispatcherTimer();
        BitmapImage _firstImage = new BitmapImage();
        private bool _run = false, _fullscreen = false, _altPressed = false, _mute = false, _maximize = false, _mediaLoaded = false;
        private int _startPos, _endPos, _lineLength = 1;
        private double _clipWidth, _cutlinePosX;
        private System.Windows.Point _positionInBlock;

        public ObservableCollection<BitmapImage> Thumbnails { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            mediaInputWindowInit.Visibility = Visibility.Visible;

            m_toolbar.Visibility = Visibility.Hidden;
            mediaInputWindow.Visibility = Visibility.Hidden;
            mediaEditWindow.Visibility = Visibility.Hidden;

            mediaInputTimeline.Visibility = Visibility.Hidden;
            mediaEditTimeline.Visibility = Visibility.Hidden;

            Thumbnails = new ObservableCollection<BitmapImage>();
            DataContext = this;

            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Tick += new EventHandler(ticktock);
            _timer.Start();
        }

        void ticktock(object sender, EventArgs e)
        {
            if (!_mediaLoaded) return;
            double sec = media.Position.TotalSeconds;
            TimeSlider.Value = sec;
            if (CutButton.IsMouseCaptured) return;
            double pos = sec * _capture.Fps;
            _cutlinePosX = ClipScroll.ActualWidth * (pos - _startPos) / (_endPos - _startPos);
            double x = _cutlinePosX < 0 ? 0 : _cutlinePosX;
            x = x > ClipScroll.ActualWidth ? ClipScroll.ActualWidth : x;
            TranslateTransform _transform = new TranslateTransform(x, 0);
            CutButton.RenderTransform = _transform;
            CutLine.RenderTransform = _transform;
            CutLabel.RenderTransform = _transform;
            CutLabel.Content = GetFormatTime((int)pos);
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
                time.Interval = TimeSpan.FromSeconds(2);
                time.Start();
                time.Tick += async delegate
                {
                    // Import the video file
                    string _videoFile = openFileDialog.FileName;
                    _capture = new VideoCapture(_videoFile);
                    media.Source = new Uri(_videoFile);
                    media.Play();
                    media.Pause();
                    _mediaLoaded = true;

                    _clipWidth = 50 * _capture.FrameWidth / _capture.FrameHeight;
                    _startPos = 0;
                    _endPos = (int)(_capture.FrameCount * ClipScroll.ActualWidth / _clipWidth);
                    TimeEnd.Content = GetFormatTime(_endPos);
                    await GetFrame(0, _firstImage);
                    InitClip();

                    BgGrid.Children.Clear();
                    time.Stop();
                };
            }
        }

        private void InitClip()
        {
            Thumbnails.Clear();
            for (int i = 0; i < _lineLength; i++)
            {
                Thumbnails.Add(_firstImage);
            }
            SyncThumbnails();
        }

        private void SyncThumbnails()
        {
            for (int i = 0; i < _lineLength; i++)
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
            int cnt = _lineLength;
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

        private string GetFormatTime(int t)
        {
            int elapsedTime = (int)(t / _capture.Fps);
            return "00:" + (elapsedTime / 3600).ToString("D2") + " " + (elapsedTime / 60).ToString("D2") + ":" + (elapsedTime % 60).ToString("D2");
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
            _run = false;
        }

        private void Button_Play(object sender, RoutedEventArgs e)
        {
            _run = !_run;
            if (_run) media.Play();
            else media.Pause();
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
            _lineLength /= 2; _endPos *= 2;
            SetTimeLinePosition();
        }

        private void ZoomIn(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider.Value == ZoomSlider.Maximum) return;
            double zoomRate = ZoomSlider.Value + 1;
            if (zoomRate > ZoomSlider.Maximum) zoomRate = ZoomSlider.Maximum;
            ZoomSlider.Value = zoomRate;
            _lineLength *= 2; _endPos /= 2;
            SetTimeLinePosition();
        }

        private void SetTimeLinePosition()
        {
            TimeStart.Content = GetFormatTime(_startPos);
            TimeEnd.Content = GetFormatTime(_endPos);
            InitClip();
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
                ClipScroll.ScrollToHorizontalOffset(ClipScroll.HorizontalOffset - e.Delta);
            }
        }

        private void ClipScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!_mediaLoaded) return;
            double t = e.HorizontalChange * (_endPos - _startPos) / ClipScroll.ActualWidth;
            _startPos += (int)t;
            _endPos += (int)t;
            TimeStart.Content = GetFormatTime(_startPos);
            TimeEnd.Content = GetFormatTime(_endPos);
        }

        private void CutButtonDown(object sender, MouseButtonEventArgs e)
        {
            // when the mouse is down, get the position within the current control. (so the control top/left doesn't move to the mouse position)
            _positionInBlock = Mouse.GetPosition(CutButton);

            // capture the mouse (so the mouse move events are still triggered (even when the mouse is not above the control)
            CutButton.CaptureMouse();
            _run = false; media.Pause();
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
                double x = mousePosition.X - _positionInBlock.X + 10;
                if (x < 0) x = 0;
                if (x > ClipScroll.ActualWidth) x = ClipScroll.ActualWidth;
                int pos = (int)(_startPos + (_endPos - _startPos) * x / ClipScroll.ActualWidth);
                //if (pos > _capture.FrameCount) x = ClipScroll.ActualWidth;
                _cutlinePosX = x;
                CutButton.RenderTransform = new TranslateTransform(x, 0);
                CutLine.RenderTransform = new TranslateTransform(x, 0);
                CutLabel.RenderTransform = new TranslateTransform(x, 0);
                CutLabel.Content = GetFormatTime(pos);
                int mili = (int)(1000 * pos / _capture.Fps);
                media.Position = new TimeSpan(0, 0, 0, mili / 1000, mili % 1000);
            }
        }

        private void CutButtonUp(object sender, MouseButtonEventArgs e)
        {
            // release this control.
            CutButton.ReleaseMouseCapture();
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            ExportDialog exportDialog = new ExportDialog();
            exportDialog._capture = _capture;
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
