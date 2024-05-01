﻿using System;
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
    public class AniListEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder
    {
        private readonly IApplicationPaths _paths;
        private readonly ILogger<AniListEpisodeProvider> _log;
        private readonly AniListApi _aniListApi;
        public int Order => -2;
        public string Name => "AniList";

        public AniListEpisodeProvider(IApplicationPaths appPaths, ILogger<AniListEpisodeProvider> logger)
        {
            _log = logger;
            _aniListApi = new AniListApi();
            _paths = appPaths;
        }

        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();
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
                var episodeNumber = info.ParentIndexNumber;
                if (episodeNumber != null && searchName != null)
                {
                    if (episodeNumber > 1)
                    {
                        searchName = string.Join(" ", searchName, episodeNumber.ToString());
                    }
                    _log.LogInformation("Start AniList Episode search... Searching({Name})", searchName);
                    MediaSearchResult msr;
                    msr = await _aniListApi.Search_GetSeries(searchName, cancellationToken);
                    if (msr != null)
                    {
                        media = await _aniListApi.GetAnime(msr.id.ToString());
                    }
                } else
                {
                  _log.LogInformation("Episode is null for {Name}", searchName);  
                }
            }

            if (media != null)
            {
                result.HasMetadata = true;
                result.Item = media.ToEpisode();
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

        private void StoreImageUrl(string episode, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "anilist", type, episode + ".txt");
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            File.WriteAllText(path, url);
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = Plugin.Instance.GetHttpClient();

            return await httpClient.GetAsync(url).ConfigureAwait(false);
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
