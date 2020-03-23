using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using selenium_dotnet.DTO;

// TODO: https://stackoverflow.com/questions/29776607/python-selenium-webdriver-changing-proxy-settings-on-the-fly/48816584#48816584

namespace selenium_dotnet.Wrappers
{
    public class BehanceViewer
    {
        FirefoxDriver driver;

        public BehanceViewer()
        {
            var options = new FirefoxOptions();
            options.AddArgument("--headless");
            driver = new FirefoxDriver(options);
        }

        public void Close()
        {
            if (driver != null) driver.Quit();
            Console.WriteLine("Terminated driver");
        }

        private void Sleep(int sleep_ms)
        {
            if (Task.CurrentId != null)
            {
                Task.Delay(sleep_ms);
            }
            else
            {
                Thread.Sleep(sleep_ms);
            }
        }

        private void SetProxy(ProxyDTO proxy)
        {
            driver.Navigate().GoToUrl("about:config");
            var setupScript = @"var prefs = Components.classes[""@mozilla.org/preferences-service;1""].getService(Components.interfaces.nsIPrefBranch);
                                prefs.setIntPref(""network.proxy.type"", 1);
                                prefs.setCharPref(""network.proxy.http"", ""{0}"");
                                prefs.setIntPref(""network.proxy.http_port"", ""{1}"");
                                prefs.setCharPref(""network.proxy.ssl"", ""{0}"");
                                prefs.setIntPref(""network.proxy.ssl_port"", ""{1}"");
                                prefs.setCharPref(""network.proxy.ftp"", ""{0}"");
                                prefs.setIntPref(""network.proxy.ftp_port"", ""{1}"");";
            string.Format(setupScript, proxy.host, proxy.port);

            (driver as IJavaScriptExecutor).ExecuteScript(setupScript);

            this.Sleep(1000);
        }

        public int TaskFunction(QueueDTO item, ProxyDTO proxy = null)
        {
            int code = -1;

            Console.WriteLine("{0} started", item.id);
            int views = item.views_work;
            int likes = item.likes_work;
            var url = item.url;
            if (likes > views)
            {
                views = likes;
                item.views_work = item.likes_work;
            }
            Console.WriteLine("Started");
            try
            {
                if (proxy != null)
                    this.SetProxy(proxy);
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
                (new WebDriverWait(driver, TimeSpan.FromMinutes(2))).Until(
                                   d => (bool)(d as IJavaScriptExecutor).ExecuteScript("return window.jQuery != undefined && jQuery.active === 0"));

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
                    (new WebDriverWait(driver, TimeSpan.FromMinutes(2))).Until(
                                       d => (bool)(d as IJavaScriptExecutor).ExecuteScript("return window.jQuery != undefined && jQuery.active === 0"));
                    var inner_after = like.GetAttribute("innerHTML");

                    if (inner_after.Contains("Appreciate-count-") && !inner_after.Equals(inner_before))
                    {
                        item.likes_work -= 1;
                    }
                }
                Console.WriteLine("Done");

                this.Sleep((new Random()).Next(5000, 10001));
                Console.WriteLine("Done sleeping");

                item.views_work -= 1;
                code = 1;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error");
                System.Console.WriteLine(e.Message);
                code = -1;
            }
            return code;
        }
    }
}