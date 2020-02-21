using System;
using System.Collections.Generic;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support;
using OpenQA.Selenium.Support.UI;
using MySqlConnector;
using MySql.Data.MySqlClient;
using RestSharp;
using RestSharp.Serializers;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace selenium_dotnet
{
    class Program
    {
        private static string api_key = "e94580eb-08eb-4e0e-9dc8-48d30ed67c3d";

        static int Main(string[] args)
        {
            return SingleBehance();
        }

        static string getProxy()
        {
            var client = new RestClient();
            var request = new RestRequest("https://proxybroker.craft-group.xyz/", Method.GET, DataFormat.Json);
            client.Timeout = 2 * 60000;
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Get<ProxyDTO>(request);
            if (response.ErrorMessage == null)
            {
                return response.Data.host + ":" + response.Data.port;
            }
            return null;
        }

        static Dictionary<string, bool> used_proxies = new Dictionary<string, bool>();
        static string getProxyLimit(int limit = 50, int sleep_ms = 5000)
        {
            string proxy_str = null;
            int counter = 0;
            while (proxy_str == null || counter < limit)
            {
                proxy_str = getProxy();
                counter++;
                if (proxy_str == null) 
                    Thread.Sleep(sleep_ms);
                else if (used_proxies.ContainsKey(proxy_str)) {
                    Thread.Sleep(sleep_ms);
                    proxy_str = null;
                } else {
                    used_proxies.Add(proxy_str, true);
                }
            }
            return proxy_str;
        }

        static List<Queue> getItems(int count = 10)
        {
            var client = new RestClient("http://behance/behance");
            var request = new RestRequest("get", Method.GET, DataFormat.Json);
            request.AddQueryParameter("api_key", api_key);
            request.AddQueryParameter("count", count.ToString());
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Get<List<Queue>>(request);
            return response.Data;
        }

        static int updateQueue(int id, int likes, int views)
        {
            var client = new RestClient("http://behance/behance");
            var request = new RestRequest("update", Method.GET, DataFormat.Json);
            request.AddQueryParameter("id", id.ToString());
            request.AddQueryParameter("likes", likes.ToString());
            request.AddQueryParameter("views", views.ToString());
            request.AddQueryParameter("api_key", api_key);
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Get<int>(request);
            return response.Data;
        }

        static int SingleBehance()
        {
            var queue = getItems(16);
            string path = "log.txt";
            Stopwatch watch = new Stopwatch();

            using (StreamWriter sw = new StreamWriter(path, false, Encoding.UTF8, 4096000))
            {
                while (queue.Count > 0)
                {
                    string proxy_str = getProxyLimit();
                    if (proxy_str == null)
                    {
                        Console.WriteLine("proxy not found, aborting");
                        return -1;
                    }

                    Console.WriteLine("proxy found - {0}", proxy_str);
                    var to_delete = new List<int>();
                    for (int i = 0; i < queue.Count; i++)
                    {
                        Console.WriteLine("{0} started", queue[i].id);
                        Console.WriteLine("Timer start", queue[i].id);
                        watch.Restart();
                        int views = queue[i].views_work;
                        int likes = queue[i].likes_work;
                        var url = queue[i].url;

                        if (likes > views)
                        {
                            views = likes;
                            queue[i].views_work = queue[i].likes_work;
                        }
                        Console.WriteLine("Started");
                        var options = new FirefoxOptions();
                        options.AddArgument("--headless");

                        Proxy proxy = new Proxy();
                        proxy.HttpProxy = proxy_str;
                        proxy.SslProxy = proxy_str;
                        options.Proxy = proxy;

                        var driver = new FirefoxDriver(options);
                        try
                        {
                            driver.Navigate().GoToUrl(url);
                            Actions action = new Actions(driver);

                            var elem = driver.FindElements(By.ClassName("Project-projectStat-6Y3"));
                            Console.WriteLine("Waiting for views\\likes\\comments block");
                            elem = (new WebDriverWait(driver, TimeSpan.FromSeconds(120))).
                                        Until(SeleniumExtras.WaitHelpers.ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.ClassName("Project-projectStat-6Y3")));

                            var visits = elem[1].GetAttribute("innerHTML");
                            int pFrom = visits.IndexOf(">") + ">".Length;
                            int pTo = visits.LastIndexOf("<");
                            String result = visits.Substring(pFrom, pTo - pFrom);

                            if (queue[i].likes_work > 0)
                            {

                                var like = driver.FindElement(By.ClassName("Appreciate-wrapper-9hi"));
                                var inner_before = like.GetAttribute("innerHTML");
                                action.MoveToElement(like);
                                Console.WriteLine("Waiting for like button");
                                like = (new WebDriverWait(driver, TimeSpan.FromMinutes(2))).
                                        Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.ClassName("Appreciate-wrapper-9hi")));
                                action.MoveToElement(like);
                                like.Click();
                                var inner_after = like.GetAttribute("innerHTML");
                                if (inner_after.Contains("Appreciate-count-") && !inner_after.Equals(inner_before))
                                {
                                    queue[i].likes_work -= 1;
                                }
                            }
                            Console.WriteLine("Done");
                            Thread.Sleep((new Random()).Next(5000, 10001));
                            Console.WriteLine("Done sleeping");
                            queue[i].views_work -= 1;
                            if (updateQueue(queue[i].id, queue[i].likes_work, queue[i].views_work) == 2)
                            {
                                to_delete.Add(i);
                            }
                            var z = 0;
                        }
                        catch (Exception e)
                        {
                            System.Console.WriteLine(e.Message);
                            Console.WriteLine("Error");
                            System.Console.WriteLine("Regenerating proxy");
                            proxy_str = getProxyLimit();
                            if (proxy_str == null)
                            {
                                Console.WriteLine("proxy not found, aborting");
                                return -1;
                            }

                            Console.WriteLine("proxy found - {0}", proxy_str);
                            //i--; //to repeat failed
                        }
                        finally
                        {
                            watch.Stop();
                            TimeSpan ts = watch.Elapsed;
                            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                                ts.Hours, ts.Minutes, ts.Seconds,
                                ts.Milliseconds / 10);
                            Console.WriteLine("RunTime " + elapsedTime);
                            sw.WriteLine("RunTime " + elapsedTime);
                            driver.Quit();
                            Console.WriteLine("Terminated");
                        }
                    }
                    to_delete.Reverse();
                    foreach (var item in to_delete)
                    {
                        queue.RemoveAt(item);
                    }
                    to_delete.Clear();
                }
                sw.Flush();
            }
            return 1;
        }

        static void ThreadedBehance()
        {
            // TODO: take data from queue using behance/behance/get?count=10
            var queue = new List<object>();
            queue.Add(new object());
            int threads_num = 10;
            var url = "https://www.behance.net/gallery/88190151/Landing-page-design-of-the-soap-bouquet-franchise";


            foreach (var item in queue)
            {
                var offset = 0;
                var limit = 100;
                //var proxy_list = getProxies(limit, offset);

                //string url = //item.url;
                int views = 11;//item.views;
                int likes = 11;//item.likes;

                if (likes > views)
                {
                    views = likes;
                }
                SemaphoreSlim maxThreads = new SemaphoreSlim(threads_num);
                Mutex proxy_mutex = new Mutex();
                Mutex views_mutex = new Mutex();
                Mutex likes_mutex = new Mutex();
                //TODO: mutex for decreasing views

                var tasks = new List<Task>();
                while (true)
                {
                    for (int i = 0; i < views; i++)
                    {
                        maxThreads.Wait();
                        tasks.Add(Task.Factory.StartNew(async () =>
                            {
                                Console.WriteLine("Task {0} started", Task.CurrentId);
                                var options = new FirefoxOptions();
                                options.AddArgument("--headless");

                                //proxy_mutex.WaitOne();
                                //if (proxy_list.Count <= 0)
                                //{
                                //    offset += limit;
                                //    proxy_list = getProxies(offset, limit);
                                //}
                                //var proxy_url = proxy_list[proxy_list.Count - 1];
                                //proxy_list.RemoveAt(proxy_list.Count - 1);
                                //proxy_mutex.ReleaseMutex();
                                //
                                //Proxy proxy = new Proxy();
                                //proxy.HttpProxy = proxy_url;
                                //proxy.SslProxy = proxy_url;
                                //options.Proxy = proxy;

                                var driver = new FirefoxDriver(options);

                                try
                                {
                                    driver.Navigate().GoToUrl(url);

                                    var elem = driver.FindElements(By.ClassName("Project-projectStat-6Y3"));
                                    while (elem.Count == 0)
                                    {
                                        elem = driver.FindElements(By.ClassName("Project-projectStat-6Y3"));
                                    }
                                    var visits = elem[1].GetAttribute("innerHTML");
                                    int pFrom = visits.IndexOf(">") + ">".Length;
                                    int pTo = visits.LastIndexOf("<");
                                    String result = visits.Substring(pFrom, pTo - pFrom);

                                    await Task.Delay((new Random()).Next(5000, 10001));

                                    views_mutex.WaitOne();
                                    --views;
                                    views_mutex.ReleaseMutex();

                                    elem = driver.FindElements(By.ClassName("Appreciate-wrapper-9hi"));
                                    likes_mutex.WaitOne();
                                    if (likes > 0)
                                    {
                                        var like = driver.FindElement(By.ClassName("Appreciate-wrapper-9hi"));
                                        Console.Write(like.GetAttribute("innerHTML"));
                                        like.Click();
                                        likes--;
                                    }
                                    likes_mutex.ReleaseMutex();
                                    Console.WriteLine("Task {0} is done", Task.CurrentId);
                                    maxThreads.Release();
                                }
                                catch (Exception e)
                                {
                                    likes_mutex.ReleaseMutex();
                                    Console.WriteLine("Task {0} error", Task.CurrentId);
                                    System.Console.WriteLine(e.Message);
                                }
                                finally
                                {
                                    driver.Quit();
                                    Console.WriteLine("Task {0} terminated", Task.CurrentId);
                                }
                            }, TaskCreationOptions.LongRunning));
                    }
                    Task.WaitAll(tasks.ToArray());
                    // TODO: add condition to continue if everything isnt 0
                    break;
                }
            }
        }
    }
}