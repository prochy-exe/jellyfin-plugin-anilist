using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListEpisodeProvider(AniListApi aniListApi, IHttpClientFactory httpClientFactory) : IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        public int Order => -2;
        public string Name => "AniList";

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var provider = new AniListAnimeImageProvider(aniListApi, httpClientFactory);
            return await provider.GetImageResponse(url, cancellationToken);
        }

        public Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();
            result.HasMetadata = true;
            result.Item = new Episode
            {
                IndexNumber = info.IndexNumber ?? 1,
                ParentIndexNumber = info.ParentIndexNumber ?? 1,
                ProviderIds = new Dictionary<string, string>() { { ProviderNames.AniList, info.SeasonProviderIds.GetOrDefault(ProviderNames.AniList).ToString(CultureInfo.InvariantCulture) } }
            };
            result.Provider = ProviderNames.AniList;
            return Task.FromResult(result);
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }
    }
}
