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
        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent("Click", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MemberFilterControl));

        public override event RoutedEventHandler Click
        {
            add { base.AddHandler(ClickEvent, value); }
            remove { base.RemoveHandler(ClickEvent, value); }
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
