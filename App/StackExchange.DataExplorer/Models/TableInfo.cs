using System.Collections.Generic;

namespace StackExchange.DataExplorer.Models
{
    public class TableInfo
    {
        public TableInfo()
        {
            ColumnNames = new List<string>();
            DataTypes = new List<string>();
        }

        public string Name { get; set; }
        public List<string> ColumnNames { get; set; }
        public List<string> DataTypes { get; set; }
    }
}