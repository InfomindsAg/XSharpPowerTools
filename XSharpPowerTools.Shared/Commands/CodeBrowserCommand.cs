using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.IO;
using XSharpPowerTools.View.Controls;
using XSharpPowerTools.View.Windows;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.Commands
{
    [Command(PackageIds.CodeBrowserCommand)]
    internal sealed class CodeBrowserCommand : BaseCommand<CodeBrowserCommand>
    {
        protected override async Task InitializeCompletedAsync()
        {
            await base.InitializeCompletedAsync();
            Command.BeforeQueryStatus += CommandBase.BeforeQueryStatus;
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            var solution = await VS.Solutions.GetCurrentSolutionAsync();
            if (solution != null)
            {
                var solutionDirectory = Path.GetDirectoryName(solution.FullPath);
                var window = new DialogSearchWindow();
                var control = new CodeBrowserControl(solutionDirectory, window);
                window.ShowControl(control);
                await CommandBase.ShowDialogSearchWindowAsync(window);
            }
        }
    }
}
