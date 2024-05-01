using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AniList.Configuration;

//API v2
namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>, IHasOrder
    {
        private readonly IApplicationPaths _paths;
        private readonly ILogger<AniListSeasonProvider> _log;
        private readonly AniListApi _aniListApi;
        public int Order => -2;
        public string Name => "AniList";

        public AniListSeasonProvider(IApplicationPaths appPaths, ILogger<AniListSeasonProvider> logger)
        {
            _log = logger;
            _aniListApi = new AniListApi();
            _paths = appPaths;
        }

        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season>();
            Media media = null;
            PluginConfiguration config = Plugin.Instance.Configuration;

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                media = await _aniListApi.GetAnime(aid);
            }
            else
            {
                var seriesId = info.SeriesProviderIds.GetOrDefault(ProviderNames.AniList);
                seriesId = seriesId.ToString();
                _log.LogInformation("Fetching info for id {Name}", seriesId);
                MediaSearchResult seriesInfo;
                seriesInfo = await _aniListApi.GetAnime(seriesId);
                var searchName = seriesInfo.GetPreferredTitle(config.TitlePreference, "en");
                _log.LogInformation("Anime name obtained: {Name}", searchName);
                var seasonNumber = info.IndexNumber;
                if (seasonNumber != null && searchName != null)
                {
                    if (seasonNumber > 1)
                    {
                        searchName = string.Join(" ", searchName, seasonNumber.ToString());
                    }
                    _log.LogInformation("Start AniList season search... Searching({Name})", searchName);
                    MediaSearchResult msr;
                    msr = await _aniListApi.Search_GetSeries(searchName, cancellationToken);
                    if (msr != null)
                    {
                        media = await _aniListApi.GetAnime(msr.id.ToString());
                    }
                } else
                {
                  _log.LogInformation("Season is null for {Name}", searchName);  
                }
            }

            if (media != null)
            {
                result.HasMetadata = true;
                result.Item = media.ToSeason();
                result.People = media.GetPeopleInfo();
                result.Provider = ProviderNames.AniList;
                StoreImageUrl(media.id.ToString(), media.GetImageUrl(), "image");
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                Media aid_result = await _aniListApi.GetAnime(aid).ConfigureAwait(false);
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

        private void StoreImageUrl(string season, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "anilist", type, season + ".txt");
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            File.WriteAllText(path, url);
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = Plugin.Instance.GetHttpClient();

            return await httpClient.GetAsync(url).ConfigureAwait(false);
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
