using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AniList.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

//API v2
namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>, IHasOrder
    {
        private readonly ILogger<AniListSeasonProvider> _log;
        private readonly AniListApi _aniListApi;
        public int Order => -2;
        public string Name => "AniList";

        public AniListSeasonProvider(ILogger<AniListSeasonProvider> logger)
        {
            _log = logger;
            _aniListApi = new AniListApi();
        }

        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season>();
            Media media = null;
            PluginConfiguration config = Plugin.Instance.Configuration;

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                media = await _aniListApi.GetAnime(aid, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var seriesId = info.SeriesProviderIds.GetOrDefault(ProviderNames.AniList);
                if (seriesId != null)
                {
                    var seasonIndex = info.IndexNumber;
                    if (seasonIndex != null)
                    {
                        if (seasonIndex == 1)
                        {
                            media = await _aniListApi.GetAnime(seriesId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                        }
                        else if (seasonIndex > 1)
                        {
                            Media seriesInfo = await _aniListApi.GetAnime(seriesId, cancellationToken).ConfigureAwait(false);
                            var seriesName = seriesInfo.GetPreferredTitle(config.TitlePreference, "en");
                            if (seriesName != null)
                            {
                                var searchQuery = string.Join(" ",
                                    seriesName,
                                    seasonIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
                                );
                                MediaSearchResult msr = await _aniListApi.Search_GetSeries(searchQuery, cancellationToken);
                                if (msr != null)
                                {
                                    media = await _aniListApi.GetAnime(seriesId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                                } 
                                else 
                                {
                                    _log.LogError("No series found for query {Name}", searchQuery);
                                }
                            }
                            else
                            {
                                _log.LogError("Series doesn't have a title!");
                            }
                        }
                        else
                        {
                            _log.LogInformation("Season is either a special season or an invalid index, skipping...");
                        }
                    }
                    else
                    {
                        _log.LogError("Season doesn't have a valid index!");
                    }
                }
                else
                {
                    _log.LogError("Series doesn't have a valid ID!");
                }
            }

            if (media != null)
            {
                result.HasMetadata = true;
                result.Item = media.ToSeason();
                result.People = media.GetPeopleInfo();
                result.Provider = ProviderNames.AniList;
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                Media aid_result = await _aniListApi.GetAnime(aid, cancellationToken).ConfigureAwait(false);
                if (aid_result != null)
                {
                    results.Add(aid_result.ToSearchResult());
                }
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<MediaSearchResult> name_results = await _aniListApi.Search_GetSeries_list(searchInfo.Name, cancellationToken).ConfigureAwait(false);
                foreach (var media in name_results)
                {
                    results.Add(media.ToSearchResult());
                }
            }

            return results;
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = Plugin.Instance.GetHttpClient();

            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
    }
}
