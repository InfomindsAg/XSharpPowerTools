using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.Helpers
{
    /// <summary>
    /// Reports unexpected errors to both diagnostic channels Visual Studio offers: the Extensions pane
    /// of the output window, which needs no preparation but is gone once VS closes, and the activity
    /// log, which survives on disk but is only written when VS was started with /log.
    /// </summary>
    internal static class DiagnosticLog
    {
        const string Source = "XSharpPowerTools";
        const string FileReference = "vs/XSharpPowerTools/DiagnosticLog/";

        public static void Error(string context, Exception exception)
        {
            if (exception == null)
                return;

            var joinableTaskFactory = XSharpPowerToolsPackage.Instance?.JoinableTaskFactory;
            if (joinableTaskFactory == null)
                return;

            joinableTaskFactory
                .RunAsync(async () => await ErrorAsync(context, exception))
                .FileAndForget($"{FileReference}Error");
        }

        public static async Task ErrorAsync(string context, Exception exception)
        {
            if (exception == null)
                return;

            // logging must never become a failure of its own, so every channel is guarded separately
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ActivityLog.TryLogError(Source, $"{context}: {exception}");
            }
            catch (Exception)
            { }

            try
            {
                await exception.LogAsync(context);
            }
            catch (Exception)
            { }
        }
    }

    internal static class JoinableTaskExtensions
    {
        /// <summary>
        /// Drop-in replacement for FileAndForget, which only reports faults to telemetry and therefore
        /// leaves nothing behind that a developer or a user can look at.
        /// </summary>
        public static void LogAndForget(this JoinableTask joinableTask, string faultEventName)
        {
            _ = joinableTask.Task.ContinueWith
            (
                task => DiagnosticLog.Error(faultEventName, task.Exception?.Flatten()),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );

            joinableTask.FileAndForget(faultEventName);
        }
    }
}
