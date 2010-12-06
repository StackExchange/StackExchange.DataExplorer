using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StackExchange.DataExplorer
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed class DefaultAttribute : Attribute
    {
        public DefaultAttribute(object defaultValue)
        {
            DefaultValue = defaultValue;
        }

        public object DefaultValue { get; private set; }
    }
}
