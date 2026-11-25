namespace ShortsGuard;

public enum BrowserShortsState
{
    Unavailable,
    Editing,
    OtherPage,
    YouTubeWithoutPath,
    YouTubeShorts
}

public static class BrowserShortsDetector
{
    public const string Chrome = "com.android.chrome";
    public const string ChromeAddressBarId = Chrome + ":id/url_bar";

    public static BrowserShortsState ClassifyAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return BrowserShortsState.Unavailable;
        address = address.Trim();
        if (address.Any(char.IsWhiteSpace) || address.Any(char.IsControl) || address.Contains('\\'))
            return BrowserShortsState.OtherPage;

        // The unfocused Chrome address bar can omit the scheme.
        if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            address = "https://" + address;

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || uri.UserInfo.Length != 0 ||
            uri.IdnHost is not ("youtube.com" or "www.youtube.com" or "m.youtube.com"))
            return BrowserShortsState.OtherPage;

        if (uri.AbsolutePath == "/shorts" || uri.AbsolutePath.StartsWith("/shorts/", StringComparison.Ordinal))
            return BrowserShortsState.YouTubeShorts;
        return uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0
            ? BrowserShortsState.YouTubeWithoutPath : BrowserShortsState.OtherPage;
    }
}
