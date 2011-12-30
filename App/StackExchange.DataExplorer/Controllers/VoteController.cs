using System;
using System.Linq;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Controllers
{
    public class VoteController : StackOverflowController
    {
        [HttpPost]
        [Route(@"vote/{id:\d+}")]
        public ActionResult Vote(int id, string voteType)
        {
            if (Current.User.IsAnonymous)
            {
                return new EmptyResult();
            }

            if (voteType == "favorite")
            {
                Revision revision = QueryUtil.GetCompleteRevision(id);

                if (revision == null)
                {
                    return Json(new { error = true });
                }

                Vote vote = Current.DB.Query<Vote>(@"
                    SELECT
                        *
                    FROM
                        Votes
                    WHERE
                        VoteTypeId = @vote AND
                        QuerySetId = @querySetId AND
                        UserId = @user"
                    ,
                    new
                    {
                        vote = (int)VoteType.Favorite,
                        querySetId = revision.QuerySet.Id,
                        user = CurrentUser.Id
                    }
                ).FirstOrDefault();

                if (vote == null)
                {
                    Current.DB.Votes.Insert(new 
                    {
                        QuerySetId = revision.QuerySet.Id,
                        UserId = CurrentUser.Id,
                        VoteTypeId = (int)VoteType.Favorite,
                        CreationDate = DateTime.UtcNow
                    });
                }
                else
                {
                    Current.DB.Execute("DELETE Votes WHERE Id = @id", new { id = vote.Id });
                }

                Current.DB.Execute(@"
                    UPDATE
                        QuerySets
                    SET
                        Votes = Votes + @change
                    WHERE
                        Id = @id",
                    new
                    {
                        change = vote == null ? 1 : -1,
                        id = revision.QuerySet.Id
                    }
                );
            }

            return Json(new {success = true});
        }
    }
}