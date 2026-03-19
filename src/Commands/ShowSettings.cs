using System;
using System.ComponentModel.Design;
using AsyncToolWindowSample.ToolWindows;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample
{
    /// <summary>
    /// Lệnh mở Settings dialog từ Tools menu.
    /// Đăng ký trong VSCommandTable.vsct với ID CmdIdSettings (0x0400).
    /// Hiện ở Tools > "Async Tool Window Sample Settings..."
    /// </summary>
    internal sealed class ShowSettings
    {
        private readonly AsyncPackage _package;

        private ShowSettings(AsyncPackage package)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var cs = await package.GetServiceAsync(typeof(IMenuCommandService))
                     as OleMenuCommandService;
            if (cs == null) return;

            var cmdId = new CommandID(PackageGuids.CommandSetGuid, PackageIds.CmdIdSettings);
            var cmd   = new MenuCommand(new ShowSettings(package).Execute, cmdId);
            cs.AddCommand(cmd);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var pkg = (MyPackage)_package;

            var dlg = new SettingsDialog(
                pkg.Config,
                pkg.OutputWindow,
                pkg.StatusBar);

            // ShowDialog() — modal, blocks until Save or Cancel
            dlg.ShowDialog();
        }
    }
}
