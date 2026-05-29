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

        private void MeasureBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.TraceInfos.Count == 0)
            {
                MeasurementText.Text = "No traces loaded.";
                return;
            }

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
            StatusText.Text = $"Measurements updated — {ViewModel.TraceInfos.Count} trace(s)";
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
            MeasurementText.Text = "Click 📏 to measure";
            StatusText.Text = "Traces cleared.";
        }
    }
}
