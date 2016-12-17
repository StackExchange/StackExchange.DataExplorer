using System.Collections.Generic;

namespace StackExchange.DataExplorer.ViewModel
{
    public class SubHeader
    {
        private IEnumerable<SubHeaderViewData> _items;

        public SubHeader()
        {
            DetermineSelected();
        }

        public SubHeader(string title) : this()
        {
            Title = title;
        }

        public string Title { get; set; }
        public string Selected { get; set; }

        public IEnumerable<SubHeaderViewData> Items
        {
            get { return _items; }
            set
            {
                _items = value;
                DetermineSelected();
            }
        }

        private void DetermineSelected()
        {
            if (_items == null)
            {
                return;
            }

            var found = false;
            SubHeaderViewData defaultTab = null;

            foreach (SubHeaderViewData tab in _items)
            {
                // In case the tab is explicitly selected
                if (tab.Name == Selected || tab.Selected)
                {
                    tab.Selected = true;
                    found = true;

                    break;
                }

                // First or specified default
                if (defaultTab == null || (!defaultTab.Default && tab.Default))
                {
                    defaultTab = tab;
                }
            }

            if (!found)
            {
                defaultTab.Selected = true;
                Selected = defaultTab.Name;
            }
        }
    }
}