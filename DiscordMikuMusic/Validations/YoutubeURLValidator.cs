using System.ComponentModel.DataAnnotations;
using System.Net;

namespace DiscordMikuMusic.Validations
{
    public static class YoutubeURLValidator
    {
        private const string _requiredPrefix = "https://www.youtube.com/";
        public static bool Validate(string url)
        {
            if (url.StartsWith(_requiredPrefix) && Uri.TryCreate(url, new UriCreationOptions(), out Uri? validUri) )
            {
                return true;
            }
            return false;
        }
    }
}
