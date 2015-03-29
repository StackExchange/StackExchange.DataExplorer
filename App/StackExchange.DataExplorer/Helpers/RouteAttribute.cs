using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Routing;

namespace StackExchange.DataExplorer.Helpers
{
    /// <summary>
    /// Allows MVC routing urls to be declared on the action they map to.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class StackRouteAttribute : ActionMethodSelectorAttribute, IComparable<StackRouteAttribute>
    {
        /// <summary>
        /// Contains keys that can be used in routes for well-known constraints, e.g. "users/{id:INT}" - this route would ensure the 'id' parameter
        /// would only accept at least one number to match.
        /// </summary>
        public static readonly Dictionary<string, string> PredefinedConstraints = new Dictionary<string, string>
                                                                                      {
                                                                                          {"INT", @"\d+"},
                                                                                          {
                                                                                              "INTS_DELIMITED",
                                                                                              @"\d+(;\d+)*"
                                                                                              },
                                                                                          {
                                                                                              "GUID",
                                                                                              @"\b[A-Fa-f0-9]{8}(?:-[A-Fa-f0-9]{4}){3}-[A-Za-z0-9]{12}\b"
                                                                                              }
                                                                                      };

        private string _url;

        public StackRouteAttribute(string url)
            : this(url, "", null, RoutePriority.Default)
        {
        }


        public StackRouteAttribute(string url, HttpVerbs verbs)
            : this(url, "", verbs, RoutePriority.Default)
        {
        }


        public StackRouteAttribute(string url, RoutePriority priority)
            : this(url, "", null, priority)
        {
        }

        public StackRouteAttribute(string url, HttpVerbs verbs, RoutePriority priority)
            : this(url, "", verbs, priority)
        {
        }

        private StackRouteAttribute(string url, string name, HttpVerbs? verbs, RoutePriority priority)
        {
            Url = url.ToLower();
            Name = name;
            AcceptVerbs = verbs;
            Priority = priority;
        }


        /// <summary>
        /// The explicit verbs that the route will allow.  If null, all verbs are valid.
        /// </summary>
        public HttpVerbs? AcceptVerbs { get; set; }

        public RoutePriority Priority { get; set; }

        /// <summary>
        /// Optional name to allow this route to be referred to later.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The request url that will map to the decorated action method.
        /// Specifying optional parameters: "/users/{id}/{name?}" where 'name' may be omitted.
        /// Specifying constraints on parameters: "/users/{id:(\d+)}" where 'id' matches a regex for at least one number
        /// Constraints can also be predefined: "/users/{id:INT}" where 'id' will be constrained to the predefined INT regex <see cref="PredefinedConstraints"/>.
        /// </summary>
        public string Url
        {
            get { return _url; }
            set
            {
                _url = ParseUrlForConstraints(value);
                    /* side-effects include setting this.OptionalParameters and this.Constraints */
            }
        }

        /// <summary>
        /// If true, ensures that the calling method
        /// </summary>
        public bool EnsureXSRFSafe { get; set; }

        /// <summary>
        /// Gets any optional parameters contained by this Url. Optional parameters are specified with a ?, e.g. "users/{id}/{name?}".
        /// </summary>
        public string[] OptionalParameters { get; private set; }

        /// <summary>
        /// Based on /users/{id:(\d+)(;\d+)*}
        /// </summary>
        public Dictionary<string, string> Constraints { get; private set; }

        #region IComparable<StackRouteAttribute> Members

        public int CompareTo(StackRouteAttribute other)
        {
            int diff = other.Priority.CompareTo(Priority);
            if (diff == 0)
            {
                diff = Url.CompareTo(other.Url);
            }
            return diff;
        }

        #endregion

        /// <summary>
        /// Within the StackOverflow.dll assembly, looks for any action methods that have the RouteAttribute defined, 
        /// adding the routes to the parameter 'routes' collection.
        /// </summary>
        public static void MapDecoratedRoutes(RouteCollection routes)
        {
            MapDecoratedRoutes(routes, Assembly.GetExecutingAssembly());
        }

        /// <summary>
        /// Looks for any action methods in 'assemblyToSearch' that have the RouteAttribute defined, 
        /// adding the routes to the parameter 'routes' collection.
        /// </summary>
        /// <param name="routes">The output is appended to this collection</param>
        /// <param name="assemblyToSearch">An assembly containing Controllers with public methods decorated with the RouteAttribute</param>
        public static void MapDecoratedRoutes(RouteCollection routes, Assembly assemblyToSearch)
        {
            IEnumerable<MethodInfo> decoratedMethods = from t in assemblyToSearch.GetTypes()
                                                       where t.IsSubclassOf(typeof (Controller))
                                                       from m in t.GetMethods()
                                                       where m.IsDefined(typeof (StackRouteAttribute), false)
                                                       select m;

            Debug.WriteLine(string.Format("MapDecoratedRoutes - found {0} methods decorated with RouteAttribute",
                                          decoratedMethods.Count()));

            var methodsToRegister = new SortedDictionary<StackRouteAttribute, MethodInfo>();
                // sort urls alphabetically via RouteAttribute's IComparable implementation

            // first, collect all the methods decorated with our RouteAttribute
            foreach (MethodInfo method in decoratedMethods)
            {
                foreach (object attr in method.GetCustomAttributes(typeof (StackRouteAttribute), false))
                {
                    var ra = (StackRouteAttribute) attr;
                    if (!methodsToRegister.Any(p => p.Key.Url.Equals(ra.Url)))
                        methodsToRegister.Add(ra, method);
                    else
                        Debug.WriteLine("MapDecoratedRoutes - found duplicate url -> " + ra.Url);
                }
            }

            // now register the unique urls to the Controller.Method that they were decorated upon
            foreach (var pair in methodsToRegister)
            {
                StackRouteAttribute attr = pair.Key;
                MethodInfo method = pair.Value;
                string action = method.Name;

                Type controllerType = method.ReflectedType;
                string controllerName = controllerType.Name.Replace("Controller", "");
                string controllerNamespace = controllerType.FullName.Replace("." + controllerType.Name, "");

                Debug.WriteLine(string.Format("MapDecoratedRoutes - mapping url '{0}' to {1}.{2}.{3}", attr.Url,
                                              controllerNamespace, controllerName, action));

                var route = new Route(attr.Url, new MvcRouteHandler());
                route.Defaults = new RouteValueDictionary(new {controller = controllerName, action});

                // optional parameters are specified like: "users/filter/{filter?}"
                if (attr.OptionalParameters != null)
                {
                    foreach (string optional in attr.OptionalParameters)
                        route.Defaults.Add(optional, "");
                }

                // constraints are specified like: @"users/{id:\d+}" or "users/{id:INT}"
                if (attr.Constraints != null)
                {
                    route.Constraints = new RouteValueDictionary();

                    foreach (var constraint in attr.Constraints)
                        route.Constraints.Add(constraint.Key, constraint.Value);
                }

                // fully-qualify route to its controller method by adding the namespace; allows multiple assemblies to share controller names/routes
                // e.g. StackOverflow.Controllers.HomeController, StackOverflow.Api.Controllers.HomeController
                route.DataTokens = new RouteValueDictionary(new {namespaces = new[] {controllerNamespace}});

                routes.Add(attr.Name, route);
            }
        }

        public override bool IsValidForRequest(ControllerContext cc, MethodInfo mi)
        {
            bool result = true;

            if (AcceptVerbs.HasValue)
                result = new AcceptVerbsAttribute(AcceptVerbs.Value).IsValidForRequest(cc, mi);

            if (result && EnsureXSRFSafe)
            {
                if (!AcceptVerbs.HasValue || (AcceptVerbs.Value & HttpVerbs.Post) == 0)
                    throw new ArgumentException(
                        "When this.XSRFSafe is true, this.AcceptVerbs must include HttpVerbs.Post");

                result = new XSRFSafeAttribute().IsValidForRequest(cc, mi);
            }

            return result;
        }

        public override string ToString()
        {
            return (AcceptVerbs.HasValue ? AcceptVerbs.Value.ToString().ToUpper() + " " : "") + Url;
        }

        private string ParseUrlForConstraints(string url)
        {
            // example url with both optional specifier and a constraint: "posts/{id:INT}/edit-submit/{revisionguid?:GUID}"
            // note that a constraint regex cannot use { } for quantifiers
            MatchCollection matches = Regex.Matches(url,
                                                    @"{(?<param>\w+)(?<metadata>(?<optional>\?)?(?::(?<constraint>[^}]*))?)}",
                                                    RegexOptions.IgnoreCase);

            if (matches.Count == 0) return url; // vanilla route without any parameters, e.g. "home", "users/login"   

            string result = url;
            var optionals = new List<string>();
            var constraints = new Dictionary<string, string>();

            foreach (Match m in matches)
            {
                string metadata = m.Groups["metadata"].Value; // all the extra info after the parameter name
                if (metadata.HasValue()) // we have optional specifier and/or constraints
                {
                    string param = m.Groups["param"].Value; // the name, e.g. 'id' in "/users/{id}"

                    if (m.Groups["optional"].Success)
                        optionals.Add(param);

                    string constraint = m.Groups["constraint"].Value;
                    if (constraint.HasValue())
                    {
                        string predefined = null;
                        if (PredefinedConstraints.TryGetValue(constraint.ToUpper(), out predefined))
                            constraint = predefined;

                        constraints.Add(param, constraint +
                            // If the parameter is optional, also match the empty string (our default)
                            (m.Groups["optional"].Success ? "|^$" : ""));
                    }

                    result = result.Replace(m.Value, "{" + param + "}");
                }
            }

            if (optionals.Count > 0) OptionalParameters = optionals.ToArray();
            if (constraints.Count > 0) Constraints = constraints;

            return result;
        }
    }
}