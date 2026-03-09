using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MacharPos.Infrastructure.Services
{
    public class SyncApiClient
    {
        private readonly HttpClient _client;

        public SyncApiClient(HttpClient client)
        {
            _client = client;
        }

        public async Task<bool> SendAsync(string endpoint, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(endpoint, content);

            return response.IsSuccessStatusCode;
        }
    }
}