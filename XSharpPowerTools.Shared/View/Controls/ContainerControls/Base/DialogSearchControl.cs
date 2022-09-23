using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Reactive.Linq;
using System.Windows;
using Task = System.Threading.Tasks.Task;

namespace XSharpPowerTools.View.Controls
{
    public abstract class DialogSearchControl : BaseSearchControl
    {
        protected abstract SearchTextBox SearchTextBox { get; }
        protected bool AllowReturn;

        public string SearchTerm
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value) && SearchTextBox != null)
                    SearchTextBox.Text = value;
            }
        }

        public DialogSearchControl(DialogWindow parentWindow) : base(parentWindow)
        {
            Loaded += DialogSearchControl_Loaded;
        }

        protected virtual void InitializeSearchTextBox() =>
            SearchTextBox?.WhenTextChanged
                .Throttle(TimeSpan.FromMilliseconds(500))
                .Subscribe(x => OnTextChanged());

        protected virtual void OnTextChanged() =>
            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await DoSearchAsync()).FileAndForget($"{FileReference}OnTextChanged");

        protected async Task DoSearchAsync()
        {
            await XSharpPowerToolsPackage.Instance.JoinableTaskFactory.SwitchToMainThreadAsync();
            await SearchAsync();
        }

        private void DialogSearchControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (SearchTextBox == null)
                return;

            XSharpPowerToolsPackage.Instance.JoinableTaskFactory.RunAsync(async () => await SearchAsync()).FileAndForget($"{FileReference}Control_Loaded");
            SearchTextBox.CaretIndex = int.MaxValue;
            try
            {
                SearchTextBox.Focus();
                SearchTextBox.SelectAll();
            }
            catch (Exception)
            { }
        }
    }
}
