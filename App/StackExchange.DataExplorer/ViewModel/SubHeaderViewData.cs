using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StackExchange.DataExplorer.ViewModel {
    public class SubHeaderViewData {
        public string Title { get; set; }
        public string Href { get; set; }
        public bool Selected { get; set; }
        public string Description { get; set; }
        public bool RightAlign { get; set; }
    }
}