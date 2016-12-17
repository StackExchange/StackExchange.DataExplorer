using System;
using System.Collections.Generic;
using System.Linq;
using StackExchange.DataExplorer.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Threading;

namespace StackExchange.DataExplorer.Helpers
{
    // information about the query not important for execution 
    public class QueryContextData
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsText { get; set; }
        public QuerySet QuerySet { get; set; }
        public Revision Revision { get; set; }
    }

    public class AsyncQueryRunner
    {
        static AsyncQueryRunner()
        {
            var t = new Thread(FlushOldJobs)
            {
                IsBackground = true,
                Priority = ThreadPriority.Lowest
            };
            t.Start();
        }

        // expire any jobs that completed 120 seconds ago or less 
        private const int AutoExpireSeconds = 120;
        private const int ExpireNonPolledSeconds = 10; 

        private static readonly ConcurrentDictionary<Guid, AsyncResult> _jobs = new ConcurrentDictionary<Guid, AsyncResult>();
        private static readonly ConcurrentDictionary<string, List<Task>> _running = new ConcurrentDictionary<string, List<Task>>();

        public enum AsyncState
        {
            Pending,
            Success,
            Failure,
            Cancelled
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
            public ParsedQuery ParsedQuery { get; set; }
            public Site Site { get; set; }

            public QueryContextData QueryContextData { get; set; }

            public SqlCommand Command { get; set; }
            public bool Cancelled { get; set; }
            public bool HasOutput { get; set; }
        }

        public static AsyncResult Execute(ParsedQuery query, User user, Site site, QueryContextData context)
        {
            string userTag = user.IsAnonymous ? user.IPAddress : user.Id.ToString();

            List<Task> activeTasks;
            _running.TryGetValue(userTag, out activeTasks);
            if (activeTasks != null)
            {
                lock(activeTasks)
                {
                    if (activeTasks.Count(t => !t.IsCompleted) >= AppSettings.ConcurrentQueries)
                    {
                        throw new ApplicationException("Too many queries are running, you may only run " + AppSettings.ConcurrentQueries + " queries at a time");
                    }
                }
            }
            else
            {
                _running.TryAdd(userTag, new List<Task>());
                activeTasks = _running[userTag];
            }

            var result = new AsyncResult
            {
                JobId = Guid.NewGuid(),
                State = AsyncState.Pending,
                ParsedQuery = query,
                Site = site, 
                LastPoll = DateTime.UtcNow,
                QueryContextData = context
            };

            Task task = new Task(() =>
            {
                try
                {
                    result.QueryResults = QueryRunner.GetResults(query, site, user, result);

                    if (result.State == AsyncState.Pending)
                    {
                        result.State = AsyncState.Success;
                    }
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

            _jobs.TryAdd(result.JobId, result);
            task.Start();
            lock(activeTasks)
            {
                activeTasks.Add(task);
            }

            // give it some time to get results ... 
            Thread.Sleep(50);

            return result;
        }

        private static void FlushOldJobs()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000 * 10);

                    var expires = DateTime.UtcNow.AddSeconds(-AutoExpireSeconds);
                    foreach (var job in _jobs.Values.Where(j => j.CompletionDate != null && j.CompletionDate < expires).ToList())
                    {
                        AsyncResult ignore;
                        _jobs.TryRemove(job.JobId, out ignore);
                    }

                    foreach (var job in _jobs.Values.Where(j => !j.Task.IsCompleted && j.LastPoll < DateTime.UtcNow.AddSeconds(-ExpireNonPolledSeconds)))
                    {
                        AsyncResult result;
                        _jobs.TryGetValue(job.JobId, out result);
                        if (result != null)
                        {
                            result.Cancelled = true;
                            var cmd = result.Command;
                            cmd?.Cancel();
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
            _jobs.TryGetValue(guid, out result);
            if (result != null)
            {
                result.LastPoll = DateTime.UtcNow;
            }
            return result;
        }

        public static AsyncResult CancelJob(Guid guid)
        {
            AsyncResult result;
            _jobs.TryGetValue(guid, out result);

            if (result?.Command != null)
            {
                result.Command.Cancel();
                result.Cancelled = true;
                result.State = AsyncState.Cancelled;
            }

            return result;
        }
    }
}