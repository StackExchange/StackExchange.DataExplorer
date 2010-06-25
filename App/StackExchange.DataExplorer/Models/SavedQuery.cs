namespace StackExchange.DataExplorer.Models
{
    public partial class SavedQuery
    {
        public void UpdateQueryBodyComment()
        {
            Query.Name = Title;
            Query.Description = Description;
            Query.UpdateQueryBodyComment();
        }
    }
}