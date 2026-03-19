using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample.Services
{
    /// <summary>
    /// Manages a custom Output Window pane for this extension.
    /// Wraps IVsOutputWindow / IVsOutputWindowPane so callers never
    /// touch raw COM interfaces directly.
    /// </summary>
    public sealed class OutputWindowService
    {
        // Stable GUID for this extension's Output pane – generate your own per project.
        public static readonly Guid PaneGuid =
            new Guid("3A8F7B2C-91D4-4E6A-B05C-2D1F9E3A4C78");

        private const string PaneTitle = "Async Tool Window Sample";

        private IVsOutputWindowPane _pane;
        private readonly AsyncPackage _package;

        public OutputWindowService(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        // ------------------------------------------------------------------ //
        //  Initialization (call once, on background thread is fine)           //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Creates (or retrieves) the custom Output pane.
        /// Safe to call from a background thread – switches to UI thread internally.
        /// </summary>
        public async Task InitializeAsync()
        {
            // IVsOutputWindow must be obtained on the UI thread
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var outputWindow =
                await _package.GetServiceAsync(typeof(SVsOutputWindow))
                as IVsOutputWindow;

            if (outputWindow == null)
                return;

            var guid = PaneGuid;

            // Try to get an already-existing pane first (e.g. after package reload)
            outputWindow.GetPane(ref guid, out _pane);

            if (_pane == null)
            {
                // fInitVisible  = 1 : show pane immediately
                // fClearWithSolution = 0 : keep content when solution closes
                outputWindow.CreatePane(ref guid, PaneTitle,
                    fInitVisible: 1, fClearWithSolution: 0);

                outputWindow.GetPane(ref guid, out _pane);
            }
        }

        // ------------------------------------------------------------------ //
        //  Public API                                                          //
        // ------------------------------------------------------------------ //

        /// <summary>Writes a line of text (appends \n automatically).</summary>
        public void WriteLine(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.OutputString(message + "\n");
        }

        /// <summary>Writes a timestamped line – useful for log-style output.</summary>
        public void Log(string message)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.OutputString($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }

        /// <summary>Brings this pane to the foreground in the Output window.</summary>
        public void Activate()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.Activate();
        }

        /// <summary>Removes all text from the pane.</summary>
        public void Clear()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _pane?.Clear();
        }
    }
}
