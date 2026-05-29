using CommunityToolkit.Mvvm.ComponentModel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.Generic;

namespace EdaSimulator.UI.ViewModels
{
    public partial class OscilloscopeViewModel : ObservableObject
    {
        [ObservableProperty]
        private PlotModel _simPlotModel;

        public OscilloscopeViewModel()
        {
            // MVVMTK0034: Use the generated property 'SimPlotModel' for all post-construction mutation,
            // but direct field access is safe in the constructor before source-generators hook in.
            _simPlotModel = new PlotModel
            {
                Title = "SPICE Trace Viewer",
                TextColor = OxyColors.LightGray,
                PlotAreaBorderColor = OxyColors.Gray
            };

            _simPlotModel.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendPosition = OxyPlot.Legends.LegendPosition.TopRight,
                LegendBackground = OxyColor.FromArgb(180, 40, 40, 40),
                LegendBorder = OxyColors.DimGray,
                LegendTextColor = OxyColors.LightGray
            });

            _simPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Time (s)",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.DarkGray,
                TicklineColor = OxyColors.Gray,
                AxislineColor = OxyColors.Gray
            });

            _simPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Voltage (V)",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.DarkGray,
                TicklineColor = OxyColors.Gray,
                AxislineColor = OxyColors.Gray
            });
        }

        public void RenderTrace(string traceName, IList<double> x, IList<double> y)
        {
            RenderTraceColored(traceName, x, y, OxyColors.Cyan);
        }

        public void RenderTraceColored(string traceName, IList<double> x, IList<double> y, OxyColor color)
        {
            var series = new LineSeries
            {
                Title = traceName,
                Color = color,
                MarkerType = MarkerType.None,
                StrokeThickness = 2,
                TrackerFormatString = "{0}\nTime: {2:0.000E+00} s\nValue: {4:0.000}"
            };

            int count = System.Math.Min(x.Count, y.Count);
            for (int i = 0; i < count; i++)
                series.Points.Add(new DataPoint(x[i], y[i]));

            SimPlotModel.Series.Add(series);
            SimPlotModel.InvalidatePlot(true);
        }

        /// <summary>Renders a Bode magnitude plot (dB vs log-frequency) from AC analysis data.</summary>
        public void RenderBodePlot(string traceName, IList<double> freq, IList<double> mag)
        {
            // Switch X-axis to logarithmic for frequency domain
            SimPlotModel.Axes.Clear();
            SimPlotModel.Axes.Add(new LogarithmicAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Frequency (Hz)",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.DarkGray,
                TicklineColor = OxyColors.Gray
            });
            SimPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Magnitude (dB)",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColors.DarkGray,
                TicklineColor = OxyColors.Gray
            });
            SimPlotModel.Title = "Bode Plot (AC Analysis)";

            var series = new LineSeries
            {
                Title = traceName,
                Color = OxyColors.Lime,
                StrokeThickness = 2,
                TrackerFormatString = "{0}\nFreq: {2:0.000E+00} Hz\ndB: {4:0.00}"
            };

            int count = System.Math.Min(freq.Count, mag.Count);
            for (int i = 0; i < count; i++)
            {
                if (freq[i] > 0)
                {
                    // Convert linear magnitude to dB: 20*log10(|V|)
                    double db = 20.0 * System.Math.Log10(System.Math.Abs(mag[i]) + 1e-300);
                    series.Points.Add(new DataPoint(freq[i], db));
                }
            }

            SimPlotModel.Series.Add(series);
            SimPlotModel.InvalidatePlot(true);
        }

        public void ClearTraces()
        {
            SimPlotModel.Series.Clear();
            // Reset to time-domain axes
            SimPlotModel.Axes.Clear();
            SimPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom, Title = "Time (s)",
                MajorGridlineStyle = LineStyle.Dot, MajorGridlineColor = OxyColors.DarkGray,
                TicklineColor = OxyColors.Gray, AxislineColor = OxyColors.Gray
            });
            SimPlotModel.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left, Title = "Voltage (V)",
                MajorGridlineStyle = LineStyle.Dot, MajorGridlineColor = OxyColors.DarkGray,
                TicklineColor = OxyColors.Gray, AxislineColor = OxyColors.Gray
            });
            SimPlotModel.Title = "SPICE Trace Viewer";
            SimPlotModel.InvalidatePlot(true);
        }
    }
}
