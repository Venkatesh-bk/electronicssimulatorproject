using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EdaSimulator.Engines.Simulation
{
    public class SpiceExecutionResult
    {
        public bool Success { get; set; }
        public string OutputLog { get; set; } = string.Empty;
        public string RawFilePath { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>
    /// Thread-safe wrapper responsible for dispatching Math Execution queries natively 
    /// to a compiled SPICE backend binary via System.Diagnostics.
    /// Uses NgSpiceLocator to auto-discover the binary across common install paths.
    /// </summary>
    public class SpiceExecutionService
    {
        private readonly string? _resolvedNgSpicePath;

        /// <summary>
        /// Creates an execution service. If userConfiguredPath is null or empty,
        /// NgSpiceLocator searches common install locations automatically.
        /// </summary>
        public SpiceExecutionService(string? userConfiguredPath = null)
        {
            _resolvedNgSpicePath = NgSpiceLocator.FindNgSpice(userConfiguredPath);
        }

        /// <summary>True when ngspice was found on this system.</summary>
        public bool IsNgSpiceAvailable => !string.IsNullOrEmpty(_resolvedNgSpicePath);

        /// <summary>The full path to ngspice_con.exe, or null if not found.</summary>
        public string? NgSpicePath => _resolvedNgSpicePath;

        public async Task<SpiceExecutionResult> RunSimulationAsync(string netlistContent, CancellationToken cancellationToken = default)
        {
            var result = new SpiceExecutionResult();

            // Guard: ngspice must be installed/discoverable before we can run
            if (!IsNgSpiceAvailable)
            {
                string vendorPath = NgSpiceLocator.GetRecommendedVendorPath();
                result.Success = false;
                result.ErrorMessage =
                    "ngspice simulation engine not found.\n\n" +
                    "To enable simulation, choose ONE of:\n" +
                    "  A) Install ngspice from https://ngspice.sourceforge.io/download.html\n" +
                    "  B) Place ngspice_con.exe in:\n" +
                    $"     {vendorPath}\n" +
                    "  C) Set the custom path via Tools → Preferences → Simulation\n\n" +
                    "ngspice is free, open-source, and takes ~30 seconds to install.";
                return result;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "EdaSimulatorSims");
            if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

            string cirFileName = Path.Combine(tempDir, $"sim_{Guid.NewGuid().ToString("N")}.cir");
            string rawFileName = Path.ChangeExtension(cirFileName, ".raw");

            try
            {
                // Inject the batch execution save parameter into the netlist
                // Ngspice uses control blocks to auto-export .raw binary vectors
                var finalNetlist = netlistContent + $"\n.control\nset filetype=ascii\nrun\nwrite {rawFileName}\n.endc\n";
                await File.WriteAllTextAsync(cirFileName, finalNetlist, cancellationToken);

                var startInfo = new ProcessStartInfo
                {
                    FileName = _resolvedNgSpicePath!,
                    Arguments = $"-b \"{cirFileName}\"", // -b indicates batch mode without GUI execution
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

                // Catch scenarios where the kernel isn't installed yet
                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Failed to spin up SPICE Kernel at '{_resolvedNgSpicePath}'. Ensure the binary exists natively inside the Environment path or execution directory. \nDetails: {ex.Message}";
                    return result;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                result.OutputLog = outputBuilder.ToString();
                string fullLogLower = result.OutputLog.ToLower();
                result.ErrorMessage = errorBuilder.ToString();

                // Advanced SPICE Error Routing
                // Ngspice dumps harmless warnings to stderr, but critical math failures to stdout
                bool hasFatalKeywords = fullLogLower.Contains("fatal") || 
                                        fullLogLower.Contains("aborted") || 
                                        fullLogLower.Contains("singular matrix") ||
                                        fullLogLower.Contains("timestep too small") ||
                                        fullLogLower.Contains("unknown device");

                if (process.ExitCode != 0 || hasFatalKeywords)
                {
                    result.Success = false;
                    
                    // Route the most critical lines from the log to the UI error pipeline
                    var lines = result.OutputLog.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.ToLower().Contains("error") || line.ToLower().Contains("fatal"))
                        {
                            result.ErrorMessage += $"\n{line.Trim()}";
                        }
                    }

                    if (string.IsNullOrWhiteSpace(result.ErrorMessage))
                    {
                        result.ErrorMessage = "Simulation failed silently. Check the full output log for math convergence issues.";
                    }
                }
                else
                {
                    result.Success = true;
                }

                if (File.Exists(rawFileName))
                {
                    result.RawFilePath = rawFileName;
                }
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "Simulation mathematics aborted actively by the runtime user.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Fatal runtime simulation error: {ex.Message}";
            }
            finally
            {
                // Clean up source .cir explicitly but leave .raw for the extractor pipeline memory pass
                if (File.Exists(cirFileName)) File.Delete(cirFileName);
            }

            return result;
        }
    }
}
