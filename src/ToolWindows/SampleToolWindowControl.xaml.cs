using System;
using System.Windows;
using System.Windows.Controls;
using AsyncToolWindowSample.Services;
using Microsoft.VisualStudio.Shell;

namespace AsyncToolWindowSample.ToolWindows
{
    public partial class SampleToolWindowControl : UserControl
    {
        private readonly SampleToolWindowState _state;
        private OutputWindowService OutputWindow => _state.OutputWindow;
        private StatusBarService    StatusBar    => _state.StatusBar;

        public SampleToolWindowControl(SampleToolWindowState state)
        {
            _state = state;
            InitializeComponent();
        }

        // ------------------------------------------------------------------ //
        //  Original button                                                     //
        // ------------------------------------------------------------------ //

        private void Button_ShowVsLocation_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string location = _state.DTE?.FullName ?? "(DTE not available)";
            MessageBox.Show($"Visual Studio is located here:\n'{location}'",
                "VS Location", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ------------------------------------------------------------------ //
        //  Output Window buttons                                               //
        // ------------------------------------------------------------------ //

        private void Button_WriteOutput_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputWindow.Activate();   // bring pane to focus
            OutputWindow.Log($"Button clicked – VS path: {_state.DTE?.FullName ?? "N/A"}");
            OutputWindow.WriteLine("You can write arbitrary text here.");

            StatusBar.SetText("Written to Output Window.");
        }

        private void Button_ClearOutput_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            OutputWindow.Clear();
            OutputWindow.Log("Output pane cleared.");
            StatusBar.SetText("Output pane cleared.");
        }

        // ------------------------------------------------------------------ //
        //  Status Bar buttons                                                  //
        // ------------------------------------------------------------------ //

        private void Button_SetStatus_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            StatusBar.SetText($"Hello from Async Tool Window Sample – {DateTime.Now:T}");
            OutputWindow.Log("Status bar text updated.");
        }

        private void Button_Animate_Click(object sender, RoutedEventArgs e)
        {
            // Fire-and-forget via JoinableTaskFactory so we never block the UI thread
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                OutputWindow.Log("Starting 3-second animation…");

                await StatusBar.RunWithAnimationAsync(
                    async () => await System.Threading.Tasks.Task.Delay(3000),
                    "Processing… please wait");

                OutputWindow.Log("Animation finished.");
            });
        }

        private void Button_Progress_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                OutputWindow.Log("Starting progress bar demo…");

                uint cookie = 0;
                const uint total = 5;

                for (uint i = 1; i <= total; i++)
                {
                    StatusBar.ReportProgress(ref cookie, "Demo progress", i, total);
                    OutputWindow.Log($"  Step {i}/{total}");
                    await System.Threading.Tasks.Task.Delay(600);
                }

                StatusBar.ClearProgress(ref cookie);
                StatusBar.SetText("Progress complete.");
                OutputWindow.Log("Progress bar demo finished.");
            });
        }
    }
}
