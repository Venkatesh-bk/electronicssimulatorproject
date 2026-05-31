using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EdaSimulator.Engines.PCB
{
    /// <summary>
    /// Result returned by <see cref="FreeRoutingService.RouteAsync"/>.
    /// </summary>
    public class FreeRoutingResult
    {
        public bool   Success        { get; set; }
        public string SessionFilePath { get; set; } = "";
        public string Log            { get; set; } = "";
        public string ErrorMessage   { get; set; } = "";
        public int    RoutedSegments { get; set; }
    }

    /// <summary>
    /// Orchestrates the complete FreeRouting autorouting pipeline:
    ///   1. Export PcbDocument → Specctra .dsn
    ///   2. Launch FreeRouting CLI (freerouting.jar) and await completion
    ///   3. Import the resulting .ses back into the PcbDocument
    ///
    /// FreeRouting is an open-source Java autorouter:
    ///   https://github.com/freerouting/freerouting
    /// Download freerouting-x.x.x-executable.jar and set the path via
    /// EdaSimulator Settings → FreeRouting JAR Path.
    /// </summary>
    public static class FreeRoutingService
    {
        // ── Discovery ─────────────────────────────────────────────────────────────

        /// <summary>Probes standard paths for the FreeRouting JAR.</summary>
        public static string LocateFreeRoutingJar()
        {
            string[] candidates =
            {
                // Project-local vendor bundle
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "freerouting.jar"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engines", "freerouting.jar"),

                // User-set custom path (persisted in Settings)
                EdaSimulator.Engines.Settings.SettingsManager.Instance.Current.FreeRoutingJarPath,

                // Common developer install locations
                @"C:\Tools\freerouting\freerouting.jar",
                @"C:\Program Files\FreeRouting\freerouting.jar",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "freerouting.jar")
            };

            foreach (var path in candidates)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return path;
            }
            return "";
        }

        public static bool IsFreeRoutingAvailable()
            => !string.IsNullOrEmpty(LocateFreeRoutingJar());

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs the full autorouting pipeline asynchronously.
        /// The <paramref name="pcbDoc"/> is modified in-place with the routed result.
        /// </summary>
        public static async Task<FreeRoutingResult> RouteAsync(
            PcbDocument pcbDoc,
            CancellationToken cancellationToken = default)
        {
            var result = new FreeRoutingResult();

            // ── Step 1: Find FreeRouting JAR ──────────────────────────────────────
            string jarPath = LocateFreeRoutingJar();
            if (string.IsNullOrEmpty(jarPath))
            {
                result.ErrorMessage =
                    "FreeRouting JAR not found.\n" +
                    "Download freerouting-x.x.x-executable.jar from:\n" +
                    "  https://github.com/freerouting/freerouting/releases\n\n" +
                    "Then set the path in:\n" +
                    "  Tools → Preferences → FreeRouting JAR Path";
                return result;
            }

            // ── Step 2: Create temp directory for DSN/SES exchange ────────────────
            string tmpDir  = Path.Combine(Path.GetTempPath(), "eda_freerouting_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tmpDir);

            string dsnPath = Path.Combine(tmpDir, "board.dsn");
            string sesPath = Path.Combine(tmpDir, "board.ses");

            try
            {
                // ── Step 3: Export DSN ────────────────────────────────────────────
                string dsnContent = SpecctraDsnExporter.Export(pcbDoc);
                await File.WriteAllTextAsync(dsnPath, dsnContent, cancellationToken);

                result.Log += $"[DSN] Exported {pcbDoc.Footprints.Count} footprints, " +
                              $"{pcbDoc.Ratsnest.Count} ratsnest connections to {dsnPath}\n";

                // ── Step 4: Launch FreeRouting ────────────────────────────────────
                // FreeRouting CLI: java -jar freerouting.jar -de board.dsn -do board.ses -mp 50
                var psi = new ProcessStartInfo
                {
                    FileName        = "java",
                    Arguments       = $"-jar \"{jarPath}\" -de \"{dsnPath}\" -do \"{sesPath}\" -mp 50",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow  = true,
                    WorkingDirectory = tmpDir
                };

                using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                var stdoutBuilder = new System.Text.StringBuilder();
                var stderrBuilder = new System.Text.StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) stdoutBuilder.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) stderrBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for FreeRouting to complete (with cancellation)
                var tcs = new TaskCompletionSource<bool>();
                process.Exited += (_, _) => tcs.TrySetResult(true);

                using var reg = cancellationToken.Register(() =>
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                    catch { /* ignore */ }
                    tcs.TrySetCanceled();
                });

                await tcs.Task;

                result.Log += $"\n[FreeRouting stdout]\n{stdoutBuilder}\n";
                if (stderrBuilder.Length > 0)
                    result.Log += $"[FreeRouting stderr]\n{stderrBuilder}\n";

                // ── Step 5: Import SES ────────────────────────────────────────────
                if (!File.Exists(sesPath))
                {
                    result.ErrorMessage = "FreeRouting did not produce a .ses output file.\n" +
                                         "Ensure Java (JRE 11+) is installed and on PATH.\n\n" +
                                         "[Log]\n" + result.Log;
                    return result;
                }

                int routed = SpecctraSessionImporter.Import(sesPath, pcbDoc);

                result.Success        = true;
                result.SessionFilePath = sesPath;
                result.RoutedSegments = routed;
                result.Log += $"\n[SES] Imported {routed} route segments successfully.";
            }
            catch (OperationCanceledException)
            {
                result.ErrorMessage = "Autorouting was cancelled by user.";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Autorouting failed: {ex.GetType().Name}: {ex.Message}";
            }
            finally
            {
                // Clean up temp directory
                try { Directory.Delete(tmpDir, recursive: true); }
                catch { /* best effort */ }
            }

            return result;
        }
    }
}
