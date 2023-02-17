using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace WpfTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private VideoCapture _capture;
        private Thread videoThread;
        private bool _run = false, _threadClose = false, _fullscreen = false, _altPressed = false;
        private int speedCount = 1000, clipInterval = 10;
        private double clipWidth;
        private string winName = "fullscreen";

        public MainWindow()
        {
            InitializeComponent();

            mediaInputWindowInit.Visibility = Visibility.Visible;

            m_toolbar.Visibility = Visibility.Hidden;
            mediaInputWindow.Visibility = Visibility.Hidden;
            mediaEditWindow.Visibility = Visibility.Hidden;

            mediaInputTimeline.Visibility = Visibility.Hidden;
            mediaEditTimeline.Visibility = Visibility.Hidden;
        }

        private void window_close(object sender, RoutedEventArgs e)
        {
            _threadClose = true;
            Close();
        }

        private void window_move(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void ImportVideo_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ImportVideo.Background = System.Windows.Media.Brushes.Transparent;
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

                string _videoFile = openFileDialog.FileName;
                _capture = new VideoCapture(_videoFile);

                TimeLabel2.Content = GetFormatTime(_capture.FrameCount);
                TimeSlider.Maximum = _capture.FrameCount;

                clipWidth = 50 * _capture.FrameWidth / _capture.FrameHeight;
                InitClip();

                videoThread = new Thread(PlayVideo);
                videoThread.Start();
            }
        }
        private void InitClip()
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                if (ClipStack == null) return;
                ClipStack.Children.Clear();
                TimeStack.Children.Clear();
                int pos, prevPos = _capture.PosFrames;
                for (pos = 0; pos < _capture.FrameCount; pos += clipInterval)
                {
                    _capture.PosFrames = pos;
                    Mat _image = new Mat();
                    _capture.Read(_image);
                    var bmpClip = BitmapConverter.ToBitmap(_image);
                    System.Windows.Controls.Image img = new System.Windows.Controls.Image();
                    img.Source = BitmapToImageSource(bmpClip);
                    img.Height = 50;
                    ClipStack.Children.Add(img);
                }
                _capture.PosFrames = prevPos;

                for (pos = 0; pos <= _capture.FrameCount; pos += (int)(_capture.Fps))
                {
                    System.Windows.Controls.Label _label = new System.Windows.Controls.Label();
                    _label.Content = GetFormatTime(pos);
                    _label.Width = clipWidth / clipInterval * _capture.Fps;
                    _label.Foreground = System.Windows.Media.Brushes.White;
                    TimeStack.Children.Add(_label);
                }
            }));
        }

        private void PlayVideo()
        {
            int plus = 1;
            while (true)
            {
                if (_threadClose) return;
                if (!_run) continue;

                var watch = System.Diagnostics.Stopwatch.StartNew();
                Mat _image = new Mat();
                _capture.Read(_image);
                if (_image.Empty()) continue;
                if (_fullscreen)
                {
                    Cv2.ImShow(winName, _image);
                    Cv2.WaitKey(1);
                }

                var bmpVideo = BitmapConverter.ToBitmap(_image);
                this.Dispatcher.Invoke((Action)(() =>
                {
                    TimeSlider.Value += plus;
                    if (TimeSlider.Value >= _capture.FrameCount)
                    {
                        _run = false;
                        TimeSlider.Value = 0;
                    }
                    VideoViewer.Source = BitmapToImageSource(bmpVideo);
                    _capture.PosFrames = (int)(TimeSlider.Value);
                    TimeLabel1.Content = GetFormatTime(_capture.PosFrames);
                }));

                watch.Stop();
                int elapsed = (int)(watch.ElapsedMilliseconds);
                plus = (int)(elapsed * _capture.Fps / speedCount);
            }
        }

        private string GetFormatTime(int t)
        {
            int elapsedTime = (int)(t / _capture.Fps);
            return "00:" + (elapsedTime / 3600).ToString("D2") + " " + (elapsedTime / 60).ToString("D2") + ":" + (elapsedTime % 60).ToString("D2");
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Button_Play(object sender, RoutedEventArgs e)
        {
            _run = !_run;
        }

        private void Button_Prev(object sender, RoutedEventArgs e)
        {
            speedCount += 100;
        }

        private void Button_Forward(object sender, RoutedEventArgs e)
        {
            if (speedCount == 100) return;
            speedCount -= 100;
        }

        private void ZoomOut(object sender, RoutedEventArgs e)
        {
            clipInterval += 10;
            if (clipInterval > 100) clipInterval = 100;
            ZoomSlider.Value = 110 - clipInterval;
            InitClip();
        }

        private void ZoomIn(object sender, RoutedEventArgs e)
        {
            clipInterval -= 10;
            if (clipInterval < 10)  clipInterval = 10;
            ZoomSlider.Value = 110 - clipInterval;
            InitClip();
        }

        private void ZoomSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            clipInterval = 110 - (int)e.NewValue;
            InitClip();
        }

        private void FormKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.SystemKey.ToString() == "LeftAlt")
            {
                _altPressed = true;
            }
        }

        private void FormKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.SystemKey.ToString() == "LeftAlt")
            {
                _altPressed = false;
            }
        }

        private void ClipMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_altPressed)
            {
                int _zoomRate = 3;
                if (e.Delta > 0)
                {
                    if (clipInterval + _zoomRate < _capture.Fps)
                        clipInterval += _zoomRate;
                }
                else
                {
                    if (clipInterval - _zoomRate > 0)
                        clipInterval -= _zoomRate;
                }
                Thread clipThread = new Thread(InitClip);
                clipThread.Start();
            }
            else
            {
                ClipScroll.ScrollToHorizontalOffset(ClipScroll.HorizontalOffset - e.Delta);
                TimeScroll.ScrollToHorizontalOffset(TimeScroll.HorizontalOffset - e.Delta);
            }
        }

        private void Button_Fullscreen(object sender, RoutedEventArgs e)
        {
//             Cv2.NamedWindow(winName, WindowFlags.Normal);
//             Cv2.SetWindowProperty(winName, WindowPropertyFlags.Fullscreen, 1.0);
//             _fullscreen = true;
            
        }

        private void SliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (e.NewValue - e.OldValue == speedCount) return;
        }
    }
}
