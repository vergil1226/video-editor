using NAudio.Gui;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfTest
{
    /// <summary>
    /// Interaction logic for AudioClipControl.xaml
    /// </summary>


    public partial class AudioClipControl : UserControl
    {
        public List<float> WaveFormData { get; private set; }
        private int waveFormSize = 0;
        private double maxSpan = 0.0;
        private double startPos = 0.0;
        private double endPos = 0.0;
        private double duration = 0.0;

        public AudioClipControl(List<float> _WaveFormData, int _waveFormSize, double _maxSpan, double _width, double _startPos, double _endPos, double _duration)
        {
            InitializeComponent();

            WaveFormData = _WaveFormData;
            waveFormSize = _waveFormSize;
            maxSpan = _maxSpan;
            Width = _width;
            startPos = _startPos;
            endPos = _endPos;
            duration = _duration;
            DataContext = this;
            Render();
        }

        private DrawingGroup drawingGroup { get; } = new DrawingGroup();
        protected override void OnRender(DrawingContext drawingContext)
        {
            Render();
            drawingContext.DrawDrawing(drawingGroup);
            base.OnRender(drawingContext);
        }

        private bool taskQueueRunning = false;
        private int renderCounter = 0;
        private async void Render()
        {
            renderCounter++;
            if (!taskQueueRunning)
            {
                taskQueueRunning = true;
                var dc = drawingGroup.Open();
                while (renderCounter > 0)
                {
                    RenderTask(dc);
                    renderCounter--;
                }
                dc.Close();
                taskQueueRunning = false;
            }
            await Task.Yield();
        }

        public void RenderTask(DrawingContext drawingContext)
        {
            var sampleSize = (endPos - startPos) / duration * waveFormSize / (Width + 1);

            var multiplier = (50 * 0.9) / maxSpan;

            var drawCenter = ((50 - ((WaveFormData.Average() * 2) * multiplier)) / 2);

            var visibleSample = WaveFormData.GetRange((int)(startPos / duration * waveFormSize), (int)((endPos - startPos) / duration * waveFormSize));
            var a = new System.Windows.Point(0, drawCenter);
            var b = a;

            var pen = new Pen((SolidColorBrush)Utils.BrushConverter.ConvertFromString(Settings.AudioWaveColor), 1);

            for (int i = 0; i < Width && (sampleSize * i) + sampleSize < visibleSample.Count(); i++)
            {
                var sample = visibleSample.GetRange((int)(sampleSize * i), (int)(sampleSize)).ToArray();
                if (sample.Length > 0)
                {
                    a = new System.Windows.Point(b.X, drawCenter + (-sample.Max() * multiplier));
                    b = new System.Windows.Point(i + 1, drawCenter + (-sample.Min() * multiplier));
                }
                else
                {
                    a = new System.Windows.Point(b.X, drawCenter);
                    b = new System.Windows.Point(i + 1, drawCenter);
                }
                drawingContext.DrawLine(pen, a, b);
            }
        }

        public void UpdateWidth(double zoomRate)
        {
            Width = Width * zoomRate;
            //Render();
        }

        public void UpdateEndPos(double _endPos, double _width)
        {
            endPos = _endPos;
            Width = _width;
        }
    }
}
