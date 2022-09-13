using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System.IO;
using XSharpPowerTools.View.Controls;
using XSharpPowerTools.View.Windows;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.Commands
{
    [Command(PackageIds.CodeSuggestionsCommand)]
    internal sealed class CodeSuggestionsCommand : BaseCommand<CodeSuggestionsCommand>
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
                var window = new DialogSearchWindow("X# Code Suggestions");
                var control = new CodeSuggestionsControl(window);
                window.ShowControl(control);
                await CommandBase.ShowDialogSearchWindowAsync(window);
            }
        }
    }
}
