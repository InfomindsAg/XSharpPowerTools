﻿using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XSharpPowerTools.Helpers;
using XSharpPowerTools.View.Windows;
using File = System.IO.File;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.Commands
{
    public static class CommandBase
    {
        const string FileReference = "vs/XSharpPowerTools/CommandBase/";

        public static async Task ShowBaseWindowAsync(BaseWindow window)
        {
            var solution = await VS.Solutions.GetCurrentSolutionAsync();
            
            if (solution != null)
            {
                window.XSModel = new XSModel();
                window.SearchTerm = await DocumentHelper.GetEditorSearchTermAsync();
                try
                {
                    await VS.Windows.ShowDialogAsync(window);
                }
                finally
                {
                    window.Close();
                }
            }
            else
            {
                await VS.MessageBox.ShowWarningAsync("X# Code Browser", "X# Code Browser is only available with an opened solution.");
            }
        }

        public static void BeforeQueryStatus(object sender, EventArgs e)
        {
            if (sender is OleMenuCommand menuCommand)
                XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => menuCommand.Enabled = await ActiveSolutionContainsXsProjectAsync()).FileAndForget($"{FileReference}BeforeQueryStatus");
        }

        private static async Task<bool> ActiveSolutionContainsXsProjectAsync()
        {
            var solution = await VS.Solutions.GetCurrentSolutionAsync();
            return solution != null && await ChildrenContainXsProjectAsync(solution.Children);
        }

        private static async Task<bool> ChildrenContainXsProjectAsync(IEnumerable<SolutionItem> children)
        {
            foreach (var child in children)
            {
                if (child.Type == SolutionItemType.Project)
                {
                    var project = child as Project;
                    if (await project.IsKindAsync(XSharpPowerToolsPackage.XSharpProjectTypeGuid))
                        return true;
                }
                else if (child.Type == SolutionItemType.SolutionFolder || child.Type == SolutionItemType.PhysicalFolder)
                {
                    if (await ChildrenContainXsProjectAsync(child.Children))
                        return true;
                }
            }
            return false;
        }
    }
}
