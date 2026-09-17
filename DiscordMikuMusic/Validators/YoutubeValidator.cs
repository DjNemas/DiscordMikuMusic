namespace DiscordMikuMusic.Validators
{
    internal static class YoutubeValidator
    {
        public static bool IsPlaylist(in string url, out Uri? urlForYTDLP)
        {
            if (IsValid(url, out urlForYTDLP) &&
                (urlForYTDLP!.Query.Contains("list=") ||
                 urlForYTDLP.AbsolutePath.Equals("/playlist", StringComparison.OrdinalIgnoreCase)))
            {
                urlForYTDLP = RemoveQueryParam(urlForYTDLP, "v");
                return true;
            }
            return false;
        }

        public static bool IsVideo(in string url, out Uri? urlForYTDLP)
        {
            if (IsValid(url, out urlForYTDLP) && (urlForYTDLP!.Query.Contains("v=") || urlForYTDLP.Host.Contains("youtu.be")))
            {
                urlForYTDLP = RemoveQueryParam(urlForYTDLP, "list");
                return true;
            }
            return false;
        }

        private static bool IsValid(in string url, out Uri? urlForYTDLP)
        {
            var normalizedUrl = NormalizeUrl(url);

            return Uri.TryCreate(normalizedUrl, UriKind.Absolute, out urlForYTDLP) &&
                   (urlForYTDLP.Scheme == Uri.UriSchemeHttp || urlForYTDLP.Scheme == Uri.UriSchemeHttps) &&
                   (urlForYTDLP.Host.Contains("youtube.com") || urlForYTDLP.Host.Contains("youtu.be"));
        }

        private static Uri RemoveQueryParam(Uri uri, string paramName)
        {
            var queryParams = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(p => !p.StartsWith(paramName + "=", StringComparison.OrdinalIgnoreCase));

            return new UriBuilder(uri) { Query = string.Join("&", queryParams) }.Uri;
        }

        private static string NormalizeUrl(string url)
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + url;
            }
            return url;
        }
    }
}
