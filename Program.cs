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
        private static string main_url = "https://betop.space/behance";

        static int Main(string[] args)
        {
            int count_items = 10;

            var status = mergeItems();
            if (status != 1) {
                Console.WriteLine("Merge failed. Aborting...");
                return -1;
            }
            var queue = getItems(count_items);
            return ThreadedBehance(queue);
        }

        static Dictionary<string, bool> used_proxies = new Dictionary<string, bool>();
        static Queue<string> proxies = new Queue<string>();
        static string getProxyLimit(int limit = 100, int sleep_ms = 5000)
        {
            string proxy_str = null;
            int counter = 0;
            while (counter < limit)
            {
                if (proxies.Count == 0)
                {
                    if (counter > 0)
                        Thread.Sleep(sleep_ms);
                    getProxy();
                    counter++;
                }
                if (proxies.Count == 0)
                {
                    continue;
                }
                else
                {
                    proxy_str = proxies.Dequeue();
                    if (used_proxies.ContainsKey(proxy_str))
                    {
                        proxy_str = null;
                    }
                    else
                    {
                        used_proxies.Add(proxy_str, true);
                        break;
                    }
                }
            }
            return proxy_str;
        }

        static void getProxy()
        {
            var client = new RestClient();
            var request = new RestRequest("https://proxybroker.craft-group.xyz/", Method.GET, DataFormat.Json);
            client.Timeout = 2 * 60000;
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Get<List<ProxyDTO>>(request);
            if (response.Data == null)
            {
                return;
            }
            foreach (var proxy in response.Data)
            {
                proxies.Enqueue(proxy.host + ":" + proxy.port);
            }
        }

        static List<QueueDTO> getItems(int count = 10)
        {
            var client = new RestClient(main_url);
            var request = new RestRequest("get", Method.GET, DataFormat.Json);
            request.AddQueryParameter("api_key", api_key);
            request.AddQueryParameter("count", count.ToString());
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Get<List<QueueDTO>>(request);
            return response.Data;
        }

        static int mergeItems()
        {
            var client = new RestClient(main_url);
            var request = new RestRequest("merge", Method.GET, DataFormat.Json);
            request.AddQueryParameter("api_key", api_key);
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Get<int>(request);
            return response.Data;
        }

        static int updateQueue(int id, int likes, int views)
        {
            var client = new RestClient(main_url);
            var request = new RestRequest("update", Method.GET, DataFormat.Json);
            request.AddQueryParameter("id", id.ToString());
            request.AddQueryParameter("likes", likes.ToString());
            request.AddQueryParameter("views", views.ToString());
            request.AddQueryParameter("api_key", api_key);
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Get<int>(request);
            return response.Data;
        }

        static (int code, bool to_delete) TaskFunction(QueueDTO item, string proxy_str, int num)
        {
            Console.WriteLine("{0} started", item.id);
            int views = item.views_work;
            int likes = item.likes_work;
            var url = item.url;
            bool to_delete = false;
            int code = -1;

            if (likes > views)
            {
                views = likes;
                item.views_work = item.likes_work;
            }
            Console.WriteLine("Started");
            var options = new FirefoxOptions();
            options.AddArgument("--headless");

            Proxy proxy = new Proxy();
            proxy.HttpProxy = proxy_str;
            proxy.SslProxy = proxy_str;
            options.Proxy = proxy;

            FirefoxDriver driver = null;
            try
            {
                driver = new FirefoxDriver(options);
                driver.Navigate().GoToUrl(url);
                Actions action = new Actions(driver);

                var elems = driver.FindElements(By.ClassName("Project-projectStat-6Y3"));

                Console.WriteLine("Waiting for views\\likes\\comments block");
                elems = (new WebDriverWait(driver, TimeSpan.FromMinutes(2))).
                            Until(SeleniumExtras.WaitHelpers.ExpectedConditions.VisibilityOfAllElementsLocatedBy(By.ClassName("Project-projectStat-6Y3")));
                action.MoveToElement(elems[1]);
                var visits = elems[1].GetAttribute("innerHTML");
                int pFrom = visits.IndexOf(">") + ">".Length;
                int pTo = visits.LastIndexOf("<");
                String result = visits.Substring(pFrom, pTo - pFrom);

                Console.WriteLine("Waiting for jQuery to finish");
                (new WebDriverWait(driver, TimeSpan.FromMinutes(2))).Until(d => (bool)(d as IJavaScriptExecutor).ExecuteScript("return window.jQuery != undefined && jQuery.active === 0"));

                if (item.likes_work > 0)
                {

                    var like = driver.FindElement(By.ClassName("Appreciate-wrapper-9hi"));
                    var inner_before = like.GetAttribute("innerHTML");
                    action.MoveToElement(like);
                    Console.WriteLine("Waiting for like button");
                    like = (new WebDriverWait(driver, TimeSpan.FromMinutes(2))).
                            Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(By.ClassName("Appreciate-wrapper-9hi")));
                    action.MoveToElement(like);
                    like.Click();

                    Console.WriteLine("Waiting for jQuery to finish");
                    (new WebDriverWait(driver, TimeSpan.FromMinutes(2))).Until(d => (bool)(d as IJavaScriptExecutor).ExecuteScript("return window.jQuery != undefined && jQuery.active === 0"));
                    var inner_after = like.GetAttribute("innerHTML");

                    if (inner_after.Contains("Appreciate-count-") && !inner_after.Equals(inner_before))
                    {
                        item.likes_work -= 1;
                    }
                }
                Console.WriteLine("Done");

                if (Task.CurrentId != null)
                {
                    Task.Delay((new Random()).Next(5000, 10001));
                }
                else
                {
                    Thread.Sleep((new Random()).Next(5000, 10001));
                }
                Console.WriteLine("Done sleeping");

                item.views_work -= 1;
                if (updateQueue(item.id, item.likes_work, item.views_work) == 2)
                {
                    to_delete = true;
                    code = num;
                }
                else
                {
                    code = 1;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error");
                System.Console.WriteLine(e.Message);
                code = -1;
                to_delete = false;
            }
            finally
            {
                if (driver != null)
                    driver.Quit();
                Console.WriteLine("Terminated");
            }
            return (code, to_delete);
        }

        static int SingleBehance(List<QueueDTO> queue, bool kill = true)
        {          
            while (queue.Count > 0)
            {
                string proxy_str = getProxyLimit();
                if (proxy_str == null)
                {
                    Console.WriteLine("Proxy not found. Aborting...");
                    return -1;
                }

                Console.WriteLine("proxy found - {0}", proxy_str);
                var to_delete = new List<int>();
                var tasks = new List<Task>();
                for (int i = 0; i < queue.Count; i++)
                {
                    var result = TaskFunction(queue[i], proxy_str, i);
                    if (result.code == -1)
                    {
                        proxy_str = getProxyLimit();
                        if (proxy_str == null)
                        {
                            Console.WriteLine("proxy not found, aborting");
                            return -1;
                        }
                        Console.WriteLine("proxy found - {0}", proxy_str);
                    }
                    else if (result.to_delete)
                    {
                        to_delete.Add(result.code);
                    }
                }
                to_delete.Reverse();
                foreach (var item in to_delete)
                {
                    queue.RemoveAt(item);
                }
                to_delete.Clear();
                if (kill)
                {
                    foreach (var process in Process.GetProcessesByName("firefox"))
                    {
                        process.Kill();
                    }
                }
            }
            return 1;
        }

        static int ThreadedBehance(List<QueueDTO> queue, int max_threads = 10, bool kill = true)
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

                var to_delete_mutex = new Mutex();
                var to_delete = new List<int>();
                var tasks = new List<Task>();
                var semaphore = new SemaphoreSlim(max_threads);

                for (int i = 0; i < queue.Count; i++)
                {
                    var j = i;
                    semaphore.Wait();
                    tasks.Add(Task.Factory.StartNew(() =>
                    {
                        Console.WriteLine("Task {0} started", Task.CurrentId);
                        var result = TaskFunction(queue[j], proxy_str, j);
                        Console.WriteLine("Task {0} finished", Task.CurrentId);
                        return result;
                    }, TaskCreationOptions.LongRunning).ContinueWith((task) =>
                    {
                        semaphore.Release();
                        if (task.Result.to_delete)
                        {
                            to_delete_mutex.WaitOne();
                            to_delete.Add(task.Result.code);
                            to_delete_mutex.ReleaseMutex();
                        }
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
                if (kill)
                {
                    foreach (var process in Process.GetProcessesByName("firefox"))
                    {
                        process.Kill();
                    }
                }
            }
            return 1;
        }
    }
}