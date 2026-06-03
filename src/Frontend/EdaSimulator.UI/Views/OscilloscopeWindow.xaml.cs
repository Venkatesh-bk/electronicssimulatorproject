using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EdaSimulator.UI.ViewModels;
using Microsoft.Win32;
using OxyPlot;

namespace EdaSimulator.UI.Views
{
    /// <summary>
    /// Professional oscilloscope window with zoom, CSV/PNG export,
    /// trace sidebar and cursor measurements.
    /// </summary>
    public partial class OscilloscopeWindow : Window
    {
        public OscilloscopeViewModel ViewModel { get; }

        public OscilloscopeWindow()
        {
            InitializeComponent();
            ViewModel = new OscilloscopeViewModel();
            DataContext = ViewModel;
            HookPlotEvents();
        }

        // ── Window lifecycle ────────────────────────────────────────────────────────

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }

        // ── Zoom controls ────────────────────────────────────────────────────────────

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            PlotView.Model?.Axes.ToList().ForEach(a =>
            {
                double mid = (a.ActualMaximum + a.ActualMinimum) / 2;
                double half = (a.ActualMaximum - a.ActualMinimum) / 4;
                a.Zoom(mid - half, mid + half);
            });
            PlotView.InvalidatePlot();
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            PlotView.Model?.Axes.ToList().ForEach(a =>
            {
                double mid = (a.ActualMaximum + a.ActualMinimum) / 2;
                double half = (a.ActualMaximum - a.ActualMinimum);
                a.Zoom(mid - half, mid + half);
            });
            PlotView.InvalidatePlot();
        }

        private void ZoomFitBtn_Click(object sender, RoutedEventArgs e)
        {
            PlotView.Model?.ResetAllAxes();
            PlotView.InvalidatePlot();
        }

        // ── Measurements ─────────────────────────────────────────────────────────────

        private bool _cursorsEnabled = false;
        private OxyPlot.Annotations.LineAnnotation? _cursorA;
        private OxyPlot.Annotations.LineAnnotation? _cursorB;

#pragma warning disable CS0618
        private void HookPlotEvents()
        {
            ViewModel.SimPlotModel.MouseDown += (s, e) =>
            {
                if (!_cursorsEnabled) return;
                if (e.ChangedButton == OxyMouseButton.Left)
                {
                    var xAxis = ViewModel.SimPlotModel.Axes.FirstOrDefault(a => a.Position == OxyPlot.Axes.AxisPosition.Bottom);
                    var yAxis = ViewModel.SimPlotModel.Axes.FirstOrDefault(a => a.Position == OxyPlot.Axes.AxisPosition.Left);
                    if (xAxis != null && yAxis != null && _cursorA != null && _cursorB != null)
                    {
                        var dataPoint = OxyPlot.Axes.Axis.InverseTransform(e.Position, xAxis, yAxis);
                        double clickX = dataPoint.X;

                        // Determine which cursor is closer
                        double distA = Math.Abs(clickX - _cursorA.X);
                        double distB = Math.Abs(clickX - _cursorB.X);
                        if (distA < distB)
                        {
                            _cursorA.X = clickX;
                        }
                        else
                        {
                            _cursorB.X = clickX;
                        }

                        UpdateCursorMeasurements();
                        PlotView.InvalidatePlot();
                        e.Handled = true;
                    }
                }
            };
        }
#pragma warning restore CS0618

        private void MeasureBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.TraceInfos.Count == 0)
            {
                MeasurementText.Text = "No traces loaded.";
                return;
            }

            var xAxis = ViewModel.SimPlotModel.Axes.FirstOrDefault(a => a.Position == OxyPlot.Axes.AxisPosition.Bottom);
            if (xAxis == null) return;

            _cursorsEnabled = !_cursorsEnabled;

            if (_cursorsEnabled)
            {
                double minX = xAxis.ActualMinimum;
                double maxX = xAxis.ActualMaximum;
                if (double.IsNaN(minX) || double.IsInfinity(minX)) minX = 0;
                if (double.IsNaN(maxX) || double.IsInfinity(maxX)) maxX = 1;

                if (_cursorA == null)
                {
                    _cursorA = new OxyPlot.Annotations.LineAnnotation
                    {
                        Type = OxyPlot.Annotations.LineAnnotationType.Vertical,
                        Color = OxyColors.LightGreen,
                        LineStyle = LineStyle.Dash,
                        StrokeThickness = 1.5,
                        Text = "A",
                        TextColor = OxyColors.LightGreen,
                        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Right,
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Top
                    };
                }
                if (_cursorB == null)
                {
                    _cursorB = new OxyPlot.Annotations.LineAnnotation
                    {
                        Type = OxyPlot.Annotations.LineAnnotationType.Vertical,
                        Color = OxyColors.HotPink,
                        LineStyle = LineStyle.Dash,
                        StrokeThickness = 1.5,
                        Text = "B",
                        TextColor = OxyColors.HotPink,
                        TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Left,
                        TextVerticalAlignment = OxyPlot.VerticalAlignment.Top
                    };
                }

                _cursorA.X = minX + (maxX - minX) * 0.25;
                _cursorB.X = minX + (maxX - minX) * 0.75;

                if (!ViewModel.SimPlotModel.Annotations.Contains(_cursorA))
                    ViewModel.SimPlotModel.Annotations.Add(_cursorA);
                if (!ViewModel.SimPlotModel.Annotations.Contains(_cursorB))
                    ViewModel.SimPlotModel.Annotations.Add(_cursorB);

                UpdateCursorMeasurements();
                StatusText.Text = "Cursors enabled. Click on plot to position Cursor A or B.";
            }
            else
            {
                RemoveCursors();

                // Revert to displaying trace statistics
                var sb = new StringBuilder();
                foreach (var ti in ViewModel.TraceInfos)
                {
                    sb.AppendLine($"── {ti.Name} ──");
                    sb.AppendLine($"  Min:  {ti.Min:G4}");
                    sb.AppendLine($"  Max:  {ti.Max:G4}");
                    sb.AppendLine($"  Avg:  {ti.Average:G4}");
                    sb.AppendLine($"  Pk-Pk:{ti.PeakToPeak:G4}");
                    sb.AppendLine();
                }
                MeasurementText.Text = sb.ToString().Trim();
                StatusText.Text = "Cursors disabled.";
            }

            PlotView.InvalidatePlot();
        }

        private void RemoveCursors()
        {
            if (_cursorA != null && ViewModel.SimPlotModel.Annotations.Contains(_cursorA))
                ViewModel.SimPlotModel.Annotations.Remove(_cursorA);
            if (_cursorB != null && ViewModel.SimPlotModel.Annotations.Contains(_cursorB))
                ViewModel.SimPlotModel.Annotations.Remove(_cursorB);
        }

        private void UpdateCursorMeasurements()
        {
            if (ViewModel.SimPlotModel == null || _cursorA == null || _cursorB == null) return;

            var sb = new StringBuilder();
            sb.AppendLine("📐 CURSOR MEASUREMENTS");
            sb.AppendLine($"  Cursor A: {_cursorA.X:G4} s");
            sb.AppendLine($"  Cursor B: {_cursorB.X:G4} s");
            double dx = _cursorB.X - _cursorA.X;
            sb.AppendLine($"  Delta X:  {Math.Abs(dx):G4} s");
            if (Math.Abs(dx) > 0)
            {
                sb.AppendLine($"  Freq (1/dX): {Math.Abs(1.0 / dx):G4} Hz");
            }
            sb.AppendLine();

            foreach (var ti in ViewModel.TraceInfos)
            {
                double yA = InterpolateY(ti, _cursorA.X);
                double yB = InterpolateY(ti, _cursorB.X);
                double dy = yB - yA;
                sb.AppendLine($"── {ti.Name} ──");
                sb.AppendLine($"  Val A:  {yA:G4}");
                sb.AppendLine($"  Val B:  {yB:G4}");
                sb.AppendLine($"  Delta Y:{dy:G4}");
                sb.AppendLine();
            }

            MeasurementText.Text = sb.ToString().Trim();
        }

        private double InterpolateY(TraceInfo trace, double targetX)
        {
            if (trace.X == null || trace.X.Count == 0) return 0;
            if (targetX <= trace.X[0]) return trace.Y[0];
            if (targetX >= trace.X[trace.X.Count - 1]) return trace.Y[trace.Y.Count - 1];

            // Binary search to find index
            int idx = trace.X.BinarySearch(targetX);
            if (idx >= 0) return trace.Y[idx];

            int nextIdx = ~idx;
            if (nextIdx == 0) return trace.Y[0];
            if (nextIdx >= trace.X.Count) return trace.Y[trace.Y.Count - 1];

            int prevIdx = nextIdx - 1;
            double x0 = trace.X[prevIdx];
            double x1 = trace.X[nextIdx];
            double y0 = trace.Y[prevIdx];
            double y1 = trace.Y[nextIdx];

            // Linear interpolation
            if (Math.Abs(x1 - x0) < 1e-15) return y0;
            return y0 + (targetX - x0) * (y1 - y0) / (x1 - x0);
        }

        // ── CSV Export ───────────────────────────────────────────────────────────────

        private void ExportCsvBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.HasTraceData)
            {
                MessageBox.Show("No waveform data to export.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title      = "Export Waveform Data as CSV",
                Filter     = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName   = $"simulation_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var csv = ViewModel.BuildCsvExport();
                File.WriteAllText(dlg.FileName, csv, Encoding.UTF8);
                StatusText.Text = $"CSV exported: {Path.GetFileName(dlg.FileName)}";
                MessageBox.Show($"Waveform data exported to:\n{dlg.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── PNG Export ───────────────────────────────────────────────────────────────

        private void ExportPngBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title      = "Save Waveform Plot as PNG",
                Filter     = "PNG Image (*.png)|*.png",
                DefaultExt = ".png",
                FileName   = $"waveform_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                // Use OxyPlot's built-in PNG exporter
                using var stream = File.Create(dlg.FileName);
                var exporter = new OxyPlot.Wpf.PngExporter
                {
                    Width  = 1600,
                    Height = 900
                };
                exporter.Export(ViewModel.SimPlotModel, stream);
                StatusText.Text = $"PNG saved: {Path.GetFileName(dlg.FileName)}";
                MessageBox.Show($"Plot saved as PNG:\n{dlg.FileName}",
                    "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PNG save failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Clear ────────────────────────────────────────────────────────────────────

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearTraces();
            _cursorsEnabled = false;
            RemoveCursors();
            MeasurementText.Text = "Click 📏 to measure";
            StatusText.Text = "Traces cleared.";
        }

        private void AddMathBtn_Click(object sender, RoutedEventArgs e)
        {
            var expr = MathExpressionTxt.Text;
            if (ViewModel.AddMathChannel(expr, out string error))
            {
                StatusText.Text = $"Math channel added successfully: {expr}";
                PlotView.InvalidatePlot();
            }
            else
            {
                MessageBox.Show($"Could not add math channel:\n{error}", "Math Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
