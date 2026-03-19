using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace MacharPos.Infrastructure.Services
{
    public class InternetChecker
    {
        private readonly HttpClient _client;

        public InternetChecker(HttpClient client)
        {
            _client = client;
        }

        public async Task<bool> HasInternetAsync()
        {
            try
            {
                var response = await _client.GetAsync("https://www.google.com");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}