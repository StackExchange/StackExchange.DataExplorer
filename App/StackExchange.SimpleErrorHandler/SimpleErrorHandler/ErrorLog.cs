namespace SimpleErrorHandler
{
    using System;
    using System.Web;
    using System.Collections.Generic;
    using System.Collections;
    using System.Configuration;

    /// <summary>
    /// Represents an error log capable of storing and retrieving errors generated in an ASP.NET Web application.
    /// </summary>
    public abstract class ErrorLog
    {
        [ThreadStatic]
        private static ErrorLog _defaultLog;

        [ThreadStatic]
        private static Hashtable _ignoreRegex;

        [ThreadStatic]
        private static Hashtable _ignoreExceptions;

        /// <summary>
        /// Logs an error in log for the application.
        /// </summary>
        public abstract void Log(Error error);

        /// <summary>
        /// Retrieves a single application error from log given its identifier, or null if it does not exist.
        /// </summary>
        public abstract ErrorLogEntry GetError(string id);

        public abstract bool DeleteError(string id);

        /// <summary>
        /// Prevents error identfied by 'id' from being deleted when the error log is full.
        /// </summary>
        public abstract bool ProtectError(string id);

        /// <summary>
        /// Retrieves a page of application errors from the log in descending order of logged time.
        /// </summary>
        public abstract int GetErrors(int pageIndex, int pageSize, IList errorEntryList);

        /// <summary>
        /// Get the name of this log.
        /// </summary>
        public virtual string Name
        {
            get { return this.GetType().Name; }
        }

        /// <summary>
        /// Gets the name of the application to which the log is scoped.
        /// </summary>
        public virtual string ApplicationName
        {
            get { return HttpRuntime.AppDomainAppId; }
        }

        /// <summary>
        /// Gets the list of exceptions to ignore specified in the configuration file
        /// </summary>
        public static Hashtable IgnoreRegex
        {
            get
            {
                if (_ignoreRegex == null)
                {
                    Hashtable h = (Hashtable)ConfigurationManager.GetSection("SimpleErrorHandler/ignoreRegex");
                    if (h == null)
                    {
                        _ignoreRegex = new Hashtable();
                    }
                    else
                    {
                        _ignoreRegex = h;
                    }
                }
                return _ignoreRegex;
            }
        }

        /// <summary>
        /// Gets the list of exceptions to ignore specified in the configuration file
        /// 
        /// </summary>
        public static Hashtable IgnoreExceptions
        {
            get
            {
                if (_ignoreExceptions == null)
                {
                    Hashtable h = (Hashtable)ConfigurationManager.GetSection("SimpleErrorHandler/ignoreException");
                    if (h == null)
                        h = new Hashtable();

                    _ignoreExceptions = h;
                }

                return _ignoreExceptions;
            }
        }

        /// <summary>
        /// Gets the default error log implementation specified in the configuration file, 
        /// or the in-memory log implemention if none is configured.
        /// </summary>
        public static ErrorLog Default
        {
            get
            {
                if (_defaultLog == null)
                {
                    // Determine the default store type from the configuration and create an instance of it.
                    ErrorLog log = (ErrorLog)SimpleServiceProviderFactory.CreateFromConfigSection("SimpleErrorHandler/errorLog");
                    // default to in-memory logger
                    _defaultLog = (log != null) ? log : new MemoryErrorLog();
                }
                return _defaultLog;
            }
        }
    }
}