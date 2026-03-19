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
    [ProvideToolWindow(typeof(ConfigEditorWindow),
        Style = VsDockStyle.Tabbed, DockedWidth = 480,
        Window = "DocumentWell", Orientation = ToolWindowOrientation.Right)]
    [Guid(PackageGuids.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(SampleOptionsPage),
        "Async Tool Window Sample", "General",
        categoryResourceID: 0, pageNameResourceID: 0,
        supportsAutomation: true)]
    public sealed class MyPackage : AsyncPackage
    {
        public OutputWindowService   OutputWindow   { get; private set; }
        public StatusBarService      StatusBar      { get; private set; }
        public SelectionService      Selection      { get; private set; }
        public DocumentService       Document       { get; private set; }
        public ProjectService        Project        { get; private set; }
        public EventService          Events         { get; private set; }
        public OptionsService        Options        { get; private set; }
        public MenuService           Menu           { get; private set; }
        public ToolbarService        Toolbar        { get; private set; }
        public ConfigurationService  Config         { get; private set; }

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            // ── Construct services ────────────────────────────────────────
            OutputWindow = new OutputWindowService(this);
            StatusBar    = new StatusBarService(this);
            Selection    = new SelectionService(this);
            Document     = new DocumentService(this);
            Project      = new ProjectService(this);
            Options      = new OptionsService(this);
            Config       = new ConfigurationService(this, OutputWindow);
            Events       = new EventService(this, OutputWindow);
            Toolbar      = new ToolbarService(this, OutputWindow);
            Menu         = new MenuService(this);

            // ── Async init ────────────────────────────────────────────────
            await OutputWindow.InitializeAsync();
            await StatusBar.InitializeAsync();
            await Selection.InitializeAsync();
            await Config.InitializeAsync();

            // ── Switch to UI thread ───────────────────────────────────────
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await ShowToolWindow.InitializeAsync(this);
            await Menu.InitializeAsync();
            await ShowConfigEditor.InitializeAsync(this);

            // §Settings: đăng ký lệnh mở Settings dialog từ Tools menu
            await ShowSettings.InitializeAsync(this);

            OutputWindow.Log("AsyncToolWindowSample loaded successfully.");
            StatusBar.SetText("Async Tool Window Sample loaded.");
        }

        public override IVsAsyncToolWindowFactory GetAsyncToolWindowFactory(Guid toolWindowType)
        {
            if (toolWindowType.Equals(Guid.Parse(SampleToolWindow.WindowGuidString)))
                return this;
            if (toolWindowType.Equals(Guid.Parse(ConfigEditorWindow.WindowGuidString)))
                return this;
            return null;
        }

        protected override string GetToolWindowTitle(Type toolWindowType, int id)
        {
            if (toolWindowType == typeof(SampleToolWindow))   return SampleToolWindow.Title;
            if (toolWindowType == typeof(ConfigEditorWindow)) return ConfigEditorWindow.Title;
            return base.GetToolWindowTitle(toolWindowType, id);
        }

        protected override async Task<object> InitializeToolWindowAsync(
            Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;

            if (toolWindowType == typeof(SampleToolWindow))
            {
                return new SampleToolWindowState
                {
                    DTE          = dte,
                    OutputWindow = OutputWindow,
                    StatusBar    = StatusBar,
                    Selection    = Selection,
                    Document     = Document,
                    Project      = Project,
                    Events       = Events,
                    Options      = Options,
                    Menu         = Menu,
                    Toolbar      = Toolbar,
                    Config       = Config
                };
            }

            if (toolWindowType == typeof(ConfigEditorWindow))
            {
                return new ConfigEditorState
                {
                    Config       = Config,
                    OutputWindow = OutputWindow,
                    StatusBar    = StatusBar
                };
            }

            return null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Events?.Dispose();
            base.Dispose(disposing);
        }
    }
}
