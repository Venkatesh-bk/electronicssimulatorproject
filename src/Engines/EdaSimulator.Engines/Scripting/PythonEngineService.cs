using System;
using System.Linq;
using Python.Runtime;
using EdaSimulator.Engines.Models;

namespace EdaSimulator.Engines.Scripting
{
    /// <summary>
    /// Manages the embedded CPython runtime using pythonnet.
    /// Provides the ability to run MATLAB/Python-style scripts that interact natively with the C# EDA engine.
    /// </summary>
    public class PythonEngineService
    {
        private static bool _isInitialized = false;

        /// <summary>
        /// Initializes the Python runtime. Must be called once before executing scripts.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            // Locate the .venv directory in the project root
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "..", ".."));
            string venvPath = System.IO.Path.Combine(projectRoot, ".venv");

            if (System.IO.Directory.Exists(venvPath))
            {
                // Explicitly route Python to the virtual environment
                Environment.SetEnvironmentVariable("PYTHONHOME", venvPath, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("PYTHONPATH", System.IO.Path.Combine(venvPath, "Lib", "site-packages"), EnvironmentVariableTarget.Process);
            }

            // Sanitize PATH to remove missing directories. Cupy crashes on Windows if PATH contains dead links.
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            var validPaths = pathEnv.Split(System.IO.Path.PathSeparator)
                                    .Where(p => !string.IsNullOrWhiteSpace(p) && System.IO.Directory.Exists(p));
            Environment.SetEnvironmentVariable("PATH", string.Join(System.IO.Path.PathSeparator.ToString(), validPaths), EnvironmentVariableTarget.Process);

            PythonEngine.Initialize();
            _isInitialized = true;
        }

        /// <summary>
        /// Shuts down the Python runtime cleanly.
        /// </summary>
        public static void Shutdown()
        {
            if (_isInitialized)
            {
                PythonEngine.Shutdown();
                _isInitialized = false;
            }
        }

        /// <summary>
        /// Executes a raw Python script string within the embedded runtime.
        /// The script is provided a global variable 'circuit' pointing to the currently active Schematic.
        /// </summary>
        /// <param name="script">The Python code to execute.</param>
        /// <param name="activeSchematic">The C# schematic object to expose to the script.</param>
        /// <returns>Standard output (stdout) captured from the script execution.</returns>
        public static string ExecuteScript(string script, Schematic activeSchematic)
        {
            if (!_isInitialized)
                Initialize();

            // To capture python's print() statements
            string output = string.Empty;

            using (Py.GIL()) // Acquire the Global Interpreter Lock
            {
                using (var scope = Py.CreateScope())
                {
                    try
                    {
                        // Expose the C# Schematic object natively to Python as a global variable
                        scope.Set("circuit", activeSchematic.ToPython());

                        // Redirect stdout to a StringWriter-like mechanism in Python
                        string redirectCode = @"
import sys
import io
sys.stdout = io.StringIO()
";
                        scope.Exec(redirectCode);

                        // Execute user script
                        scope.Exec(script);

                        // Extract output
                        dynamic sys = Py.Import("sys");
                        output = sys.stdout.getvalue().ToString();
                    }
                    catch (PythonException ex)
                    {
                        output += $"\n[Python Error] {ex.Message}";
                    }
                }
            }

            return output;
        }
    }
}
