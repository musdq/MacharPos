using System.Text;
using Microsoft.EntityFrameworkCore;
using MacharPos.Web.Data;

namespace MacharPos.Infrastructure.Sync
{
    /// <summary>
    /// Background service that periodically sends unsynced records
    /// from the local SyncQueue table to the Railway cloud API.
    /// </summary>
    public class SyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<SyncService> _logger;

        private const string CloudEndpoint = "https://macharpos-production.up.railway.app/api/sync";

        public SyncService(
            IServiceProvider serviceProvider,
            IHttpClientFactory httpFactory,
            ILogger<SyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Sync Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncPendingItems(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sync cycle failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task SyncPendingItems(CancellationToken token)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var pending = await db.SyncQueue
                .Where(x => !x.Synced)
                .OrderBy(x => x.CreatedAt)
                .Take(50)
                .ToListAsync(token);

            if (!pending.Any())
                return;

            var client = _httpFactory.CreateClient();

            foreach (var item in pending)
            {
                try
                {
                    var content = new StringContent(
                        item.PayloadJson,
                        Encoding.UTF8,
                        "application/json"
                    );

                    var response = await client.PostAsync(CloudEndpoint, content, token);

                    if (response.IsSuccessStatusCode)
                    {
                        item.Synced = true;
                        item.SyncedAt = DateTime.UtcNow;

                        _logger.LogInformation($"Synced {item.EntityType} {item.EntityId}");
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to sync {item.EntityType} {item.EntityId}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error syncing {item.EntityType} {item.EntityId}");
                }
            }

            await db.SaveChangesAsync(token);
        }
    }
}
