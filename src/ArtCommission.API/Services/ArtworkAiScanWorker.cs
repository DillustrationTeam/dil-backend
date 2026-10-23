using ArtCommission.Infrastructure.Services;
namespace ArtCommission.API.Services;
public class ArtworkAiScanWorker(IServiceScopeFactory scopes, ILogger<ArtworkAiScanWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processed = await scope.ServiceProvider.GetRequiredService<ArtworkAiScanService>().ProcessNextAsync(stoppingToken);
                await Task.Delay(processed ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("Artwork scan worker paused after {ErrorType}.", ex.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
