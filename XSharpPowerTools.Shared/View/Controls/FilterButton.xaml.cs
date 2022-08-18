using System.Windows;
using System.Windows.Controls;

namespace XSharpPowerTools.View.Controls
{
    /// <summary>
    /// Interaction logic for FilterButton.xaml
    /// </summary>
    public partial class FilterButton : UserControl
    {
        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent("Click", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(FilterButton));
        public static DependencyProperty ButtonTextProperty = DependencyProperty.Register("ButtonText", typeof(string), typeof(FilterButton));
        public static DependencyProperty PopupTextProperty = DependencyProperty.Register("PopupText", typeof(string), typeof(FilterButton));

        public event RoutedEventHandler Click
        {
            add { AddHandler(ClickEvent, value); }
            remove { RemoveHandler(ClickEvent, value); }
        }

        public string ButtonText
        {
            get => (string)GetValue(ButtonTextProperty);
            set => SetValue(ButtonTextProperty, value);
        }

        public string PopupText
        {
            get => (string)GetValue(PopupTextProperty);
            set => SetValue(PopupTextProperty, value);
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
    
        public void ShowPopup() => 
            HotkeyPopup.IsOpen = true;

        public void HidePopup() => 
            HotkeyPopup.IsOpen = false;
    }
}
