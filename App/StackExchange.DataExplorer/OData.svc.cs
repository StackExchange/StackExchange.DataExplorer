using System;
using System.Collections.Generic;
using System.Data.EntityClient;
using System.Data.Metadata.Edm;
using System.Data.Services;
using System.Data.Services.Common;
using System.Linq;
using System.ServiceModel.Web;
using System.Web;
using StackExchange.DataExplorer.Models.StackEntities;
using System.Reflection;


namespace StackExchange.DataExplorer
{

    public class OData : DataService<Entities>
    {
        // This method is called only once to initialize service-wide policies.
        public static void InitializeService(DataServiceConfiguration config)
        {
           config.SetEntitySetAccessRule("*", EntitySetRights.AllRead);
           config.SetEntitySetPageSize("*", 50);
           config.DataServiceBehavior.MaxProtocolVersion = DataServiceProtocolVersion.V2;
        }

        protected override void HandleException(HandleExceptionArgs args) {
            base.HandleException(args);
        }

        protected override Entities CreateDataSource() {
            var sqlConnection = Current.DB.Sites.First().GetConnection();

            MetadataWorkspace workspace = new MetadataWorkspace(new string[] { "res://*/" }, new List<Assembly>() { this.GetType().Assembly });
            EntityConnection connection = new EntityConnection(workspace, sqlConnection);

            Entities entities = new Entities(connection);
            return entities;
        }


        protected override void OnStartProcessingRequest(ProcessRequestArgs args)
        {
            base.OnStartProcessingRequest(args);
            HttpContext context = HttpContext.Current;

            HttpCachePolicy c = HttpContext.Current.Response.Cache;
            c.SetCacheability(HttpCacheability.ServerAndPrivate);

            c.SetExpires(HttpContext.Current.Timestamp.AddSeconds(600));

            c.VaryByHeaders["Accept"] = true;
            c.VaryByHeaders["Accept-Charset"] = true;
            c.VaryByHeaders["Accept-Encoding"] = true;
            c.VaryByParams["*"] = true;

        }

    }
 
}
