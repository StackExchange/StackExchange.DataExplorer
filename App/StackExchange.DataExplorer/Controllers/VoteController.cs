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
        [StackRoute(@"vote/{querySetId:\d+}")]
        public ActionResult Vote(int querySetId, string voteType)
        {
            if (Current.User.IsAnonymous)
            {
                return new EmptyResult();
            }

            if (voteType == "favorite")
            {
                QuerySet querySet = Current.DB.QuerySets.Get(querySetId);

                if (querySet == null)
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
                        querySetId = querySetId,
                        user = CurrentUser.Id
                    }
                ).FirstOrDefault();

                if (vote == null)
                {
                    Current.DB.Votes.Insert(new 
                    {
                        QuerySetId = querySetId,
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
                        id = querySetId
                    }
                );
            }

            return Json(new {success = true});
        }
    }
}