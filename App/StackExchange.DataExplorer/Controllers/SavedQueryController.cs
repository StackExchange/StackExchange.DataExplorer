using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;
using StackExchange.DataExplorer.ViewModel;


namespace StackExchange.DataExplorer.Controllers
{
    public class SavedQueryController : StackOverflowController
    {

        [Route("{sitename}/s/{id}")]
        [Route("{sitename}/s/{id}/{slug}")]
        [Route("{sitename}/st/{id}")]
        [Route("{sitename}/st/{id}/{slug}")]
        public ActionResult Show(int id, string sitename, string slug) {
            var db = Current.DB;
            var site = db.Sites.First(s => s.Name.ToLower() == sitename);
            this.Site = site;
            string format = "default";

            if (Request.Path.IndexOf("/st/", 0, sitename.Length + 6) > 0) {
                format = "text";
            }

            ViewData["GuessedUserId"] = site.GuessUserId(CurrentUser);

            SelectMenuItem("Queries");

            var savedQuery = db.SavedQueries.First(q => q.Id == id);
            SetHeader(savedQuery.Title);


            var totalVotes = db.Votes.Where(v => v.SavedQueryId == id && v.VoteTypeId == (int)VoteType.Favorite).Count();

            QueryVoting voting = new QueryVoting() { 
                TotalVotes = totalVotes
            }; 

            if (!Current.User.IsAnonymous) {
                voting.HasVoted = (
                db.Votes.FirstOrDefault(v => v.SavedQueryId == id 
                && v.UserId == Current.User.Id 
                && v.VoteTypeId == (int)VoteType.Favorite)
                ) != null; 
            }

            ViewData["QueryVoting"] = voting;
            ViewData["Sites"] = Current.DB.Sites.ToList();
            ViewData["LoggedOn"] = Current.User.IsAnonymous ? "false" : "true";

            var cachedResults = GetCachedResults(savedQuery.Query);
            if (cachedResults != null) {
                ViewData["cached_results"] = cachedResults;

                if (format == "text") {

                    if (cachedResults != null && cachedResults.Results != null) {
                        cachedResults.Results = QueryResults.FromJson(cachedResults.Results).ToTextResults().ToJson();
                    }
                } 
            
            }



            return View(savedQuery); 
        }

        [Route("saved_query/delete", HttpVerbs.Post)]
        public ActionResult Delete(int id) {
            var query = Current.DB.SavedQueries.FirstOrDefault(q => q.Id == id);
            if (query != null && (query.UserId == Current.User.Id || Current.User.IsAdmin)) {
                query.IsDeleted = true;
                if (query.IsFeatured ?? false) {
                    return Json("Query is featured, delete is not permitted");
                }
                Current.DB.SubmitChanges();
            }

            return Json("success");
        }

        [Route("saved_query/undelete", HttpVerbs.Post)]
        public ActionResult Undelete(int id) {
            var query = Current.DB.SavedQueries.FirstOrDefault(q => q.Id == id);
            if (query != null && (query.UserId == Current.User.Id || Current.User.IsAdmin)) {
                query.IsDeleted = false;
                Current.DB.SubmitChanges();
            }

            return Json("success");
        }

        [Route("saved_query/create", HttpVerbs.Post)]
        public ActionResult Create(SavedQuery query)
        {
            if (Current.User.IsAnonymous) {
                throw new InvalidOperationException("You must be logged on to access this method");
            }

            var db = Current.DB;

            SavedQuery updateQuery = null;

            var oSavedQueryId = Request.Params["savedQueryId"];
            if (oSavedQueryId != null) {
                int savedQueryId; 
                Int32.TryParse(oSavedQueryId, out savedQueryId);
                if (savedQueryId > 0) {
                    updateQuery = db.SavedQueries.FirstOrDefault(q => q.Id == savedQueryId);
                    if (updateQuery != null && (updateQuery.UserId == Current.User.Id || Current.User.IsAdmin)) {
                        updateQuery.QueryId = query.QueryId;
                        updateQuery.SiteId = query.SiteId;
                        updateQuery.Title = query.Title;
                        updateQuery.Description = query.Description;
                        updateQuery.LastEditDate = DateTime.UtcNow;
                    } else {
                        updateQuery = null;
                    }
                }
            }

            

            query.CreationDate = query.LastEditDate = DateTime.UtcNow;
            query.UserId = CurrentUser.Id;

            if (ModelState.IsValid) {
                if (updateQuery == null) {
                    db.SavedQueries.InsertOnSubmit(query);
                } 
                db.SubmitChanges();
                return Json(new {success = true});
            } else {
                return Json(new { success = false, message = "Title must be at least 10 chars or longer!" });
            }   
        }


        [HttpPost]
        [Route("feature_query/{id}")]
        public ActionResult Feature(int id, bool feature) {
            if (Current.User.IsAdmin) {
                Current.DB.SavedQueries.FirstOrDefault(q => q.Id == id).IsFeatured = feature;
                Current.DB.SubmitChanges();
            }

            return Content("success");
        }

        [HttpPost]
        [Route("skip_query/{id}")]
        public ActionResult Skip(int id, bool skip) {
            if (Current.User.IsAdmin) {
                Current.DB.SavedQueries.FirstOrDefault(q => q.Id == id).IsSkipped = skip;
                Current.DB.SubmitChanges();
            }

            return Content("success");
        }


        [Route("{sitename}/queries")]
        public ActionResult Index(string sitename, string order_by, int? page, int? pagesize) {

            var site = Current.DB.Sites.First(s => s.Name.ToLower() == sitename);

            if (string.IsNullOrEmpty(order_by)) {
                order_by = "featured";
            }

            this.Site = site;
            SelectMenuItem("Queries");

            SetHeader("All Queries",
                new SubHeaderViewData()
                {
                    Description = "featured",
                    Title = "Interesting queries selected by the administrators",
                    Href = "/" + sitename + "/queries?order_by=featured",
                    Selected = (order_by == "featured")
                },
                new SubHeaderViewData()
                {
                    Description = "recent",
                    Title = "Recently saved queries",
                    Href = "/" + sitename + "/queries?order_by=recent",
                    Selected = (order_by == "recent")
                },
                new SubHeaderViewData()
                {
                    Description = "favorite",
                    Title = "Favorite saved queries",
                    Href = "/" + sitename + "/queries?order_by=favorite",
                    Selected = (order_by == "favorite")
                },
                new SubHeaderViewData()
                {
                    Description = "popular",
                    Title = "Saved queries with the most views",
                    Href = "/" + sitename + "/queries?order_by=popular",
                    Selected = (order_by == "popular")
                },

                 new SubHeaderViewData()
                 {
                     Description = "everything",
                     Title = "All queries recently executed on the site",
                     Href = "/" + sitename + "/queries?order_by=everything",
                     Selected = (order_by == "everything")
                 }

                );


            ViewData["Site"] = site;

            var db = Current.DB;

            var query = db.Queries.Select(q => new { Query = q, Saved = (SavedQuery)null });


            if (order_by != "everything") {

                query = from q in query
                        join s in db.SavedQueries on q.Query.Id equals s.QueryId
                        where s.IsFirst == true
                        select new { Query = q.Query, Saved = s };
            } 
            
            if (order_by == "featured") {
                query = query.Where(q => q.Saved.IsFeatured == true);
            }

            if (order_by != "everything") {
                query = query.Where(q => !(q.Saved.IsDeleted ?? false));
            }


            var transformed = query.Select(q => new QueryExecutionViewData()
            {
                Id =  q.Saved == null ? q.Query.Id : q.Saved.Id,
                SQL = q.Query.QueryBody,
                Name = q.Saved == null ? q.Query.Name : q.Saved.Title,
                Description = q.Saved == null ? q.Query.Description : q.Saved.Description,
                SiteName = site.Name.ToLower(),
                LastRun = q.Query.FirstRun ?? DateTime.Now,
                Views = q.Query.Views ?? 1,
                Creator = q.Saved == null ? q.Query.User : q.Saved.User,
                Featured = q.Saved == null ? false : q.Saved.IsFeatured ?? false,
                Skipped = q.Saved == null ? false : q.Saved.IsSkipped ?? false,
                FavoriteCount = q.Saved == null ? 0 :  q.Saved.FavoriteCount,
                UrlPrefix = q.Saved == null ?  "q" : "s"
            });

            if (order_by == "popular" ) {
                transformed = transformed.OrderByDescending(item => item.Views)
                     .ThenByDescending(item => item.FavoriteCount);
            } else if (order_by == "favorite" || order_by == "featured") {
                transformed = transformed.OrderByDescending(item => item.FavoriteCount)
                    .ThenByDescending(item => item.Views);
            } else {
                transformed = transformed.OrderByDescending(item => item.LastRun);
            }


            var totalQueries = transformed.Count();
            ViewData["TotalQueries"] = string.Format("{0:n0}", totalQueries);
            var currentPerPage = pagesize ?? 50;
            var currentPage = page ?? 1;
            var href = "/" + site.Name.ToLower() + "/queries?order_by=" + order_by;
            ViewData["PageNumbers"] = new PageNumber(href + "&page=-1", (totalQueries / currentPerPage) + 1, currentPage - 1, "pager");

            ViewData["PageSizer"] = new PageSizer(href + "&pagesize=-1", currentPage, currentPerPage, totalQueries, "page-sizer fr");

            return View(transformed.Skip((currentPage - 1) * currentPerPage).Take(currentPerPage));
        }



    }
}
