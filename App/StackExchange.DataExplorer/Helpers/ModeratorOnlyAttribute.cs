using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.Controllers;

namespace StackExchange.DataExplorer.Helpers {
    public class ModeratorOnlyAttribute : ActionMethodSelectorAttribute {

        public override bool IsValidForRequest(ControllerContext controllerContext, System.Reflection.MethodInfo methodInfo) {
            var c = controllerContext.Controller as StackOverflowController;

            if (c != null && c.CurrentUser != null) {
                return c.CurrentUser.IsModerator;
            }

            return false;
        }
    }
}