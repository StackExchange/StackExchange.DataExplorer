namespace StackExchange.DataExplorer.ViewModel
{
    public class SubHeaderViewData
    {
        private string _name;

        public string Id { get; set; }
        public string Title { get; set; }
        public string Href { get; set; }
        public bool Selected { get; set; }
        public bool Default { get; set; }
        public string Description { get; set; }
        public bool RightAlign { get; set; }

        public string Name
        {
            get { return _name ?? Description; }
            set { _name = value; }
        }
    }
}