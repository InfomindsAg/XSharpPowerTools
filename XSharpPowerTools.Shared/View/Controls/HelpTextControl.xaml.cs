using System.Windows.Controls;

namespace XSharpPowerTools.View.Controls
{
    /// <summary>
    /// Interaction logic for HelpTextControl.xaml
    /// </summary>
    public partial class HelpTextControl : UserControl
    {
        public HelpTextControl()
        {
            InitializeComponent();

            HelpTextLabel.Content =
@"example      - searches for classes with names similar to 'example'
ex1.ex2       - searches for members similar to 'ex2' within classes similar to 'ex1' (""."" equal to "":"")
.example     - searches for members 'example' within all classes
..example    - searches for members 'example' within current document
" + "\"example\"" + @"   - matches whole word only
ex*Model    - * is a placeholder for multiple characters
p example   - filtered search with prefixes, for more info see tooltips of filter buttons";
        }
    }
}
