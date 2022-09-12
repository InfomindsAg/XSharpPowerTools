using Microsoft.VisualStudio.PlatformUI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using XSharpPowerTools.Helpers;
using XSharpPowerTools.View.Controls;
using static Microsoft.VisualStudio.Shell.VsTaskLibraryHelper;

namespace XSharpPowerTools.View.Windows
{
    /// <summary>
    /// Interaction logic for FindNamespaceWindow.xaml
    /// </summary>
    public partial class FindNamespaceWindow : BaseWindow
    {
        readonly DialogSearchControl SearchControl;

        public override string SearchTerm
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    SearchControl.SearchTerm = value;
            }
        }

        public override XSModel XSModel
        {
            get => SearchControl.XSModel;
            set
            {
                if (value != null)
                    SearchControl.XSModel = value;
            }
        }

        public FindNamespaceWindow() : base()
        {
            InitializeComponent();
            SearchControl = new FindNamespaceControl(this);
            WindowBorder.Child = SearchControl;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
