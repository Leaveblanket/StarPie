namespace WinPieGestures.Services
{
    /// <summary>
    /// Config seam (ADR-0002): loading and saving config.json plus profile lookup
    /// (foreground-process match with Global-profile fallback). The implementation
    /// owns the file I/O; callers receive it via constructor injection.
    /// Import/Export stay on the concrete implementation until a consumer needs
    /// them through this seam.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>Current live config; defaults when loading failed or never ran — never null.</summary>
        AppConfig Current { get; }

        /// <summary>Loads from disk; a missing file is seeded with the default config, corrupt JSON falls back to defaults.</summary>
        void Load();

        /// <summary>Writes the current config back to disk; failures are silent (Debug output), never thrown.</summary>
        void Save();

        /// <summary>Returns the profile for the foreground process; empty/unknown process names fall back to the Global profile.</summary>
        WheelProfile GetProfileForProcess(string processName);

        /// <summary>Returns the Global profile; recreates an empty one at the front of Profiles when missing.</summary>
        WheelProfile GetGlobalProfile();
    }
}
