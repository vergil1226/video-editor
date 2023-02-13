using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Video_Editor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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
            Close();
        }

        private void window_move(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void ImportVideo_MouseEnter(object sender, MouseEventArgs e)
        {
            ImportVideo.Background = Brushes.Transparent;
        }

        private void ImportVideo_Click(object sender, RoutedEventArgs e)
        {

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Video Files (*.mp4, *.avi, *.wmv)|*.mp4;*.avi;*.wmv";
            openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            if (openFileDialog.ShowDialog() == true)
            {
                //m_toolbar.Visibility = Visibility.Visible;
                //mediaInputWindow.Visibility = Visibility.Visible;

                BgGrid.Children.Add(new ImportingModalDialog());
                DispatcherTimer time = new DispatcherTimer();
                time.Interval = TimeSpan.FromSeconds(3);
                time.Start();
                time.Tick += delegate
                {
                    BgGrid.Children.Clear();

                    // Import the video file
                    string videoFile = openFileDialog.FileName;
                    mediaEdit.Source = new Uri(videoFile);
                    m_toolbar.Visibility = Visibility.Hidden;
                    mediaEditWindow.Visibility = Visibility.Visible;
                    mediaInputWindow.Visibility = Visibility.Hidden;

                    mediaInputTimeline.Visibility = Visibility.Hidden;
                    mediaEditTimeline.Visibility = Visibility.Visible;
                };
            }
        }
    }
}
