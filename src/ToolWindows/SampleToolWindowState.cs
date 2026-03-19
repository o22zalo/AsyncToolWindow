using AsyncToolWindowSample.Services;

namespace AsyncToolWindowSample.ToolWindows
{
    /// <summary>
    /// State object passed from <see cref="MyPackage.InitializeToolWindowAsync"/>
    /// into <see cref="SampleToolWindow"/> constructor.
    /// Carries DTE and the two new services.
    /// </summary>
    public class SampleToolWindowState
    {
        public EnvDTE80.DTE2 DTE { get; set; }

        /// <summary>Output Window pane managed by this extension.</summary>
        public OutputWindowService OutputWindow { get; set; }

        /// <summary>Status bar wrapper.</summary>
        public StatusBarService StatusBar { get; set; }
    }
}
