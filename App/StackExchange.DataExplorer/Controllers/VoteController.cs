using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Controllers
{
    public class VoteController : StackOverflowController
    {
        [HttpPost]
        [Route(@"vote/{id:\d+}")]
        public ActionResult Vote(int id, string voteType) {
            if (Current.User.IsAnonymous) {
                return new EmptyResult();
            }

            var db = Current.DB;

            if (voteType == "favorite") {
                var vote = db.Votes.FirstOrDefault(v => v.SavedQueryId == id 
                    && v.UserId == Current.User.Id 
                    && v.VoteTypeId == (int)VoteType.Favorite);

                if (vote == null) {
                    vote = new Vote()
                    {
                        SavedQueryId = id,
                        VoteTypeId = (int)VoteType.Favorite,
                        UserId = Current.User.Id,
                        CreationDate = DateTime.UtcNow
                    };
                    db.Votes.InsertOnSubmit(vote); 

                } else {
                    db.Votes.DeleteOnSubmit(vote);
                }

                db.SubmitChanges();

                var favoriteCounts = from v in db.Votes
                                     where v.VoteTypeId == (int)VoteType.Favorite && v.SavedQueryId == id
                                     group v by v.SavedQueryId into g
                                     select new { Id = g.Key, Count = g.Count() };

                var savedQuery = db.SavedQueries.First(q => q.Id == id);
                var firstCount = favoriteCounts.FirstOrDefault();
                savedQuery.FavoriteCount = firstCount == null ? 0 : firstCount.Count;
                db.SubmitChanges();
            }

            return Json(new {success = true});
        }

    }
}
