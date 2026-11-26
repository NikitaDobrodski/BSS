namespace ShortsGuard;

public enum BrowserShortsState
{
    Unavailable,
    Editing,
    OtherPage,
    SiteWithoutPath,
    YouTubeShorts,
    VkClips,
    VkVideoClips,
    RutubeShorts,
    TikTokFeed
}

public static class BrowserShortsDetector
{
    public const string Chrome = "com.android.chrome";
    public const string Yandex = "com.yandex.browser";

    public static bool IsSupported(string? package) => package is Chrome or Yandex;

    public static bool ShouldBlock(BrowserShortsState state) => state is BrowserShortsState.YouTubeShorts or
        BrowserShortsState.VkClips or BrowserShortsState.VkVideoClips or BrowserShortsState.RutubeShorts or
        BrowserShortsState.TikTokFeed;

    internal static BrowserShortsState ClassifyYandexAddress(string? description)
    {
        // Умная строка сообщает адрес или пару «домен заголовок страницы».
        const string russianLabel = "Умная адресная строка. ";
        const string englishLabel = "SmartBox. ";
        if (description is null) return BrowserShortsState.Unavailable;
        if (description.StartsWith(russianLabel, StringComparison.Ordinal))
            description = description[russianLabel.Length..];
        else if (description.StartsWith(englishLabel, StringComparison.Ordinal))
            description = description[englishLabel.Length..];
        else
            return ClassifyAddress(description);

        var address = description.AsSpan().Trim();
        int separator = address.IndexOfAny(" \n\t\r");
        return ClassifyAddress((separator < 0 ? address : address[..separator]).ToString());
    }

    public static BrowserShortsState ClassifyAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return BrowserShortsState.Unavailable;
        address = address.Trim();
        if (address.Any(char.IsWhiteSpace) || address.Any(char.IsControl) || address.Contains('\\'))
            return BrowserShortsState.OtherPage;

        // Браузер может передать адрес без схемы.
        if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            address = "https://" + address;

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || uri.UserInfo.Length != 0)
            return BrowserShortsState.OtherPage;

        string path = uri.AbsolutePath;
        var unmatched = path == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0
            ? BrowserShortsState.SiteWithoutPath : BrowserShortsState.OtherPage;
        return uri.IdnHost switch
        {
            "youtube.com" or "www.youtube.com" or "m.youtube.com" =>
                IsSection(path, "/shorts") ? BrowserShortsState.YouTubeShorts : unmatched,
            "vk.com" or "www.vk.com" or "m.vk.com" =>
                IsVkClip(path) ? BrowserShortsState.VkClips : unmatched,
            "vkvideo.ru" or "www.vkvideo.ru" or "m.vkvideo.ru" =>
                IsVkClip(path) ? BrowserShortsState.VkVideoClips : unmatched,
            "rutube.ru" or "www.rutube.ru" or "m.rutube.ru" =>
                IsRutubeShorts(path) ? BrowserShortsState.RutubeShorts : unmatched,
            "tiktok.com" or "www.tiktok.com" or "m.tiktok.com" =>
                IsTikTokFeed(path) ? BrowserShortsState.TikTokFeed : unmatched,
            _ => BrowserShortsState.OtherPage
        };
    }

    private static bool IsSection(string path, string section) =>
        path == section || (path.Length > section.Length && path[section.Length] == '/' &&
            path.StartsWith(section, StringComparison.Ordinal));

    private static bool IsVkClip(string path)
    {
        if (IsSection(path, "/clips")) return true;
        if (!path.StartsWith("/clip", StringComparison.Ordinal)) return false;
        var id = path.AsSpan(5).TrimEnd('/');
        if (id.StartsWith("-", StringComparison.Ordinal)) id = id[1..];
        int separator = id.IndexOf('_');
        return separator > 0 && IsDigits(id[..separator]) && IsDigits(id[(separator + 1)..]);
    }

    private static bool IsRutubeShorts(string path)
    {
        if (IsSection(path, "/shorts") || IsSection(path, "/feeds/shorts")) return true;
        string[] segments = path.TrimEnd('/').Split('/');
        return segments is ["", "u" or "channel", { Length: > 0 }, "shorts"];
    }

    private static bool IsTikTokFeed(string path)
    {
        path = path.TrimEnd('/');
        if (path is "/foryou" or "/following") return true;
        string[] segments = path.Split('/');
        return segments is ["", { Length: > 1 } profile, "video", var id] &&
            profile[0] == '@' && IsDigits(id);
    }

    private static bool IsDigits(ReadOnlySpan<char> value) =>
        !value.IsEmpty && value.IndexOfAnyExceptInRange('0', '9') < 0;
}
