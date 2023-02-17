using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
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
        private Thread videoThread;
        private bool _run = false, _threadClose = false, _fullscreen = false, _altPressed = false;
        private int speedCount = 1000, startPos, endPos;
        private double clipWidth, cutlinePosX;
        private string winName = "fullscreen";
        private System.Windows.Point _positionInBlock;

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

                BgGrid.Children.Add(new ImportingModalDialog());
                DispatcherTimer time = new DispatcherTimer();
                time.Interval = TimeSpan.FromSeconds(1);
                time.Start();
                time.Tick += delegate
                {

                    // Import the video file
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
                    startPos = 0;
                    endPos = _capture.FrameCount;
                    InitClip();

                    BgGrid.Children.Clear();
                    time.Stop();

                    videoThread = new Thread(PlayVideo);
                    videoThread.Start();

                    _run = true;
                    Thread.Sleep(10);
                    _run = false;
                };
            }
        }

        private void InitClip()
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                ClipStack.Children.Clear();

                int cnt = (int)(ClipStack.ActualWidth / clipWidth);
                double clipInterval = (double)(endPos - startPos) / cnt;
                int prevPos = _capture.PosFrames;
                for ( int i = 0; i < cnt; i++ )
                {
                    _capture.PosFrames = (int)(i * clipInterval + startPos);
                    Mat _image = new Mat();
                    _capture.Read(_image);
                    var bmpClip = BitmapConverter.ToBitmap(_image);
                    System.Windows.Controls.Image img = new System.Windows.Controls.Image();
                    img.Source = BitmapToImageSource(bmpClip);
                    img.Height = 50;
                    img.Width = ClipStack.ActualWidth / cnt;
                    ClipStack.Children.Add(img);
                }
                _capture.PosFrames = prevPos;

                TimeStart.Content = GetFormatTime(startPos);
                TimeEnd.Content = GetFormatTime(endPos);
            }));
        }

        private void ShowClip()
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                Mat _image = new Mat();
                _capture.Read(_image);
                if (_image.Empty()) return;
                VideoViewer.Source = BitmapToImageSource(BitmapConverter.ToBitmap(_image));
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

        private void SetTimeLinePosition(double zoomRate, double posX)
        {
            int frameCount = endPos - startPos;
            startPos += (int)(frameCount * zoomRate * posX / ClipStack.ActualWidth);
            endPos -= (int)(frameCount * zoomRate * (ClipStack.ActualWidth - posX) / ClipStack.ActualWidth);
            if (startPos < 0) startPos = 0;
            if (endPos > _capture.FrameCount) endPos = _capture.FrameCount;
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
            if (ZoomSlider.Value == ZoomSlider.Minimum) return;
            double zoomRate = ZoomSlider.Value - 1;
            if (zoomRate < ZoomSlider.Minimum) zoomRate = ZoomSlider.Minimum;
            ZoomSlider.Value = zoomRate;
        }

        private void ZoomIn(object sender, RoutedEventArgs e)
        {
            if (ZoomSlider.Value == ZoomSlider.Maximum) return;
            double zoomRate = ZoomSlider.Value + 1;
            if (zoomRate > ZoomSlider.Maximum) zoomRate = ZoomSlider.Maximum;
            ZoomSlider.Value = zoomRate;
        }

        private void ZoomSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ClipStack == null) return;
            SetTimeLinePosition((e.NewValue - e.OldValue) / e.OldValue, cutlinePosX);
            (new Thread(InitClip)).Start();
        }

        private void FormKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.SystemKey.ToString() == "LeftAlt")
            {
                _altPressed = true;
            }
        }

        private void CutButtonDown(object sender, MouseButtonEventArgs e)
        {
            // when the mouse is down, get the position within the current control. (so the control top/left doesn't move to the mouse position)
            _positionInBlock = Mouse.GetPosition(CutButton);

            // capture the mouse (so the mouse move events are still triggered (even when the mouse is not above the control)
            CutButton.CaptureMouse();
            _run = false;
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
                if (x > ClipStack.ActualWidth) x = ClipStack.ActualWidth;
                cutlinePosX = x;
                CutButton.RenderTransform = new TranslateTransform(x, 0);
                CutLine.RenderTransform = new TranslateTransform(x, 0);
                int pos = (int)(startPos + (endPos - startPos) * x / ClipStack.ActualWidth);
                CutLabel.RenderTransform = new TranslateTransform(x, 0);
                CutLabel.Content = GetFormatTime(pos);
                _capture.PosFrames = pos;
                (new Thread(ShowClip)).Start();
            }
        }

        private void CutButtonUp(object sender, MouseButtonEventArgs e)
        {
            // release this control.
            CutButton.ReleaseMouseCapture();
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
                if (e.Delta > 0 && startPos == 0 || e.Delta < 0 && endPos == _capture.FrameCount) return;
                int s = e.Delta > 0 ? -1 : 1;
                startPos += s * (endPos - startPos) / 10;
                endPos += s * (endPos - startPos) / 10;
                if (startPos < 0)
                {
                    endPos -= startPos;
                    startPos = 0;
                }
                if (endPos > _capture.FrameCount)
                {
                    startPos -= endPos - _capture.FrameCount;
                    endPos = _capture.FrameCount;
                }
                (new Thread(InitClip)).Start();
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
