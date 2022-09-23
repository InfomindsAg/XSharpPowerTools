using System.Windows.Controls;

namespace XSharpPowerTools.View.Controls
{
    public class CodeBrowserHelpText : HelpTextControl
    {
        const string HelpText =
@"example      - searches for classes with names similar to 'example'
ex1.ex2       - searches for members similar to 'ex2' within classes similar to 'ex1' (""."" equal to "":"")
.example     - searches for members 'example' within all classes
..example    - searches for members 'example' within current type
""example""   - matches whole word only
ex*Model    - * is a placeholder for multiple characters

Ctrl + 1,2,3,... to activate/deactivate filters";

        public CodeBrowserHelpText() : base(HelpText)
        { }
    }

    public class CodeSuggestionsHelpText : HelpTextControl
    {
        const string HelpText =
@"example      - searches for classes with names similar to 'example'
ex1.ex2       - searches for members similar to 'ex2' within classes with the exact name 'ex1' (""."" equal to "":"")
..example    - searches for members 'example' within current type
ex*Model    - * is a placeholder for multiple characters

Ctrl + 1,2,3,... to activate/deactivate filters
Ctrl + Return to search within members of selected type";

        public CodeSuggestionsHelpText() : base(HelpText)
        { }
    }

    /// <summary>
    /// Interaction logic for HelpTextControl.xaml
    /// </summary>
    public partial class HelpTextControl : UserControl
    {
        public HelpTextControl(string helpText)
        {
            InitializeComponent();
            HelpTextLabel.Content = helpText;
        }
    }
}
