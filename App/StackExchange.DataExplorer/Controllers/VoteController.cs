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
                Revision revision = QueryUtil.GetBasicRevision(id);

                if (revision == null || revision.OwnerId == CurrentUser.Id)
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
                        RootId = @root AND
                        UserId = @user AND
                        OwnerId " + (revision.OwnerId != null ? " = @owner" : " IS NULL"),
                    new
                    {
                        vote = (int)VoteType.Favorite,
                        root = id,
                        owner = revision.OwnerId,
                        user = CurrentUser.Id
                    }
                ).FirstOrDefault();

                if (vote == null)
                {
                    Current.DB.Execute(@"
                        INSERT INTO Votes(
                            OwnerId, RootId, UserId, VoteTypeId, CreationDate
                        ) VALUES(
                            @owner, @root, @user, @vote, @creation
                        )",
                        new
                        {
                            vote = (int)VoteType.Favorite,
                            root = id,
                            owner = revision.OwnerId,
                            user = CurrentUser.Id,
                            creation = DateTime.UtcNow
                        }
                    );
                }
                else
                {
                    Current.DB.Execute("DELETE Votes WHERE Id = @id", new { id = vote.Id });
                }

                Current.DB.Execute(@"
                    UPDATE
                        Metadata
                    SET
                        Votes = Votes + @change
                    WHERE
                        RevisionId = @root AND
                        OwnerId " + (revision.OwnerId != null ? " = @owner" : " IS NULL"),
                    new
                    {
                        change = vote == null ? 1 : -1,
                        root = id,
                        owner = revision.OwnerId
                    }
                );
            }

            return Json(new {success = true});
        }
    }
}