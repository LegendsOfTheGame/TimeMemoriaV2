// TimeMemoria/Services/NewsService.cs
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using TimeMemoria.Models;

namespace TimeMemoria.Services
{
    public sealed class NewsService : IDisposable
    {
        // ── Configuration ────────────────────────────────────────────────────
        // Replace with the actual GitHub raw URL once the pipeline repo is set.
        private const string LatestNewsUrl =
            "https://raw.githubusercontent.com/LegendsOfTheGame/ffxiv-latest-news/main/LatestNews.json";

        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);

        // ── State ─────────────────────────────────────────────────────────────
        private readonly HttpClient _http;
        private readonly IPluginLog _log;
        private readonly CancellationTokenSource _cts = new();

        private NewsEvent? _cached;
        private string? _fetchError;
        private DateTimeOffset _lastFetched = DateTimeOffset.MinValue;
        private bool _isFetching;

        // ── Public surface (UI reads these; never throws) ─────────────────────
        public NewsEvent? Latest => _cached;
        public string? FetchError => _fetchError;
        public bool IsLoading => _isFetching;

        public NewsService(IPluginLog log)
        {
            _log = log;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            // Kick off the first fetch immediately.
            _ = RefreshAsync();
        }

        /// <summary>
        /// Called from the UI draw loop. Triggers a background refresh when the
        /// cache has expired; never blocks the game thread.
        /// </summary>
        public void Poll()
        {
            if (_isFetching) return;
            if (DateTimeOffset.UtcNow - _lastFetched < RefreshInterval) return;
            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            _isFetching = true;
            try
            {
                var json = await _http.GetStringAsync(LatestNewsUrl, _cts.Token)
                                      .ConfigureAwait(false);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var parsed = JsonSerializer.Deserialize<NewsEvent>(json, options);
                _cached = parsed;
                _fetchError = null;
                _lastFetched = DateTimeOffset.UtcNow;
                _log.Debug("[NewsService] LatestNews.json refreshed.");
            }
            catch (OperationCanceledException)
            {
                // Disposed — do nothing.
            }
            catch (Exception ex)
            {
                _fetchError = ex.Message;
                _log.Warning($"[NewsService] Failed to fetch LatestNews.json: {ex.Message}");
            }
            finally
            {
                _isFetching = false;
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _http.Dispose();
        }
    }
}
