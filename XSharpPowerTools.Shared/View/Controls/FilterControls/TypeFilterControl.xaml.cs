using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace XSharpPowerTools.View.Controls
{
    /// <summary>
    /// Interaction logic for TypeFilterControl.xaml
    /// </summary>
    public partial class TypeFilterControl : FilterControl<TypeFilter>
    {
        public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent("Click", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TypeFilterControl));

        public override event RoutedEventHandler Click
        {
            add { base.AddHandler(ClickEvent, value); }
            remove { base.RemoveHandler(ClickEvent, value); }
        }

        public TypeFilterControl() : base()
        {
            InitializeComponent();
            InitializeDictionaries();
        }

        protected override void InitializeDictionaries()
        {
            FilterButtons = new Dictionary<FilterButton, TypeFilter>
            {
                { ClassFilterButton, TypeFilter.Class },
                { EnumFilterButton, TypeFilter.Enum },
                { InterfaceFilterButton, TypeFilter.Interface },
                { StructFilterButton, TypeFilter.Struct }
            };

            HotKeys = new Dictionary<Key, FilterButton>
            {
                { Key.D7, ClassFilterButton },
                { Key.D8, EnumFilterButton },
                { Key.D9, InterfaceFilterButton },
                { Key.D0, StructFilterButton },
                { Key.NumPad7, ClassFilterButton },
                { Key.NumPad8, EnumFilterButton },
                { Key.NumPad9, InterfaceFilterButton },
                { Key.NumPad0, StructFilterButton }
            };
        }

        protected void FilterButton_Click(object sender, RoutedEventArgs e) =>
            RaiseClickEvent(ClickEvent);
    }
}
