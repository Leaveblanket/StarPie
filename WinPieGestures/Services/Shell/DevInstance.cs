using System;

namespace WinPieGestures.Services.Shell
{
    /// <summary>
    /// Marks instances launched with the "--dev" flag so they can run side-by-side
    /// with the installed release: isolated config folder, separate single-instance
    /// mutex, middle-button gesture trigger, and no writes to the real autostart
    /// registry entry.
    /// </summary>
    public static class DevInstance
    {
        private const string Flag = "--dev";

        /// <summary>Visible marker appended to window titles and tray tooltip.</summary>
        public static string Suffix => IsActive ? " (Dev)" : string.Empty;

        /// <summary>Dev instances sandbox into their own config folder; release keeps the default.</summary>
        public static string FolderName => IsActive ? "StarPie-Dev" : "StarPie";

        /// <summary>
        /// Single-instance mutex per flavor: a dev instance and the release instance can
        /// run simultaneously, while duplicates of the same flavor stay mutually exclusive.
        /// </summary>
        public static string MutexName => IsActive
            ? @"Global\StarPie_DevInstance_Mutex_9B8A7D"
            : @"Global\StarPie_SingleInstance_Mutex_9B8A7C";

        public static bool IsActive =>
            Environment.CommandLine.Contains(Flag, StringComparison.OrdinalIgnoreCase);
    }
}
