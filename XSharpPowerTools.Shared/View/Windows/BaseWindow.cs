using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Windows.Input;
using XSharpPowerTools.Helpers;

namespace XSharpPowerTools.View.Windows
{
    public abstract class BaseWindow : DialogWindow
    {
        public XSModel XSModel { get; set; }
        public abstract string SearchTerm { set; }
        protected bool AllowReturn;

        public BaseWindow()
        {
            Community.VisualStudio.Toolkit.Themes.SetUseVsTheme(this, true);
            PreviewKeyDown += BaseWindow_PreviewKeyDown;
        }

        private void BaseWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        protected abstract void OnTextChanged();
    }
}
