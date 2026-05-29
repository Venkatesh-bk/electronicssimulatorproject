using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace EdaSimulator.UI.ViewModels
{
    /// <summary>Metadata for one displayed trace — used by the sidebar panel.</summary>
    public class TraceInfo
    {
        public string Name        { get; set; } = "";
        public string ColorHex    { get; set; } = "#00FFFF";
        public double Min         { get; set; }
        public double Max         { get; set; }
        public double Average     { get; set; }
        public double PeakToPeak  => Max - Min;
        public string StatsText   => $"min={Min:G3}  max={Max:G3}\navg={Average:G3}  pp={PeakToPeak:G3}";

        // Raw data kept for CSV export
        public List<double> X  { get; set; } = new();
        public List<double> Y  { get; set; } = new();
    }

    /// <summary>
    /// ViewModel backing the OscilloscopeWindow.
    /// Manages OxyPlot plot model, trace metadata, and data export.
    /// </summary>
    public partial class OscilloscopeViewModel : ObservableObject
    {
        // ── Plot model ───────────────────────────────────────────────────────────────

        [ObservableProperty]
        private PlotModel _simPlotModel;

        // ── Trace metadata (sidebar) ─────────────────────────────────────────────────

        public ObservableCollection<TraceInfo> TraceInfos { get; } = new();

        public bool HasTraceData => TraceInfos.Count > 0;

        // ── Constructor ──────────────────────────────────────────────────────────────

        public OscilloscopeViewModel()
        {
            _simPlotModel = CreateTimeDomainModel();
        }

        // ── Public API: add traces ────────────────────────────────────────────────────

        /// <summary>Adds a trace using the default color (Cyan).</summary>
        public void RenderTrace(string traceName, IList<double> x, IList<double> y)
            => RenderTraceColored(traceName, x, y, OxyColors.Cyan);

        /// <summary>Adds a colored trace to the plot and registers metadata in the sidebar.</summary>
        public void RenderTraceColored(string traceName, IList<double> x, IList<double> y, OxyColor color)
        {
            var series = new LineSeries
            {
                Title            = traceName,
                Color            = color,
                MarkerType       = MarkerType.None,
                StrokeThickness  = 2,
                TrackerFormatString = "{0}\nTime: {2:0.000E+00} s\nValue: {4:0.000}"
            };

            int count = Math.Min(x.Count, y.Count);
            for (int i = 0; i < count; i++)
                series.Points.Add(new DataPoint(x[i], y[i]));

            SimPlotModel.Series.Add(series);
            SimPlotModel.InvalidatePlot(true);

            // Compute stats
            var yVals = y.Take(count).ToList();
            var info = new TraceInfo
            {
                Name     = traceName,
                ColorHex = ColorToHex(color),
                Min      = yVals.Count > 0 ? yVals.Min() : 0,
                Max      = yVals.Count > 0 ? yVals.Max() : 0,
                Average  = yVals.Count > 0 ? yVals.Average() : 0,
                X        = x.Take(count).ToList(),
                Y        = yVals
            };

            TraceInfos.Add(info);
            OnPropertyChanged(nameof(HasTraceData));
        }

        /// <summary>Renders a Bode magnitude plot (dB vs log-frequency) from AC analysis.</summary>
        public void RenderBodePlot(string traceName, IList<double> freq, IList<double> mag)
        {
            // Switch to logarithmic X axis
            SimPlotModel.Axes.Clear();
            SimPlotModel.Axes.Add(new LogarithmicAxis
            {
                Position            = AxisPosition.Bottom,
                Title               = "Frequency (Hz)",
                MajorGridlineStyle  = LineStyle.Dot,
                MajorGridlineColor  = OxyColors.DarkGray,
                TicklineColor       = OxyColors.Gray,
                TextColor           = OxyColors.LightGray
            });
            SimPlotModel.Axes.Add(new LinearAxis
            {
                Position            = AxisPosition.Left,
                Title               = "Magnitude (dB)",
                MajorGridlineStyle  = LineStyle.Dot,
                MajorGridlineColor  = OxyColors.DarkGray,
                TicklineColor       = OxyColors.Gray,
                TextColor           = OxyColors.LightGray
            });
            SimPlotModel.Title = "Bode Plot — AC Analysis";

            var series = new LineSeries
            {
                Title            = traceName,
                Color            = OxyColors.LimeGreen,
                StrokeThickness  = 2,
                TrackerFormatString = "{0}\nFreq: {2:0.000E+00} Hz\ndB: {4:0.00}"
            };

            var dbValues = new List<double>();
            int count = Math.Min(freq.Count, mag.Count);
            for (int i = 0; i < count; i++)
            {
                if (freq[i] > 0)
                {
                    double db = 20.0 * Math.Log10(Math.Abs(mag[i]) + 1e-300);
                    series.Points.Add(new DataPoint(freq[i], db));
                    dbValues.Add(db);
                }
            }

            SimPlotModel.Series.Add(series);
            SimPlotModel.InvalidatePlot(true);

            var info = new TraceInfo
            {
                Name     = traceName + " (dB)",
                ColorHex = "#32CD32",
                Min      = dbValues.Count > 0 ? dbValues.Min() : 0,
                Max      = dbValues.Count > 0 ? dbValues.Max() : 0,
                Average  = dbValues.Count > 0 ? dbValues.Average() : 0,
                X        = freq.Take(count).ToList(),
                Y        = dbValues
            };
            TraceInfos.Add(info);
            OnPropertyChanged(nameof(HasTraceData));
        }

        /// <summary>Clears all traces and resets axes to time-domain defaults.</summary>
        public void ClearTraces()
        {
            SimPlotModel.Series.Clear();
            SimPlotModel.Axes.Clear();
            foreach (var axis in CreateTimeDomainModel().Axes)
                SimPlotModel.Axes.Add(axis);
            SimPlotModel.Title = "SPICE Waveform Viewer";
            SimPlotModel.InvalidatePlot(true);

            TraceInfos.Clear();
            OnPropertyChanged(nameof(HasTraceData));
        }

        // ── CSV Export ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a multi-column CSV from all loaded traces.
        /// Column 0 = time/freq axis, columns 1..N = trace values.
        /// </summary>
        public string BuildCsvExport()
        {
            if (TraceInfos.Count == 0) return "# No data";

            var sb = new StringBuilder();

            // Header
            sb.Append("Time_s");
            foreach (var t in TraceInfos)
                sb.Append($",{t.Name.Replace(",", "_")}");
            sb.AppendLine();

            // Find longest trace
            int maxLen = TraceInfos.Max(t => t.X.Count);

            for (int i = 0; i < maxLen; i++)
            {
                // X axis (use first trace's time axis)
                double xVal = i < TraceInfos[0].X.Count ? TraceInfos[0].X[i] : double.NaN;
                sb.Append($"{xVal:G8}");

                foreach (var t in TraceInfos)
                {
                    double yVal = i < t.Y.Count ? t.Y[i] : double.NaN;
                    sb.Append($",{yVal:G8}");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        private static PlotModel CreateTimeDomainModel()
        {
            var model = new PlotModel
            {
                Title       = "SPICE Waveform Viewer",
                Background  = OxyColors.Transparent,
                TextColor   = OxyColors.LightGray,
                PlotAreaBorderColor = OxyColor.FromArgb(80, 80, 80, 120)
            };

            model.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendBackground     = OxyColor.FromArgb(200, 20, 20, 40),
                LegendBorderThickness = 0,
                LegendTextColor      = OxyColors.LightGray
            });
            model.Axes.Add(new LinearAxis
            {
                Position           = AxisPosition.Bottom,
                Title              = "Time (s)",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromArgb(60, 80, 80, 140),
                TicklineColor      = OxyColors.Gray,
                AxislineColor      = OxyColors.Gray,
                TextColor          = OxyColors.LightGray
            });
            model.Axes.Add(new LinearAxis
            {
                Position           = AxisPosition.Left,
                Title              = "Voltage (V)",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromArgb(60, 80, 80, 140),
                TicklineColor      = OxyColors.Gray,
                AxislineColor      = OxyColors.Gray,
                TextColor          = OxyColors.LightGray
            });

            return model;
        }

        private static string ColorToHex(OxyColor c)
            => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
