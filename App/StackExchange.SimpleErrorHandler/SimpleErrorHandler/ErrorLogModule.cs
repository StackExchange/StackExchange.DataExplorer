/*
 This file is derived off ELMAH:

http://code.google.com/p/elmah/

http://www.apache.org/licenses/LICENSE-2.0
 
 */

namespace SimpleErrorHandler
{
    using System;
    using System.Web;
    using Trace = System.Diagnostics.Trace;
    using System.Text.RegularExpressions;
    using System.Collections;

    /// <summary>
    /// HTTP module implementation that logs unhandled exceptions in an ASP.NET Web application to an error log.
    /// </summary>   
    public class ErrorLogModule : IHttpModule
    {

        /// <summary>
        /// Initializes the module and prepares it to handle requests.
        /// </summary>
        public virtual void Init(HttpApplication application)
        {
            if (application == null) throw new ArgumentNullException("application");
            application.Error += new EventHandler(OnError);
        }

        /// <summary>
        /// Disposes of the resources (other than memory) used by the module.
        /// </summary>
        public virtual void Dispose()
        {
        }

        /// <summary>
        /// Gets the <see cref="ErrorLog"/> instance to which the module will log exceptions.
        /// </summary>        
        protected virtual ErrorLog ErrorLog
        {
            get { return ErrorLog.Default; }
        }

        /// <summary>
        /// Returns true if t is of className, or descendent from className
        /// </summary>
        /// <param name="t"></param>
        /// <param name="className"></param>
        /// <returns></returns>
        private static bool IsDescendentOf(Type t, string className)
        {
            if (t.FullName == className) return true;

            return t.BaseType != null ?
                    IsDescendentOf(t.BaseType, className) :
                    false;
        }

        /// <summary>
        /// The handler called when an unhandled exception bubbles up to the module.
        /// </summary>
        protected virtual void OnError(object sender, EventArgs args)
        {
            HttpApplication application = (HttpApplication)sender;
            Exception ex = application.Server.GetLastError();
            foreach (DictionaryEntry de in ErrorLog.IgnoreRegex)
	        {
                string pattern = de.Value.ToString();
		        if (Regex.IsMatch(ex.ToString(), pattern, RegexOptions.IgnoreCase))
                    return;
	        }

            foreach (DictionaryEntry de in ErrorLog.IgnoreExceptions)
            {
                if (IsDescendentOf(ex.GetType(), de.Value.ToString()))
                    return;
            }

            LogException(ex, application.Context);
        }

        /// <summary>
        /// Logs an exception and its context to the error log.
        /// </summary>
        public virtual void LogException(Exception e, HttpContext context)
        {
            try
            {            
                this.ErrorLog.Log(new Error(e, context));
            }
            catch (Exception localException)
            {
                //
                // *IMPORTANT!* We swallow any exception raised during the 
                // logging and send them out to the trace . The idea 
                // here is that logging of exceptions by itself should not 
                // be  critical to the overall operation of the application.
                // The bad thing is that we catch ANY kind of exception, 
                // even system ones and potentially let them slip by.
                //
                Trace.WriteLine(localException);
            }
        }
    }
}