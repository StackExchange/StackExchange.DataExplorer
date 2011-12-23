using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using StackExchange.DataExplorer.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Threading;

namespace StackExchange.DataExplorer.Helpers
{
    public class AsyncQueryRunner
    {
        static AsyncQueryRunner()
        {
            Thread t = new Thread(new ThreadStart(FlushOldJobs));
            t.IsBackground = true;
            t.Priority = ThreadPriority.Lowest;
            t.Start();
        }

        // expire any jobs that completed 120 seconds ago or less 
        const int AutoExpireSeconds = 120;
        const int ExpireNonPolledSeconds = 10; 

        static ConcurrentDictionary<Guid, AsyncResult> jobs = new ConcurrentDictionary<Guid, AsyncResult>();
        static ConcurrentDictionary<string, List<Task>> running = new ConcurrentDictionary<string, List<Task>>();

        public enum AsyncState
        {
            Pending,
            Success,
            Failure
        }

        public class AsyncResult
        {
            public Task Task { get; set; }

            public DateTime? CompletionDate { get; set; }
            public DateTime LastPoll { get; set; }

            public QueryResults QueryResults { get; set; }
            public Exception Exception { get; set; }
            public Guid JobId { get; set; }
            public AsyncState State { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public Revision Parent { get; set; }
            public bool TextResults { get; set; }
            public ParsedQuery ParsedQuery { get; set; }
            public Site Site { get; set; }

            public SqlCommand Command { get; set; }
            public bool Cancelled { get; set; }
        }

        public static AsyncResult Execute(ParsedQuery query, User user, Site site, string title, string description, Revision parent, bool textResults)
        {
            string userTag = user.IsAnonymous ? user.IPAddress : user.Id.ToString();

            List<Task> activeTasks;
            running.TryGetValue(userTag, out activeTasks);
            if (activeTasks != null)
            {
                lock(activeTasks)
                {
                    if (activeTasks.Where(t => !t.IsCompleted).Count() >= AppSettings.ConcurrentQueries)
                    {
                        throw new ApplicationException("Too many queries are running, you may only run " + AppSettings.ConcurrentQueries + " queries at a time");
                    }
                }
            }
            else
            {
                running.TryAdd(userTag, new List<Task>());
                activeTasks = running[userTag];
            }

            AsyncResult result = new AsyncResult
            {
                JobId = Guid.NewGuid(),
                State = AsyncState.Pending,
                Title = title,
                Description = description, 
                Parent = parent, 
                TextResults = textResults,
                ParsedQuery = query,
                Site = site, 
                LastPoll = DateTime.UtcNow
            };

            Task task = new Task(() =>
            {
                try
                {
                    result.QueryResults = QueryRunner.GetResults(query, site, user, result);
                    result.State = AsyncState.Success;
                }
                catch (Exception e)
                {
                    result.Exception = e;
                    result.State = AsyncState.Failure;
                }
            });

            task.ContinueWith(ignore => {
                result.CompletionDate = DateTime.UtcNow;

                lock (activeTasks) 
                {
                    activeTasks.RemoveAll(t => t.IsCompleted);
                }
            });

            result.Task = task;

            jobs.TryAdd(result.JobId, result);
            task.Start();
            lock(activeTasks)
            {
                activeTasks.Add(task);
            }

            // give it some time to get results ... 
            System.Threading.Thread.Sleep(50);

            return result;
        }

        private static void FlushOldJobs()
        {
            while (true)
            {
                try
                {
                    System.Threading.Thread.Sleep(1000 * 10);

                    var expires = DateTime.UtcNow.AddSeconds(-AutoExpireSeconds);
                    foreach (var job in jobs.Values.Where(j => j.CompletionDate != null && j.CompletionDate < expires).ToList())
                    {
                        AsyncResult ignore;
                        jobs.TryRemove(job.JobId, out ignore);
                    }

                    foreach (var job in jobs.Values.Where(j => !j.Task.IsCompleted && j.LastPoll < DateTime.UtcNow.AddSeconds(-ExpireNonPolledSeconds)))
                    {
                        AsyncResult result;
                        jobs.TryGetValue(job.JobId, out result);
                        if (result != null)
                        {
                            result.Cancelled = true;
                            var cmd = result.Command;
                            if (cmd != null) cmd.Cancel();
                        }
                    }
                }
                catch (Exception e)
                { 
                    // nothing we can do really
                    Current.LogException(e);
                }
            }
        }

        public static AsyncResult PollJob(Guid guid)
        {
            AsyncResult result;
            jobs.TryGetValue(guid, out result);
            if (result != null)
            {
                result.LastPoll = DateTime.UtcNow;
            }
            return result;
        }
    }
}