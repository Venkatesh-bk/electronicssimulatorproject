using System;
using System.IO;

namespace EdaSimulator.Engines.Simulation
{
    /// <summary>
    /// Probes a prioritized list of common ngspice installation paths on Windows
    /// to locate the ngspice_con.exe batch-mode executable automatically.
    ///
    /// Search order (highest priority first):
    ///   1. User-configured path in AppSettings.NgSpicePath
    ///   2. Project-local resources\engines\ngspice\bin\ folder
    ///   3. Standard ngspice installer paths (Program Files)
    ///   4. Chocolatey / Scoop managed paths
    ///   5. System PATH environment variable
    /// </summary>
    public static class NgSpiceLocator
    {
        private const string ExeName = "ngspice_con.exe";

        /// <summary>
        /// Returns the fully-qualified path to ngspice_con.exe if found, or null.
        /// </summary>
        public static string? FindNgSpice(string? userConfiguredPath = null)
        {
            // 1. User-configured setting wins unconditionally
            if (!string.IsNullOrWhiteSpace(userConfiguredPath) && File.Exists(userConfiguredPath))
                return userConfiguredPath;

            // 2. Project-local vendored binary (place ngspice_con.exe here to use offline)
            string projectLocal = GetProjectLocalPath();
            if (File.Exists(projectLocal))
                return projectLocal;

            // 3. Standard Windows installer paths
            foreach (var candidate in GetStandardWindowsPaths())
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            // 4. System PATH
            string? onPath = FindOnSystemPath();
            if (onPath != null)
                return onPath;

            return null;
        }

        /// <summary>
        /// Returns the project-local engine folder path.
        /// Convention: resources\engines\ngspice\[Spice64\]bin\ngspice_con.exe
        /// relative to the solution root.
        /// </summary>
        public static string GetProjectLocalPath()
        {
            string? asmDir = Path.GetDirectoryName(
                typeof(NgSpiceLocator).Assembly.Location);

            if (asmDir == null) return string.Empty;

            string candidate = asmDir;
            for (int i = 0; i < 8; i++)
            {
                // Probe 1: flat layout  resources/engines/ngspice/bin/
                string flat = Path.Combine(candidate, "resources", "engines", "ngspice", "bin", ExeName);
                if (File.Exists(flat)) return flat;

                // Probe 2: Spice64 subdirectory layout produced by the official 7z archive
                string spice64 = Path.Combine(candidate, "resources", "engines", "ngspice", "Spice64", "bin", ExeName);
                if (File.Exists(spice64)) return spice64;

                string parent = Path.GetDirectoryName(candidate) ?? string.Empty;
                if (string.IsNullOrEmpty(parent) || parent == candidate) break;
                candidate = parent;
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the recommended local vendor path where the user should place ngspice.
        /// </summary>
        public static string GetRecommendedVendorPath()
        {
            // Walk to project root
            string? asmDir = Path.GetDirectoryName(typeof(NgSpiceLocator).Assembly.Location);
            if (asmDir == null) return string.Empty;

            string candidate = asmDir;
            for (int i = 0; i < 6; i++)
            {
                if (File.Exists(Path.Combine(candidate, "EdaSimulator.sln")))
                    return Path.Combine(candidate, "resources", "engines", "ngspice", "bin", ExeName);

                string parent = Path.GetDirectoryName(candidate) ?? string.Empty;
                if (string.IsNullOrEmpty(parent) || parent == candidate) break;
                candidate = parent;
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EdaSimulator", "engines", "ngspice", "bin", ExeName);
        }

        private static string[] GetStandardWindowsPaths() => new[]
        {
            // Confirmed project-local ngspice-46 vendor path (extracted from ngspice-46_64.7z)
            @"D:\electronicssimulatorproject\resources\engines\ngspice\Spice64\bin\ngspice_con.exe",
            // Standard installer locations across ngspice versions 38-46
            @"C:\Program Files\Spice64\bin\ngspice_con.exe",
            @"C:\Program Files\Spice64_exe\bin\ngspice_con.exe",
            @"C:\Program Files (x86)\Spice\bin\ngspice_con.exe",
            @"C:\Program Files\ngspice\bin\ngspice_con.exe",
            @"C:\ngspice\bin\ngspice_con.exe",
            // Chocolatey
            @"C:\ProgramData\chocolatey\bin\ngspice_con.exe",
            @"C:\ProgramData\chocolatey\lib\ngspice\bin\ngspice_con.exe",
            // Scoop
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @"scoop\apps\ngspice\current\bin\ngspice_con.exe"),
        };

        private static string? FindOnSystemPath()
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv == null) return null;

            foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string full = Path.Combine(dir.Trim(), ExeName);
                if (File.Exists(full)) return full;
            }
            return null;
        }
    }
}
