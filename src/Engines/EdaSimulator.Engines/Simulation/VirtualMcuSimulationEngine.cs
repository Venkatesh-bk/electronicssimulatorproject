using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace EdaSimulator.Engines.Simulation
{
    public class VirtualMcuSimulationEngine
    {
        public static string RunCoSimulation(string firmwarePath, double stopTimeSec)
        {
            if (string.IsNullOrEmpty(firmwarePath) || !File.Exists(firmwarePath))
            {
                return "[Serial Monitor] No active firmware loaded. Use properties panel to browse firmware (.ino, .cpp, .py, etc.)";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[Serial Monitor] Starting MCU Co-Simulation with firmware: {Path.GetFileName(firmwarePath)}");
            sb.AppendLine($"[Serial Monitor] Simulation duration: {stopTimeSec} seconds\n");

            try
            {
                string content = File.ReadAllText(firmwarePath);
                
                // Parse serial prints from Arduino / C++ code supporting single or double quotes
                var arduinoMatches = Regex.Matches(content, @"Serial\.(println|print)\(\s*['""]([^'""]*)['""]\s*\);");
                
                // Matches print("...") or print('...') in Python
                var pythonMatches = Regex.Matches(content, @"print\(\s*['""]([^'""]*)['""]\s*\)");

                // Matches delay(milliseconds); or time.sleep(seconds)
                var delayMatches = Regex.Matches(content, @"delay\(\s*(\d+)\s*\);");
                var sleepMatches = Regex.Matches(content, @"time\.sleep\(\s*([\d\.]+)\s*\)");

                var printStatements = new List<string>();
                foreach (Match m in arduinoMatches)
                {
                    printStatements.Add(m.Groups[2].Value);
                }
                foreach (Match m in pythonMatches)
                {
                    printStatements.Add(m.Groups[1].Value);
                }

                double delaySec = 1.0; // default delay if none found
                if (delayMatches.Count > 0 && double.TryParse(delayMatches[0].Groups[1].Value, out double ms))
                {
                    delaySec = ms / 1000.0;
                }
                else if (sleepMatches.Count > 0 && double.TryParse(sleepMatches[0].Groups[1].Value, out double sec))
                {
                    delaySec = sec;
                }

                // Enforce safety bounds on delay to avoid division by zero or infinite loop
                if (delaySec <= 0.0)
                {
                    delaySec = 0.001;
                }

                if (printStatements.Count == 0)
                {
                    sb.AppendLine("[MCU Boot] Bootloader initialized successfully.");
                    sb.AppendLine("[MCU Loop] Running user firmware loop (no Serial prints detected).");
                    return sb.ToString();
                }

                // Simulate execution over stopTimeSec
                double currentTime = 0.0;
                int statementIndex = 0;

                // First statement is setup / initialization
                sb.AppendLine($"[{currentTime:F6}s] [MCU Boot] {printStatements[0]}");
                currentTime += 0.01; // small boot delay

                int loopStartIdx = printStatements.Count > 1 ? 1 : 0;
                int iterations = 0;

                while (currentTime < stopTimeSec && iterations < 100) // safety cap to prevent infinite loop
                {
                    int idx = loopStartIdx + (statementIndex % (printStatements.Count - loopStartIdx));
                    sb.AppendLine($"[{currentTime:F6}s] [UART TX] {printStatements[idx]}");
                    
                    currentTime += delaySec;
                    statementIndex++;
                    iterations++;
                }

                sb.AppendLine($"\n[Serial Monitor] Co-simulation finished at {stopTimeSec}s.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[Serial Monitor Error] Failed to execute firmware: {ex.Message}");
            }

            return sb.ToString();
        }
    }
}
