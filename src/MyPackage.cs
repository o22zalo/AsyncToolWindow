using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AsyncToolWindowSample.Services;
using AsyncToolWindowSample.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Async Tool Window Sample",
        "Shows how to use an Async Tool Window in Visual Studio 15.6+", "1.0")]
    [ProvideToolWindow(typeof(SampleToolWindow),
        Style = VsDockStyle.Tabbed, DockedWidth = 300,
        Window = "DocumentWell", Orientation = ToolWindowOrientation.Left)]
    [Guid("6e3b2e95-902b-4385-a966-30c06ab3c7a6")]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class MyPackage : AsyncPackage
    {
        // --------------------------------------------------------------------- //
        //  Singleton accessors – use after InitializeAsync completes             //
        // --------------------------------------------------------------------- //

        /// <summary>Output Window pane service – available after package load.</summary>
        public OutputWindowService OutputWindow { get; private set; }

        /// <summary>Status bar service – available after package load.</summary>
        public StatusBarService StatusBar { get; private set; }

        // --------------------------------------------------------------------- //
        //  AsyncPackage lifecycle                                                //
        // --------------------------------------------------------------------- //

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            // ── 1. Initialize services on background thread where possible ──
            OutputWindow = new OutputWindowService(this);
            StatusBar    = new StatusBarService(this);

            // Both services switch to UI thread internally for COM calls
            await OutputWindow.InitializeAsync();
            await StatusBar.InitializeAsync();

            // ── 2. Switch to UI thread for VS shell operations ──
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await ShowToolWindow.InitializeAsync(this);

            // ── 3. Announce successful load ──
            OutputWindow.Log("AsyncToolWindowSample loaded successfully.");
            StatusBar.SetText("Async Tool Window Sample loaded.");
        }

        // --------------------------------------------------------------------- //
        //  Async Tool Window factory                                             //
        // --------------------------------------------------------------------- //

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            return toolWindowType.Equals(Guid.Parse(SampleToolWindow.WindowGuidString))
                ? this
                : null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            return toolWindowType == typeof(SampleToolWindow)
                ? SampleToolWindow.Title
                : base.GetToolWindowTitle(toolWindowType, id);
        }

        protected override async Task<object> InitializeToolWindowAsync(
            Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            // Runs on background thread – do expensive work here
            var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;

            return new SampleToolWindowState
            {
                DTE          = dte,
                OutputWindow = OutputWindow,
                StatusBar    = StatusBar
            };
        }
    }
}
