using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StackExchange.DataExplorer.ViewModel {
    public class SubHeader {

        public SubHeader() { 
        
        }
        
        public SubHeader(string title) {
            this.Title = title;
        }

        public string Title { get; set; }
        public IEnumerable<SubHeaderViewData> Items { get; set; }
    }
}