using System.Collections.Generic;
using RestSharp;
using System.Threading;
using selenium_dotnet.DTO;

namespace selenium_dotnet.Wrappers
{
    public class ProxyWrapper 
    {
        private Dictionary<ProxyDTO, bool> used_proxies = new Dictionary<ProxyDTO, bool>();
        private Queue<ProxyDTO> proxies = new Queue<ProxyDTO>();

        public ProxyDTO dequeProxy(int max_tries = 100, int sleep_inbetween_tries_ms = 5000)
        {
            ProxyDTO proxy = null;
            int counter = 0;
            while (counter < max_tries)
            {
                if (proxies.Count == 0)
                {
                    if (counter > 0) 
                    {
                        Thread.Sleep(sleep_inbetween_tries_ms);
                    }
                    this.getProxy();
                    counter++;
                }
                if (proxies.Count == 0)
                {
                    continue;
                }
                else
                {
                    proxy = proxies.Dequeue();
                    if (used_proxies.ContainsKey(proxy))
                    {
                        proxy = null;
                    }
                    else
                    {
                        used_proxies.Add(proxy, true);
                        break;
                    }
                }
            }
            return proxy;
        }

        private void getProxy()
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
            response.Data.ForEach(item => proxies.Enqueue(item));
        }
    }
}