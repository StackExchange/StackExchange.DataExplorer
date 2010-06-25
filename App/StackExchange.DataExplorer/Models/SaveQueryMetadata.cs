using System;
using System.ComponentModel.DataAnnotations;

namespace StackExchange.DataExplorer.Models
{
    [MetadataType(typeof (SavedQueryMetadata))]
    public partial class SavedQuery
    {
    }


    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false)]
    public class MinLengthAttribute : ValidationAttribute
    {
        public MinLengthAttribute(int minLength)
        {
            MinLength = minLength;
        }

        public int MinLength { get; private set; }

        public override string FormatErrorMessage(string name)
        {
            return name + " was too short, the minimum length is " + MinLength;
        }

        public override bool IsValid(object value)
        {
            int num = (value == null) ? 0 : ((string) value).Length;
            return (num > MinLength);
        }
    }


    public class SavedQueryMetadata
    {
        [Required]
        [MinLengthAttribute(10)]
        public string Title { get; set; }
    }
}