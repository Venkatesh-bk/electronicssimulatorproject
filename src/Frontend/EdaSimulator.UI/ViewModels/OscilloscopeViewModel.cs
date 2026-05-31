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
        public void RenderTraceColored(string traceName, IList<double> x, IList<double> y, OxyColor color, string trackerFormat = "{0}\nTime: {2:0.000E+00} s\nValue: {4:0.000}")
        {
            var series = new LineSeries
            {
                Title            = traceName,
                Color            = color,
                MarkerType       = MarkerType.None,
                StrokeThickness  = 2,
                TrackerFormatString = trackerFormat
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

        /// <summary>Configures the plot model axes for a custom swept parameter (e.g. DC sweep).</summary>
        public void SetupSweepPlot(string xAxisTitle, string plotTitle)
        {
            SimPlotModel.Series.Clear();
            SimPlotModel.Axes.Clear();

            SimPlotModel.Axes.Add(new LinearAxis
            {
                Position           = AxisPosition.Bottom,
                Title              = xAxisTitle,
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromArgb(60, 80, 80, 140),
                TicklineColor      = OxyColors.Gray,
                AxislineColor      = OxyColors.Gray,
                TextColor          = OxyColors.LightGray
            });
            SimPlotModel.Axes.Add(new LinearAxis
            {
                Position           = AxisPosition.Left,
                Title              = "Voltage (V) / Current (A)",
                MajorGridlineStyle = LineStyle.Dot,
                MajorGridlineColor = OxyColor.FromArgb(60, 80, 80, 140),
                TicklineColor      = OxyColors.Gray,
                AxislineColor      = OxyColors.Gray,
                TextColor          = OxyColors.LightGray
            });

            SimPlotModel.Title = plotTitle;
            SimPlotModel.InvalidatePlot(true);

            TraceInfos.Clear();
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

        /// <summary>
        /// Highlights the series matching the given net name (by thickening it)
        /// and resets others to normal thickness.
        /// </summary>
        public void HighlightTrace(string netName)
        {
            if (string.IsNullOrEmpty(netName)) return;

            string normalizedNet = netName.Trim().ToUpper();

            foreach (var series in SimPlotModel.Series.OfType<LineSeries>())
            {
                string title = series.Title?.ToUpper() ?? "";
                if (title == normalizedNet || title == $"V({normalizedNet})" || title == $"I({normalizedNet})")
                {
                    series.StrokeThickness = 4.5;
                }
                else
                {
                    series.StrokeThickness = 1.5;
                }
            }
            SimPlotModel.InvalidatePlot(true);
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

        /// <summary>
        /// Evaluates a math channel expression and appends the computed trace to the scope.
        /// Supports differential traces (e.g. V(N1)-V(N2)) and Fast Fourier Transforms (e.g. FFT(V(N1))).
        /// </summary>
        public bool AddMathChannel(string expression, out string error)
        {
            error = string.Empty;
            expression = expression.Trim().Replace(" ", "");

            if (string.IsNullOrEmpty(expression))
            {
                error = "Expression is empty.";
                return false;
            }

            // Case 1: FFT(V(NET))
            if (expression.StartsWith("FFT(", StringComparison.OrdinalIgnoreCase) && expression.EndsWith(")"))
            {
                string inner = expression.Substring(4, expression.Length - 5);
                var sourceTrace = TraceInfos.FirstOrDefault(t => string.Equals(t.Name, inner, StringComparison.OrdinalIgnoreCase));
                if (sourceTrace == null)
                {
                    error = $"Source trace '{inner}' not found.";
                    return false;
                }

                int n = sourceTrace.Y.Count;
                if (n < 4)
                {
                    error = "Not enough data points to compute FFT.";
                    return false;
                }

                // Find largest power of 2 <= n
                int fftSize = 1;
                while (fftSize * 2 <= n) fftSize *= 2;

                var complexData = new System.Numerics.Complex[fftSize];
                for (int i = 0; i < fftSize; i++)
                {
                    complexData[i] = new System.Numerics.Complex(sourceTrace.Y[i], 0);
                }

                // Run FFT
                FftHelper.Fft(complexData);

                // Calculate average dt
                double totalTime = sourceTrace.X[fftSize - 1] - sourceTrace.X[0];
                double dt = totalTime / (fftSize - 1);
                if (dt <= 0)
                {
                    error = "Invalid time step for frequency calculation.";
                    return false;
                }
                double samplingFreq = 1.0 / dt;

                var freqs = new List<double>();
                var magnitudes = new List<double>();

                // Single-sided spectrum
                int halfSize = fftSize / 2;
                for (int k = 0; k < halfSize; k++)
                {
                    double freq = k * samplingFreq / fftSize;
                    double mag = complexData[k].Magnitude / fftSize;
                    // Double all AC components to account for negative frequencies
                    if (k > 0) mag *= 2.0;

                    freqs.Add(freq);
                    magnitudes.Add(mag);
                }

                RenderBodePlot($"FFT({sourceTrace.Name})", freqs, magnitudes);
                return true;
            }

            // Case 2: V(A)-V(B) or similar differential math
            char[] operators = new[] { '-', '+', '*', '/' };
            int opIdx = expression.IndexOfAny(operators);
            if (opIdx > 0)
            {
                char op = expression[opIdx];
                string leftName = expression.Substring(0, opIdx);
                string rightName = expression.Substring(opIdx + 1);

                var leftTrace = TraceInfos.FirstOrDefault(t => string.Equals(t.Name, leftName, StringComparison.OrdinalIgnoreCase));
                var rightTrace = TraceInfos.FirstOrDefault(t => string.Equals(t.Name, rightName, StringComparison.OrdinalIgnoreCase));

                if (leftTrace == null)
                {
                    error = $"Left trace '{leftName}' not found.";
                    return false;
                }
                if (rightTrace == null)
                {
                    error = $"Right trace '{rightName}' not found.";
                    return false;
                }

                int count = Math.Min(leftTrace.X.Count, rightTrace.X.Count);
                if (count == 0)
                {
                    error = "One of the source traces has no data.";
                    return false;
                }

                var newX = new List<double>();
                var newY = new List<double>();

                for (int i = 0; i < count; i++)
                {
                    newX.Add(leftTrace.X[i]);
                    double yVal = 0;
                    switch (op)
                    {
                        case '+': yVal = leftTrace.Y[i] + rightTrace.Y[i]; break;
                        case '-': yVal = leftTrace.Y[i] - rightTrace.Y[i]; break;
                        case '*': yVal = leftTrace.Y[i] * rightTrace.Y[i]; break;
                        case '/': yVal = rightTrace.Y[i] != 0 ? leftTrace.Y[i] / rightTrace.Y[i] : 0; break;
                    }
                    newY.Add(yVal);
                }

                RenderTraceColored($"{leftTrace.Name}{op}{rightTrace.Name}", newX, newY, OxyColors.Violet);
                return true;
            }

            error = "Unsupported expression format. Use e.g. V(N1)-V(N2) or FFT(V(N1)).";
            return false;
        }
    }

    /// <summary>
    /// Radix-2 decimation-in-time FFT helper.
    /// </summary>
    public static class FftHelper
    {
        public static void Fft(System.Numerics.Complex[] a)
        {
            int n = a.Length;
            if (n <= 1) return;

            var even = new System.Numerics.Complex[n / 2];
            var odd = new System.Numerics.Complex[n / 2];
            for (int i = 0; i < n / 2; i++)
            {
                even[i] = a[2 * i];
                odd[i] = a[2 * i + 1];
            }

            Fft(even);
            Fft(odd);

            for (int k = 0; k < n / 2; k++)
            {
                double theta = -2.0 * Math.PI * k / n;
                var w = new System.Numerics.Complex(Math.Cos(theta), Math.Sin(theta)) * odd[k];
                a[k] = even[k] + w;
                a[k + n / 2] = even[k] - w;
            }
        }
    }
}
