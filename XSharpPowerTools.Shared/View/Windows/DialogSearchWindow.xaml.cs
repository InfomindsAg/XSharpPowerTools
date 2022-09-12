using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.PlatformUI;
using System.Windows.Input;
using XSharpPowerTools.View.Controls;

namespace XSharpPowerTools.View.Windows
{
    /// <summary>
    /// Interaction logic for DialogSearchWindow.xaml
    /// </summary>
    public partial class DialogSearchWindow : DialogWindow
    {
        protected DialogSearchControl SearchControl;

        public string SearchTerm
        {
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    SearchControl.SearchTerm = value;
            }
        }

        public XSModel XSModel
        {
            get => SearchControl.XSModel;
            set
            {
                if (value != null)
                    SearchControl.XSModel = value;
            }
        }

        public DialogSearchWindow()
        {
            Themes.SetUseVsTheme(this, true);
            InitializeComponent();
            PreviewKeyDown += Window_PreviewKeyDown;
        }

        public void ShowControl(DialogSearchControl searchControl)
        {
            SearchControl = searchControl;
            WindowBorder.Child = SearchControl;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
        }
    }
}