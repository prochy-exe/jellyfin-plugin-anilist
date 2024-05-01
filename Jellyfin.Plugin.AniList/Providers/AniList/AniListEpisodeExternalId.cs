using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListEpisodeExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
            => item is Episode;

        public string ProviderName
            => "AniList";

        public string Key
            => ProviderNames.AniList;

        public ExternalIdMediaType? Type
            => ExternalIdMediaType.Episode;

        public string UrlFormatString
            => "https://anilist.co/anime/{0}/";
    }
}
