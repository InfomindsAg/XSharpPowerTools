using Microsoft.VisualStudio.PlatformUI;
using System;
using System.Linq;
using System.Windows.Input;
using XSharpPowerTools.Helpers;

namespace XSharpPowerTools.View.Windows
{
    public abstract class BaseWindow : DialogWindow
    {
        public XSModel XSModel { get; set; }
        public abstract string SearchTerm { set; }
        protected bool AllowReturn;

        public BaseWindow() => 
            PreviewKeyDown += BaseWindow_PreviewKeyDown;


        private void BaseWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            try
            {
                //var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(q => q.GetType("XSharpModel.XDatabase", false) != null);
                //var obj = assembly?.GetType("XSharpModel.XDatabase");
                //// get the static field with reflection from the type obj
                //var field = obj?.GetField("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                

            }
            catch (InvalidOperationException)
            { }
        }

        protected abstract void OnTextChanged();
    }
}
