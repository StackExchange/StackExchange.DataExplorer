using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace StackExchange.DataExplorer.Helpers {
    /// <summary>
    /// Contains keys for common app-wide keys (used in querystring, form, etc)
    /// </summary>
    public static class Keys {
        public const string OpenId = "openid_identifier";
        public const string Session = "s";
        public const string ReturnUrl = "returnurl";
        public const string UserFlag = "m";
    }

    /// <summary>
    /// Contains keys for common ViewData collection values
    /// </summary>
    public static class ViewDataKeys {
        public const string CurrentTags = "CurrentTags";
        public const string QuestionOwnerId = "QuestionOwnerId";
        public const string CurrentUser = "CurrentUser";
        public const string Error = "Error";
        public const string SystemMessage = "SystemMessage";
        public const string UserMessages = "UserMessages";
        public const string InformModeratorCount = "InformModeratorCount";
    }

    /// <summary>
    /// Contains keys for FormTypes we store in our database session
    /// </summary>
    public static class FormTypeKeys {
        public const string Authorize = "Authorize";
        public const string Captcha = "Captcha";
        public const string Confirm = "Confirm";
    }
}