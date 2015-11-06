using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace ConsoleApplication1.Manager
{
    public delegate void Output(string text);
    public class HtmlSearchManager
    {        
        private string _startUrl;
        private string _textToSearch;
        private int _numberOfUrlsToSearch;
        private LimitedConcurrencyLevelTaskScheduler _scheduler;
        private TaskFactory _taskFactory;
        private CancellationTokenSource _cancelTokenSource;
        private CancellationToken _cancelToken;       
        
        private Output _outputMethod;


        

        public HtmlSearchManager(string startUrl, string textToSearch, int numberOfUrlsToSearch, int threadNum, Output method)
        {
            _scheduler = new LimitedConcurrencyLevelTaskScheduler(threadNum);
            _startUrl = startUrl;
            _textToSearch = textToSearch;
            _numberOfUrlsToSearch = numberOfUrlsToSearch;            
            _cancelTokenSource = new CancellationTokenSource();
            _cancelToken = _cancelTokenSource.Token;
            _taskFactory = new TaskFactory(_cancelToken,
                TaskCreationOptions.None,
                TaskContinuationOptions.None,
                _scheduler);
            _outputMethod = method;
        }

        public void StopSearch()
        {
            _cancelTokenSource.Cancel();
        }
        public void StartSearch()
        {
            string startPageUrl = GetHTMLFromUrl(_startUrl);
            
            Regex textPattern = new Regex("(?i)"+_textToSearch);
            var startMatcher = textPattern.Match(startPageUrl);
            int textOccurencesOnStartPage = 0;
            while (startMatcher.Success)
            {
                textOccurencesOnStartPage++;
                startMatcher = startMatcher.NextMatch();
            }
            _outputMethod(String.Format("On start page were found {0} occurences\n", textOccurencesOnStartPage));

            var urlFromStartPage = GetUrlFromPage(startPageUrl, _numberOfUrlsToSearch);
            int urlCounter = 0;
            foreach (string url in urlFromStartPage)                
            {
                Task<int> t = _taskFactory.StartNew(() =>
                {
                    if (_cancelToken.IsCancellationRequested)
                        return 0;
                    string HTMLText = GetHTMLFromUrl(url);

                    Regex textToSearch = new Regex("(?i)"+_textToSearch);
                    var matcher = textToSearch.Match(HTMLText);
                    int textCounter = 0;
                    while (matcher.Success)
                    {
                        textCounter++;
                        matcher = matcher.NextMatch();  
                    }
                    return textCounter;
                });
                
                int i = urlCounter;
                urlCounter++;
                
                var awaiter = t.GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    try
                    {
                        _outputMethod(String.Format("{2}  Task for {0} finished.\nHas founded {1} occurence(s)\n", url, t.Result, i));
                    }
                    catch(Exception ex)
                    {
                        _outputMethod("Task was stopped\n");
                    }
                });
            }
        }

        private IEnumerable<string> GetUrlFromPage(string HTMLText, int maxNumberOfUrl)
        {
            LinkedList<string> urlList = new LinkedList<string>();
            Regex regex = new Regex(@"<a\s+href=""(http[^>\s]*)""\s+.*>.+?</a>");
            var anotherFoundedUrl = regex.Match(HTMLText);
            {
                int i = 0;
                while (anotherFoundedUrl.Success && i < maxNumberOfUrl)
                {
                    urlList.AddLast(anotherFoundedUrl.Groups[1].Value);
                    anotherFoundedUrl = anotherFoundedUrl.NextMatch();
                    i++;
                }
            }
            return urlList;
        }

        private string GetHTMLFromUrl(string url)
        {
            _outputMethod(String.Format("Starting download {0} ...\n",url));
            
            string result = "";
            WebRequest request;
            WebResponse response;
            //getting response            
            try
            {
                //for test purposes assume that url is valid
                request = WebRequest.Create(url);
                request.Credentials = CredentialCache.DefaultCredentials;
                request.ContentType = "text/html";
                response = request.GetResponse();
                Stream responseStream = response.GetResponseStream();
                StreamReader responseStreamReader = new StreamReader(responseStream);
                result = responseStreamReader.ReadToEnd();

                //close response and reader
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            _outputMethod(String.Format("Download of {0} finished.\n", url));
            return result;
        }
    }

    // Provides a task scheduler that ensures a maximum concurrency level while 
    // running on top of the thread pool.
    public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
    {
        // Indicates whether the current thread is processing work items.
        [ThreadStatic]
        private static bool _currentThreadIsProcessingItems;

        // The list of tasks to be executed 
        private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks)

        // The maximum concurrency level allowed by this scheduler. 
        private readonly int _maxDegreeOfParallelism;

        // Indicates whether the scheduler is currently processing work items. 
        private int _delegatesQueuedOrRunning = 0;

        // Creates a new instance with the specified degree of parallelism. 
        public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
        {
            if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        // Queues a task to the scheduler. 
        protected sealed override void QueueTask(Task task)
        {
            // Add the task to the list of tasks to be processed.  If there aren't enough 
            // delegates currently queued or running to process tasks, schedule another. 
            lock (_tasks)
            {
                _tasks.AddLast(task);
                if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                {
                    ++_delegatesQueuedOrRunning;
                    NotifyThreadPoolOfPendingWork();
                }
            }
        }

        // Inform the ThreadPool that there's work to be executed for this scheduler. 
        private void NotifyThreadPoolOfPendingWork()
        {
            ThreadPool.UnsafeQueueUserWorkItem(_ =>
            {
                // Note that the current thread is now processing work items.
                // This is necessary to enable inlining of tasks into this thread.
                _currentThreadIsProcessingItems = true;
                try
                {
                    // Process all available items in the queue.
                    while (true)
                    {
                        Task item;
                        lock (_tasks)
                        {
                            // When there are no more items to be processed,
                            // note that we're done processing, and get out.
                            if (_tasks.Count == 0)
                            {
                                --_delegatesQueuedOrRunning;
                                break;
                            }

                            // Get the next item from the queue
                            item = _tasks.First.Value;
                            _tasks.RemoveFirst();
                        }

                        // Execute the task we pulled out of the queue
                        base.TryExecuteTask(item);
                    }
                }
                // We're done processing items on the current thread
                finally { _currentThreadIsProcessingItems = false; }
            }, null);
        }

        // Attempts to execute the specified task on the current thread. 
        protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_currentThreadIsProcessingItems) return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued)
                // Try to run the task. 
                if (TryDequeue(task))
                    return base.TryExecuteTask(task);
                else
                    return false;
            else
                return base.TryExecuteTask(task);
        }

        // Attempt to remove a previously scheduled task from the scheduler. 
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
        }

        // Gets the maximum concurrency level supported by this scheduler. 
        public sealed override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }

        // Gets an enumerable of the tasks currently scheduled on this scheduler. 
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(_tasks, ref lockTaken);
                if (lockTaken) return _tasks;
                else throw new NotSupportedException();
            }
            finally
            {
                if (lockTaken) Monitor.Exit(_tasks);
            }
        }
    }
}
