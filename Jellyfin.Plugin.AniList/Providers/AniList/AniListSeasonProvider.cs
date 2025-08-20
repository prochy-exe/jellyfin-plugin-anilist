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
    public class AniListSeasonProvider(AniListApi aniListApi, IHttpClientFactory httpClientFactory, ILogger<AniListSeriesProvider> logger) : IRemoteMetadataProvider<Season, SeasonInfo>, IHasOrder
    {
        public int Order => -2;
        public string Name => "AniList";

        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season>();
            var seasonIndex = info.IndexNumber ?? 1;
            var anilistId = info.SeriesProviderIds.GetOrDefault(ProviderNames.AniList).ToString(CultureInfo.InvariantCulture);
            PluginConfiguration config = Plugin.Instance.Configuration;
            if (seasonIndex == 0)
            {
                logger.LogInformation("Special index detected, skipping...");
                anilistId = "";
            }
            else if (seasonIndex == 1)
            {
                logger.LogInformation("Season 1 index detected, reusing series ID...");
            }
            else
            {
                Media seriesInfo = await aniListApi.GetAnime(anilistId, cancellationToken).ConfigureAwait(false);
                if (seriesInfo == null) {
                    anilistId = "";
                } else
                {
                    var seriesName = seriesInfo.GetPreferredTitle(config.TitlePreference, "en");
                    var searchQuery = string.Join(" ",
                        seriesName,
                        seasonIndex.ToString(CultureInfo.InvariantCulture) ?? string.Empty
                    );
                    logger.LogInformation("Searching for {Name}", searchQuery);
                    MediaSearchResult msr = await aniListApi.Search_GetSeries(searchQuery, cancellationToken);
                    anilistId = msr.id.ToString(CultureInfo.InvariantCulture);
                }
            }
            result.HasMetadata = true;
            result.Item = new Season
            {
                IndexNumber = seasonIndex,
                ProviderIds = new Dictionary<string, string>() { { ProviderNames.AniList, anilistId } }
            };
            result.Provider = ProviderNames.AniList;
            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                Media aid_result = await aniListApi.GetAnime(aid, cancellationToken).ConfigureAwait(false);
                if (aid_result != null)
                {
                    results.Add(aid_result.ToSearchResult());
                }
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<MediaSearchResult> name_results = await aniListApi.Search_GetSeries_list(searchInfo.Name, cancellationToken).ConfigureAwait(false);
                foreach (var media in name_results)
                {
                    results.Add(media.ToSearchResult());
                }
            }

            return results;
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var provider = new AniListAnimeImageProvider(aniListApi, httpClientFactory);
            return await provider.GetImageResponse(url, cancellationToken);
        }
    }
}
