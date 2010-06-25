using System.Collections.Generic;

namespace StackExchange.DataExplorer.ViewModel
{
    public class SubHeader
    {
        public SubHeader()
        {
        }

        public SubHeader(string title)
        {
            Title = title;
        }

        public string Title { get; set; }
        public IEnumerable<SubHeaderViewData> Items { get; set; }
    }
}