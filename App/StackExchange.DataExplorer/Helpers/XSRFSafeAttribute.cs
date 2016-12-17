using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using System.Web.Mvc;
using StackExchange.DataExplorer.Controllers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Helpers
{
    public class XSRFSafeAttribute : ActionMethodSelectorAttribute
    {
        public static void EnsureSafe(NameValueCollection form, User currentUser)
        {
            string xsrfFormValue = form["fkey"];

            if (xsrfFormValue.IsNullOrEmpty())
                throw new InvalidOperationException("XSRF validation: Request did not have required form value 'fkey'");

            if (!xsrfFormValue.Equals(currentUser.XSRFFormValue, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "XSRF validation: Request form value 'fkey' did not match CurrentUser.XSRFFormValue");

            Debug.WriteLine("XSRFSafeAttribute.EnsureSafe => true");
        }

        public override bool IsValidForRequest(ControllerContext cc, MethodInfo mi)
        {
            var soController = cc.Controller as StackOverflowController;
            if (soController == null)
                throw new ArgumentException(
                    "Current ControllerContext's Controller isn't of type StackOverflowController");

            if (!soController.CurrentUser.IsAnonymous)
            {
                EnsureSafe(cc.HttpContext.Request.Form, soController.CurrentUser);
            }
            return true;
        }
    }
}