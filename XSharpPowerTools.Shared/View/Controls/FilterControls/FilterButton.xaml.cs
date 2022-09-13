using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace XSharpPowerTools.View.Controls
{
    /// <summary>
    /// Interaction logic for FilterButton.xaml
    /// </summary>
    [ContentProperty("ButtonContent")]
    public partial class FilterButton : UserControl
    {
        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent("Click", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilterButton));
        public static DependencyProperty ButtonContentProperty = DependencyProperty.Register("ButtonContent", typeof(object), typeof(FilterButton));
        public static DependencyProperty PopupTextProperty = DependencyProperty.Register("PopupText", typeof(string), typeof(FilterButton));
        public static DependencyProperty HotkeyTextProperty = DependencyProperty.Register("HotkeyText", typeof(string), typeof(FilterButton));

        public event RoutedEventHandler Click
        {
            add { AddHandler(ClickEvent, value); }
            remove { RemoveHandler(ClickEvent, value); }
        }

        public object ButtonContent
        {
            get => GetValue(ButtonContentProperty);
            set => SetValue(ButtonContentProperty, value);
        }

        public string PopupText
        {
            get => (string)GetValue(PopupTextProperty);
            set => SetValue(PopupTextProperty, value);
        }

        public string HotkeyText
        {
            get => (string)GetValue(HotkeyTextProperty);
            set => SetValue(HotkeyTextProperty, value);
        }

        public bool? IsChecked
        {
            get => FilterToggleButton.IsChecked;
            set => FilterToggleButton.IsChecked = value;
        }

        public FilterButton()
        {
            InitializeComponent();
            DataContext = this;
        }

        protected void RaiseClickEvent()
        {
            var newEventArgs = new RoutedEventArgs(FilterButton.ClickEvent);
            RaiseEvent(newEventArgs);
        }

        protected void FilterButton_Click(object sender, RoutedEventArgs e) =>
            RaiseClickEvent();

        public void ShowPopup()
        {
            if (Visibility == Visibility.Visible)
                HotkeyPopup.IsOpen = true;
        }

        public void HidePopup() =>
            HotkeyPopup.IsOpen = false;
    }
}
