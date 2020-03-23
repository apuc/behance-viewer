using RestSharp;
using System.Collections.Generic;
using selenium_dotnet.DTO;

namespace selenium_dotnet.Wrappers
{
    public class QueueWrapper
    {
        private string main_url = System.AppContext.GetData("main-url") as string;
        private string api_key = System.AppContext.GetData("api-key") as string;
        private List<QueueDTO> items;
        public int Count { get => items.Count; }
        public void GetItems(int count = 10)
        {
            var client = new RestClient(main_url);
            var request = new RestRequest("get", Method.GET, DataFormat.Json);
            request.AddQueryParameter("api_key", api_key);
            request.AddQueryParameter("count", count.ToString());
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Get<List<QueueDTO>>(request);
            items = response.Data;
        }

        public int MergeQueueItems()
        {
            
            var client = new RestClient(main_url);
            var request = new RestRequest("merge", Method.GET, DataFormat.Json);
            request.AddQueryParameter("api_key", api_key);
            request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
            var response = client.Get<int>(request);
            return response.Data;
        }

        private int UpdateQueue(int id, int likes, int views)
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

        public QueueDTO this[int index]
        {
            get => items[index];
        }
        public bool UpdateItem(int index)
        {
            var item = this.items[index];
            return UpdateQueue(item.id, item.likes_work, item.views_work) == 2;
        }

        public void RemoveAt(int index) {
            this.items.RemoveAt(index);
        }
    }
}