using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using CommandLine;
using selenium_dotnet.Wrappers;
using selenium_dotnet.DTO;

// TODO: make class object from TaskFunction

namespace selenium_dotnet
{
    class Program
    {
        class Options
        {
            [Option(Required = false)]
            public int Threads { get; set; } = 10;

            [Option(Required = false)]
            public int CountItems { get; set; } = 10;

            [Option(Required = false)]
            public bool Threaded { get; set; } = true;

            [Option(Required = false)]
            public bool Kill { get; set; } = false;

            [Option(Required = false)]
            public int RefreshQueuePeriod { get; set; } = 50;
        }

        private static ProxyWrapper proxy_wrapper = new ProxyWrapper();
        private static QueueWrapper queue = new QueueWrapper();

        private static int refresh_period = -1;
        private static int current_period = 0;
        static int Main(string[] args)
        {
            int count_items = -1;
            int threads = -1;
            bool threaded = false;
            bool kill = false;
            var result = Parser.Default.ParseArguments<Options>(args).
            WithParsed(options =>
            {
                count_items = options.CountItems;
                threads = options.Threads;
                kill = options.Kill;
                threaded = options.Threaded;
                refresh_period = options.RefreshQueuePeriod;
            }).WithNotParsed(errors => { return; });

            var status = RefreshQueue();
            if (status != 1)
            {
                return -1;
            }
            status = threaded ? ThreadedBehance(threads) : SingleBehance();
            if (kill)
            {
                foreach (var process in Process.GetProcessesByName("firefox"))
                {
                    process.Kill();
                }
            }
            return status;
        }

        static int RefreshQueue(int count_items=10)
        {
            var status = queue.MergeQueueItems();
            if (status != 1)
            {
                Console.WriteLine("Merge failed. Aborting...");
                return -1;
            }
            queue.GetItems(count_items);
            return status;
        }

        static int SingleBehance()
        {
            BehanceWrapper viewer = new BehanceWrapper();
            while (queue.Count > 0)
            {
                ProxyDTO proxy = proxy_wrapper.dequeProxy();
                if (proxy == null)
                {
                    Console.WriteLine("Proxy not found. Aborting...");
                    return -1;
                }

                Console.WriteLine("proxy found - {0}", proxy);
                var to_delete = new List<int>();
                var tasks = new List<Task>();
                for (int i = 0; i < queue.Count; i++)
                {
                    var result = viewer.TaskFunction(queue[i], proxy);
                    if (result == -1)
                    {
                        proxy = proxy_wrapper.dequeProxy();
                        if (proxy == null)
                        {
                            Console.WriteLine("proxy not found, aborting");
                            return -1;
                        }
                        Console.WriteLine("proxy found - {0}", proxy);
                    }
                    else
                    {
                        var to_delete_flag = queue.UpdateItem(i);
                        to_delete.Add(i);
                    }
                }
                to_delete.Reverse();
                foreach (var item in to_delete)
                {
                    queue.RemoveAt(item);
                }
                to_delete.Clear();

                current_period += 1;
                if (refresh_period >= current_period ||  queue.Count == 0) {
                    Console.WriteLine("Refershing queue");
                    var status = RefreshQueue();
                    if (status != 1)
                    {
                        return -1;
                    }
                    current_period = 0;
                }
            }
            viewer.Close();
            return 1;
        }

        static int ThreadedBehance(int max_threads = 10)
        {
            var loc_max_threads = max_threads > queue.Count ? queue.Count : max_threads;
            List<BehanceWrapper> viewers = new List<BehanceWrapper>(loc_max_threads);
            for (int i = 0; i < loc_max_threads; i++) {
                viewers.Add(new BehanceWrapper());
            }
            while (queue.Count > 0)
            {
                ProxyDTO proxy = proxy_wrapper.dequeProxy();
                if (proxy == null)
                {
                    Console.WriteLine("proxy not found, aborting");
                    return -1;
                }
                Console.WriteLine("proxy found - {0}", proxy);

                var to_delete_mutex = new Mutex();
                var to_delete = new List<int>();
                var tasks = new List<Task>();
                var semaphore = new SemaphoreSlim(loc_max_threads);

                for (int i = 0; i < queue.Count; i++)
                {
                    var j = i;
                    semaphore.Wait();
                    tasks.Add(Task.Factory.StartNew(() =>
                    {
                        var num = j % max_threads;
                        Console.WriteLine("Task {0} started", Task.CurrentId);
                        var result = viewers[num].TaskFunction(queue[j], proxy);
                        if (result == 1)
                        {
                            var to_delete_flag = queue.UpdateItem(j);
                            if (to_delete_flag)
                            {
                                to_delete_mutex.WaitOne();
                                to_delete.Add(j);
                                to_delete_mutex.ReleaseMutex();
                            }
                        }
                        Console.WriteLine("Task {0} finished", Task.CurrentId);
                    }, TaskCreationOptions.LongRunning).ContinueWith((task) =>
                    {
                        semaphore.Release();
                    }));
                }
                Task.WaitAll(tasks.ToArray());
                tasks.Clear();
                to_delete.Sort();
                to_delete.Reverse();
                foreach (var item in to_delete)
                {
                    queue.RemoveAt(item);
                }
                to_delete.Clear();

                current_period += 1;
                if (refresh_period >= current_period ||  queue.Count == 0) {
                    Console.WriteLine("Refershing queue");
                    var status = RefreshQueue();
                    if (status != 1)
                    {
                        return -1;
                    }
                    current_period = 0;
                }
            }
            viewers.ForEach(item => item.Close());
            return 1;
        }
    }
}
