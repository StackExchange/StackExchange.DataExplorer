using System;

namespace StackExchange.DataExplorer
{
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class DefaultAttribute : Attribute
    {
        public DefaultAttribute(object defaultValue)
        {
            DefaultValue = defaultValue;
        }

        public object DefaultValue { get; private set; }
    }
}
