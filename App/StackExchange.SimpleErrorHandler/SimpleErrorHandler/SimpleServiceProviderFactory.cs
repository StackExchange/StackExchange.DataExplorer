namespace SimpleErrorHandler
{
    using System;
    using System.Configuration;
    using System.Collections;

    /// <summary>
    /// A simple factory for creating instances of types specified in a section of the configuration file.
    /// </summary>	
    internal sealed class SimpleServiceProviderFactory
    {
        public static object CreateFromConfigSection(string sectionName)
        {
            // Get the configuration section with the settings.
            IDictionary config = (IDictionary)ConfigurationManager.GetSection(sectionName);
            if (config == null) return null;

            // We modify the settings by removing items as we consume them so make a copy here.
            config = (IDictionary)((ICloneable)config).Clone();

            // Get the type specification of the service provider.
            string typeSpec = (string)config["type"] ?? "";
            if (typeSpec.Length == 0) return null;
            config.Remove("type");

            // Locate, create and return the service provider object.
            Type type = Type.GetType(typeSpec, true);
            return Activator.CreateInstance(type, new object[] { config });
        }

        private SimpleServiceProviderFactory() { }
    }
}
