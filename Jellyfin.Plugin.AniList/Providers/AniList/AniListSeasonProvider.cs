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
            Media media = null;
            PluginConfiguration config = Plugin.Instance.Configuration;

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                media = await aniListApi.GetAnime(aid, cancellationToken).ConfigureAwait(false);
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
                            media = await aniListApi.GetAnime(seriesId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                        }
                        else if (seasonIndex > 1)
                        {
                            Media seriesInfo = await aniListApi.GetAnime(seriesId, cancellationToken).ConfigureAwait(false);
                            var seriesName = seriesInfo.GetPreferredTitle(config.TitlePreference, "en");
                            if (seriesName != null)
                            {
                                var searchQuery = string.Join(" ",
                                    seriesName,
                                    seasonIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
                                );
                                MediaSearchResult msr = await aniListApi.Search_GetSeries(searchQuery, cancellationToken);
                                if (msr != null)
                                {
                                    media = await aniListApi.GetAnime(seriesId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                                } 
                                else 
                                {
                                    logger.LogError("No series found for query {Name}", searchQuery);
                                }
                            }
                            else
                            {
                                logger.LogError("Series doesn't have a title!");
                            }
                        }
                        else
                        {
                            logger.LogInformation("Season is either a special season or an invalid index, skipping...");
                        }
                    }
                    else
                    {
                        logger.LogError("Season doesn't have a valid index!");
                    }
                }
                else
                {
                    logger.LogError("Series doesn't have a valid ID!");
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
            var httpClient = httpClientFactory.CreateClient();

            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
    }
}
