using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XSharpPowerTools.View.Controls
{
    public abstract class FilterControl<T> : UserControl where T : Enum
    {
        public abstract event RoutedEventHandler Click;

        protected Dictionary<FilterButton, T> FilterButtons;
        protected Dictionary<Key, FilterButton> HotKeys;

        protected abstract void InitializeDictionaries();

        public FilterControl()
        {
            Community.VisualStudio.Toolkit.Themes.SetUseVsTheme(this, true);
            DataContext = this;
        }

        protected void RaiseClickEvent(RoutedEvent clickEvent)
        {
            var newEventArgs = new RoutedEventArgs(clickEvent);
            RaiseEvent(newEventArgs);
        }

        public bool TryHandleHotKey(Key key)
        {
            if (HotKeys == null)
                return false;

            if (HotKeys.ContainsKey(key))
            {
                var filterButtonToToggle = HotKeys[key];
                if (filterButtonToToggle.Visibility == Visibility.Visible) 
                {
                    filterButtonToToggle.IsChecked = !filterButtonToToggle.IsChecked;
                    return true;
                }
            }
            return false;
        }

        public List<T> GetFilters()
        {
            var filters = new List<T>();
            if (Visibility == Visibility.Visible)
                filters.AddRange(FilterButtons.Where(q => q.Key.IsChecked.HasValue && q.Key.IsChecked.Value).Select(q => q.Value));
            return filters;
        }

        public List<T> GetAllFilters() =>
            FilterButtons.Values.ToList();

        public bool IsActive() =>
            FilterButtons?.Any(q => q.Key.IsChecked.HasValue && q.Key.IsChecked.Value) ?? false;

        public void Deactivate()
        {
            foreach (var filterButton in FilterButtons.Keys)
                filterButton.IsChecked = false;
        }

        public void ShowPopups()
        {
            if (Visibility != Visibility.Visible)
                return;

            foreach (var filterButton in FilterButtons.Keys)
                filterButton.ShowPopup();
        }

        public void HidePopups()
        {
            foreach (var filterButton in FilterButtons.Keys)
                filterButton.HidePopup();
        }
    }
}
