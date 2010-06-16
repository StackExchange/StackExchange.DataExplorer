using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace SimpleErrorHandler.Test
{
    /// <summary>
    /// Allows MVC routing urls to be declared on the action they map to.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class RouteAttribute : ActionMethodSelectorAttribute
    {

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
        /// <param name="assemblyToSearch">An assembly containing Controllers with public methods decorated with the RouteAttribute</param>
        public static void MapDecoratedRoutes(RouteCollection routes, Assembly assemblyToSearch)
        {
            var decoratedMethods = from t in assemblyToSearch.GetTypes()
                                   where t.IsSubclassOf(typeof(System.Web.Mvc.Controller))
                                   from m in t.GetMethods()
                                   where m.IsDefined(typeof(RouteAttribute), false)
                                   select m;

            Debug.WriteLine(string.Format("MapDecoratedRoutes - found {0} methods decorated with RouteAttribute", decoratedMethods.Count()));

            var methodsToRegister = new Dictionary<RouteAttribute, MethodInfo>();

            // first, collect all the methods decorated with our RouteAttribute
            foreach (var method in decoratedMethods)
            {
                foreach (var attr in method.GetCustomAttributes(typeof(RouteAttribute), false))
                {
                    var ra = (RouteAttribute)attr;
                    if (!methodsToRegister.Any(p => p.Key.Url.Equals(ra.Url)))
                        methodsToRegister.Add(ra, method);
                    else
                        Debug.WriteLine("MapDecoratedRoutes - found duplicate url -> " + ra.Url);
                }
            }

            // to ease route debugging later (and accommodate routes with response-type-specific {format} parameters),
            // we'll sort the routes alphabetically, applying a custom comparer for urls with {format} in them
            var urls = new List<string>();
            methodsToRegister.Keys.ToList().ForEach(k => urls.Add(k.Url));

            urls.Sort((x, y) =>
            {
                // one url contains the other, but with extra parameters
                if (x.IndexOf(y) > -1 || y.IndexOf(x) > -1)
                {
                    // bubble up the format-carrying routes
                    if (x.Contains("{format}"))
                        return -1;
                    if (y.Contains("{format}"))
                        return 1;
                }
                return x.CompareTo(y);
            });

            // now register the unique urls to the Controller.Method that they were decorated upon, respecting the sort
            foreach (var url in urls)
            {
                var pair = methodsToRegister.First(p => p.Key.Url == url);
                var routeAttribute = pair.Key;
                var method = pair.Value;
                var action = method.Name;

                var controllerType = method.ReflectedType;
                var controllerName = controllerType.Name.Replace("Controller", "");
                var controllerNamespace = controllerType.FullName.Replace("." + controllerType.Name, "");

                Debug.WriteLine(string.Format("MapDecoratedRoutes - mapping url '{0}' to {1}.{2}.{3}", 
                    routeAttribute.Url, controllerNamespace, controllerName, action));

                var route = new Route(routeAttribute.Url, new MvcRouteHandler());
                route.Defaults = new RouteValueDictionary(new { controller = controllerName, action = action });

                // urls with optional parameters may specify which parameters can be ignored and still match
                if (routeAttribute.Optional != null && routeAttribute.Optional.Length > 0)
                {
                    for (int i = 0; i < routeAttribute.Optional.Length; i++)
                    {
                        route.Defaults.Add(routeAttribute.Optional[i], ""); // default route parameter value should be empty string..
                    }
                }

                // fully-qualify this new route to its controller method, so that multiple assemblies can have similar 
                // controller names/routes, differing only by namespace,
                // e.g. StackOverflow.Controllers.HomeController, StackOverflow.Careers.Controllers.HomeController
                route.DataTokens = new RouteValueDictionary(new { namespaces = new[] { controllerNamespace} });

                routes.Add(routeAttribute.Name, route);
            }
        }
        

        /// <summary>
        /// The explicit verbs that the route will allow.  If null, all verbs are valid.
        /// </summary>
        public HttpVerbs? AcceptVerbs { get; set; }

        /// <summary>
        /// Optional name to allow this route to be referred to later.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The request url that will map to the decorated action method.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// If true, ensures that the calling method
        /// </summary>
        public bool EnsureXSRFSafe { get; set; }

        /// <summary>
        /// Lists the bracketed parameter names in the Url that are optional.
        /// </summary>
        public string[] Optional { get; set; }


        public RouteAttribute(string url)
            : this(url, "", null)
        {
        }

        public RouteAttribute(string url, HttpVerbs verbs)
            : this(url, "", verbs)
        {
        }

        private RouteAttribute(string url, string name, HttpVerbs? verbs)
        {
            Url = url.ToLower();
            Name = name;
            AcceptVerbs = verbs;
        }


        public override bool IsValidForRequest(ControllerContext cc, MethodInfo mi)
        {
            bool result = true;

            if (AcceptVerbs.HasValue)
                result = new AcceptVerbsAttribute(AcceptVerbs.Value).IsValidForRequest(cc, mi);

            //if (result && EnsureXSRFSafe)
            //{
            //    if (!AcceptVerbs.HasValue || (AcceptVerbs.Value & HttpVerbs.Post) == 0)
            //        throw new ArgumentException("When this.XSRFSafe is true, this.AcceptVerbs must include HttpVerbs.Post");

            //    result = new XSRFSafeAttribute().IsValidForRequest(cc, mi);
            //}

            return result;
        }

        public override string ToString()
        {
            return (AcceptVerbs.HasValue ? AcceptVerbs.Value.ToString().ToUpper() + " " : "") + Url;
        }

    }
}
