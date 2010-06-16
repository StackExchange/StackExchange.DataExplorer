using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.SqlClient;
using System.Configuration;
using StackExchange.DataExplorer.Helpers;
using System.ComponentModel.DataAnnotations;

namespace StackExchange.DataExplorer.Models {

    [MetadataType(typeof(SavedQueryMetadata))]
    public partial class SavedQuery {
    }


    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple=false)]
    public class MinLengthAttribute : ValidationAttribute
    {
        public int MinLength { get; private set; }

        public MinLengthAttribute (int minLength) {
            this.MinLength = minLength;
	    }

        public override string FormatErrorMessage(string name) {
            return name + " was too short, the minimum length is " + MinLength.ToString();
        }

        public override bool IsValid(object value)
        {
 	        int num = (value == null) ? 0 : ((string) value).Length;
            return (num > MinLength);
        }

    }


    public class SavedQueryMetadata {
        [Required]
        [MinLengthAttribute(10)]
        public string Title { get; set; }
    }


}