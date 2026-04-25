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
            _simPlotModel = new PlotModel 
            { 
                Title = "SPICE Trace Viewer",
                TextColor = OxyColors.LightGray,
                PlotAreaBorderColor = OxyColors.Gray
            };

            // Overlay a transparent Legend Box identically to MATLAB defaults
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
            var series = new LineSeries 
            { 
                Title = traceName, 
                MarkerType = MarkerType.None,
                StrokeThickness = 2,
                TrackerFormatString = "{0}\nTime: {2:0.000E+00} s\nValue: {4:0.000} U"
            };

            int count = System.Math.Min(x.Count, y.Count);

            for(int i = 0; i < count; i++)
            {
                series.Points.Add(new DataPoint(x[i], y[i]));
            }

            _simPlotModel.Series.Add(series);
            _simPlotModel.InvalidatePlot(true);
        }

        public void ClearTraces()
        {
            _simPlotModel.Series.Clear();
            _simPlotModel.InvalidatePlot(true);
        }
    }
}
