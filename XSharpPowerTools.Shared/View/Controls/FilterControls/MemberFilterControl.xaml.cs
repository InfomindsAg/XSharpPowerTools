using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace XSharpPowerTools.View.Controls
{
    /// <summary>
    /// Interaction logic for MemberFilterControl.xaml
    /// </summary>
    public partial class MemberFilterControl : FilterControl<MemberFilter>
    {
        public enum DisplayMode 
        { 
            Default,
            ContainedInType,
            GlobalScope
        }

        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent("Click", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MemberFilterControl));
        public static DependencyProperty ModeProperty = DependencyProperty.Register("Mode", typeof(DisplayMode), typeof(MemberFilterControl), new PropertyMetadata(new PropertyChangedCallback(MemberFilterControl.OnModeChanged)));

        public override event RoutedEventHandler Click
        {
            add { AddHandler(ClickEvent, value); }
            remove { RemoveHandler(ClickEvent, value); }
        }

        public DisplayMode Mode
        {
            get { return (DisplayMode)GetValue(ModeProperty); }
            set { SetValue(ModeProperty, value); }
        }

        private static void OnModeChanged(DependencyObject control, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = control as MemberFilterControl;
            switch ((DisplayMode)e.NewValue)
            {
                case DisplayMode.Default:
                {
                    ctrl.MethodFilterButton.Visibility = Visibility.Visible;
                    ctrl.PropertyFilterButton.Visibility = Visibility.Visible;
                    ctrl.FunctionFilterButton.Visibility = Visibility.Visible;
                    ctrl.VariableFilterButton.Visibility = Visibility.Visible;
                    ctrl.DefineFilterButton.Visibility = Visibility.Visible;
                    ctrl.EnumValueFilterButton.Visibility = Visibility.Visible;
                    break;
                }
                case DisplayMode.ContainedInType:
                {
                    ctrl.MethodFilterButton.Visibility = Visibility.Visible;
                    ctrl.PropertyFilterButton.Visibility = Visibility.Visible;
                    ctrl.FunctionFilterButton.Visibility = Visibility.Collapsed;
                    ctrl.VariableFilterButton.Visibility = Visibility.Visible;
                    ctrl.DefineFilterButton.Visibility = Visibility.Collapsed;
                    ctrl.EnumValueFilterButton.Visibility = Visibility.Visible;
                    break;
                }
                case DisplayMode.GlobalScope:
                {
                    ctrl.MethodFilterButton.Visibility = Visibility.Collapsed;
                    ctrl.PropertyFilterButton.Visibility = Visibility.Collapsed;
                    ctrl.FunctionFilterButton.Visibility = Visibility.Visible;
                    ctrl.VariableFilterButton.Visibility = Visibility.Collapsed;
                    ctrl.DefineFilterButton.Visibility = Visibility.Visible;
                    ctrl.EnumValueFilterButton.Visibility = Visibility.Collapsed;
                    break;
                }
            }
        }

        public MemberFilterControl() : base()
        {
            InitializeComponent();
            InitializeDictionaries();
        }

        protected override void InitializeDictionaries()
        {
            FilterButtons = new Dictionary<FilterButton, MemberFilter>
            {
                { MethodFilterButton, MemberFilter.Method },
                { PropertyFilterButton, MemberFilter.Property },
                { FunctionFilterButton, MemberFilter.Function },
                { VariableFilterButton, MemberFilter.Variable },
                { DefineFilterButton, MemberFilter.Define },
                { EnumValueFilterButton, MemberFilter.EnumValue }
            };

            HotKeys = new Dictionary<Key, FilterButton>
            {
                { Key.D1, MethodFilterButton },
                { Key.D2, PropertyFilterButton },
                { Key.D3, FunctionFilterButton },
                { Key.D4, VariableFilterButton },
                { Key.D5, DefineFilterButton },
                { Key.D6, EnumValueFilterButton },
                { Key.NumPad1, MethodFilterButton },
                { Key.NumPad2, PropertyFilterButton },
                { Key.NumPad3, FunctionFilterButton },
                { Key.NumPad4, VariableFilterButton },
                { Key.NumPad5, DefineFilterButton },
                { Key.NumPad6, EnumValueFilterButton }
            };
        }

        protected void FilterButton_Click(object sender, RoutedEventArgs e) =>
            RaiseClickEvent(ClickEvent);
    }
}
