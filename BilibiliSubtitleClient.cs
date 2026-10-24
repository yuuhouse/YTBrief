using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

sealed record BilibiliVideo(string Bvid, int Page);
sealed record BilibiliSubtitleResult(
    string Title,
    string Uploader,
    string Duration,
    string Language,
    int Part,
    string? Transcript,
    string? RawSubtitleJson,
    string? AudioUrl);

internal static class BilibiliSubtitleParsing
{
    private static readonly int[] WbiPermutation = [
        46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35,
        27, 43, 5, 49, 33, 9, 42, 19, 29, 28, 14, 39, 12, 38, 41, 13,
        37, 48, 7, 16, 24, 55, 40, 61, 26, 17, 0, 1, 60, 51, 30, 4,
        22, 25, 54, 21, 56, 59, 6, 63, 57, 62, 11, 36, 20, 34, 44, 52
    ];

    public static bool TryParseVideoUrl(string value, out BilibiliVideo video)
    {
        video = new BilibiliVideo("", 1);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || !(uri.Host.Equals("bilibili.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".bilibili.com", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var match = Regex.Match(uri.AbsolutePath, @"/video/(BV[0-9A-Za-z]+)", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var page = 1;
        var pageValue = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(parts => parts.Length == 2 && parts[0] == "p");
        if (pageValue is not null && int.TryParse(Uri.UnescapeDataString(pageValue[1]), out var requestedPage) && requestedPage > 0)
        {
            page = requestedPage;
        }

        video = new BilibiliVideo(match.Groups[1].Value, page);
        return true;
    }

    public static int LanguageRank(string language, string displayName)
    {
        var value = (language + " " + displayName).ToLowerInvariant();
        if (value.Contains("zh-hant") || value.Contains("zh-tw") || value.Contains("繁體") || value.Contains("繁体")) return 0;
        if (value.Contains("zh-hans") || value.Contains("zh-cn") || value.Contains("zh") || value.Contains("中文") || value.Contains("漢語") || value.Contains("汉语")) return 1;
        return 2;
    }

    public static string FormatTimestamp(double seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}.{span.Milliseconds:000}";
    }

    public static string ParseSubtitleTranscript(JsonElement subtitleDocument)
    {
        if (!subtitleDocument.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("B 站回傳的字幕格式無法辨識。");
        }

        var lines = new List<string>();
        foreach (var segment in body.EnumerateArray())
        {
            if (!segment.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String)
            {
                continue;
            }
            var text = content.GetString()?.Trim();
            if (string.IsNullOrEmpty(text)) continue;
            var start = segment.TryGetProperty("from", out var from) ? from.GetDouble() : 0;
            var end = segment.TryGetProperty("to", out var to) ? to.GetDouble() : start;
            lines.Add($"[{FormatTimestamp(start)} --> {FormatTimestamp(end)}] {text}");
        }
        return string.Join(Environment.NewLine, lines);
    }

    public static bool IsAllowedAssetUri(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
        var host = uri.Host;
        return host.Equals("bilibili.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".bilibili.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("hdslb.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".hdslb.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("bilivideo.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".bilivideo.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("bilivideo.cn", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".bilivideo.cn", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ParseCookieHeader(string content)
    {
        var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
        var rawCookieLine = lines
            .Select(line => line.Trim())
            .FirstOrDefault(line => !line.StartsWith('#') && !line.Contains('\t') && line.Contains('='));
        if (rawCookieLine is not null)
        {
            if (rawCookieLine.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase)) rawCookieLine = rawCookieLine[7..].Trim();
            return rawCookieLine.Contains('=') ? rawCookieLine : null;
        }

        var cookies = new List<string>();
        foreach (var line in lines)
        {
            var value = line.StartsWith("#HttpOnly_", StringComparison.Ordinal) ? line[10..] : line;
            if (value.StartsWith('#') || !value.Contains('\t')) continue;
            var fields = value.Split('\t');
            if (fields.Length < 7) continue;
            var domain = fields[0].TrimStart('.');
            if (!(domain.Equals("bilibili.com", StringComparison.OrdinalIgnoreCase)
                || domain.EndsWith(".bilibili.com", StringComparison.OrdinalIgnoreCase))) continue;
            if (fields[5].Length == 0) continue;
            cookies.Add($"{fields[5]}={fields[6]}");
        }
        return cookies.Count > 0 ? string.Join("; ", cookies) : null;
    }

    public static string CreateWbiSignature(IDictionary<string, string> parameters, string mixinKey, long timestamp)
    {
        var query = new SortedDictionary<string, string>(parameters, StringComparer.Ordinal)
        {
            ["wts"] = timestamp.ToString(CultureInfo.InvariantCulture)
        };
        var encoded = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(Regex.Replace(pair.Value, "[!'()*]", ""))}"));
        var digest = MD5.HashData(Encoding.UTF8.GetBytes(encoded + mixinKey));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    public static string GetMixinKey(string imageKey, string subKey)
    {
        var source = imageKey + subKey;
        return string.Concat(WbiPermutation.Take(32).Where(index => index < source.Length).Select(index => source[index]));
    }
}

static class BilibiliSubtitleClient
{
    private static readonly HttpClient Client = CreateClient();
    private static readonly string? CookieHeader = LoadCookieHeader();

    public static bool TryParseVideoUrl(string value, out BilibiliVideo video)
        => BilibiliSubtitleParsing.TryParseVideoUrl(value, out video);

    public static async Task<BilibiliSubtitleResult> FetchSubtitleAsync(BilibiliVideo video)
    {
        Console.WriteLine("B 站步驟 1/4：查詢影片與分 P 資訊...");
        var metadataUri = $"https://api.bilibili.com/x/web-interface/view?bvid={Uri.EscapeDataString(video.Bvid)}";
        using var metadata = await GetJsonAsync(metadataUri, "okhttp/4.9.3");
        EnsureApiSuccess(metadata.RootElement, "讀取 B 站影片資訊");
        var data = metadata.RootElement.GetProperty("data");
        var pages = data.GetProperty("pages");
        if (pages.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("B 站影片沒有可處理的分 P 資訊。");
        }
        if (video.Page > pages.GetArrayLength())
        {
            throw new InvalidOperationException($"這支影片只有 {pages.GetArrayLength()} 個分 P，網址指定了第 {video.Page} P。");
        }

        var page = pages[video.Page - 1];
        var cid = page.GetProperty("cid").GetInt64();
        var aid = data.GetProperty("aid").GetInt64();
        var wbiParameters = new Dictionary<string, string>
        {
            ["aid"] = aid.ToString(CultureInfo.InvariantCulture),
            ["cid"] = cid.ToString(CultureInfo.InvariantCulture)
        };
        Console.WriteLine("B 站步驟 2/4：取得字幕 API 簽名資料...");
        var mixinKey = await FetchWbiMixinKeyAsync();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = BilibiliSubtitleParsing.CreateWbiSignature(wbiParameters, mixinKey, timestamp);
        var playerUri = $"https://api.bilibili.com/x/player/wbi/v2?aid={aid}&cid={cid}&wts={timestamp}&w_rid={signature}";
        Console.WriteLine("B 站步驟 3/4：查詢字幕清單...");
        using var player = await GetJsonAsync(playerUri);
        EnsureApiSuccess(player.RootElement, "讀取 B 站字幕清單");

        var playerData = player.RootElement.GetProperty("data");
        if (playerData.TryGetProperty("cid", out var returnedCid) && returnedCid.TryGetInt64(out var returnedCidValue) && returnedCidValue != cid)
        {
            throw new InvalidOperationException("B 站字幕 API 回傳的影片分 P 不符，為避免摘要錯誤字幕，已停止處理。");
        }
        var subtitleInfo = playerData.GetProperty("subtitle");
        var tracks = subtitleInfo.TryGetProperty("subtitles", out var subtitleTracks)
            ? subtitleTracks.EnumerateArray().ToArray()
            : [];
        var selected = tracks
            .OrderBy(track => BilibiliSubtitleParsing.LanguageRank(GetString(track, "lan"), GetString(track, "lan_doc")))
            .FirstOrDefault(track => track.TryGetProperty("subtitle_url", out var subtitleUrl)
                && subtitleUrl.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(subtitleUrl.GetString()));
        string? rawSubtitleJson = null;
        var selectedLanguage = "";
        if (selected.ValueKind != JsonValueKind.Undefined)
        {
            var subtitleAddress = selected.GetProperty("subtitle_url").GetString()!;
            if (subtitleAddress.StartsWith("//", StringComparison.Ordinal))
            {
                subtitleAddress = "https:" + subtitleAddress;
            }
            else if (subtitleAddress.StartsWith("/", StringComparison.Ordinal))
            {
                subtitleAddress = "https://api.bilibili.com" + subtitleAddress;
            }
            if (!BilibiliSubtitleParsing.IsAllowedAssetUri(subtitleAddress))
            {
                throw new InvalidOperationException("B 站字幕網址不是允許的 HTTPS B 站媒體網域，已停止下載。");
            }

            Console.WriteLine("B 站步驟 4/4：下載字幕內容...");
            using var subtitle = await GetJsonAsync(subtitleAddress);
            var transcript = BilibiliSubtitleParsing.ParseSubtitleTranscript(subtitle.RootElement);
            rawSubtitleJson = subtitle.RootElement.GetRawText();
            selectedLanguage = GetString(selected, "lan_doc") is { Length: > 0 } language
                ? language
                : GetString(selected, "lan");
            if (!string.IsNullOrWhiteSpace(transcript))
            {
                return BuildResult(transcript, rawSubtitleJson, null);
            }
        }

        string? audioUrl = null;
        if (selected.ValueKind == JsonValueKind.Undefined || rawSubtitleJson is not null)
        {
            Console.WriteLine("B 站沒有可用字幕，嘗試取得音訊...");
            audioUrl = await FetchAudioUrlAsync(video.Bvid, cid);
            if (audioUrl is null)
            {
                var loginRequired = subtitleInfo.TryGetProperty("need_login_subtitle", out var needLogin)
                    && needLogin.ValueKind == JsonValueKind.True;
                throw new InvalidOperationException(loginRequired
                    ? "B 站字幕需要登入，而且目前無字幕可用時的音訊備援也無法取得。請確認影片可公開播放。"
                    : "B 站沒有回傳可用字幕，且音訊備援無法取得。影片可能需要登入或受到存取限制。");
            }
        }

        return BuildResult(null, null, audioUrl);

        BilibiliSubtitleResult BuildResult(string? transcript, string? subtitleJson, string? fallbackAudioUrl)
        {
            var title = GetString(data, "title");
            var uploader = data.TryGetProperty("owner", out var owner) ? GetString(owner, "name") : "";
            var durationSeconds = page.TryGetProperty("duration", out var durationValue) ? durationValue.GetInt32() : 0;
            var partName = GetString(page, "part");
            var fullTitle = string.IsNullOrWhiteSpace(partName) || pages.GetArrayLength() == 1 ? title : $"{title} - {partName}";
            return new BilibiliSubtitleResult(
                fullTitle,
                uploader,
                TimeSpan.FromSeconds(durationSeconds).ToString(durationSeconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss", CultureInfo.InvariantCulture),
                selectedLanguage,
                video.Page,
                transcript,
                subtitleJson,
                fallbackAudioUrl);
        }
    }

    public static async Task DownloadAudioAsync(string address, string path)
    {
        if (!BilibiliSubtitleParsing.IsAllowedAssetUri(address))
        {
            throw new InvalidOperationException("B 站音訊網址不是允許的 HTTPS B 站媒體網域，已停止下載。");
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var response = await Client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        await using var output = File.Create(path);
        await input.CopyToAsync(output, timeout.Token);
    }

    private static async Task<string?> FetchAudioUrlAsync(string bvid, long cid)
    {
        var address = $"https://api.bilibili.com/x/player/playurl?bvid={Uri.EscapeDataString(bvid)}&cid={cid}&qn=16&fnval=16&fourk=1";
        using var document = await GetJsonAsync(address);
        EnsureApiSuccess(document.RootElement, "取得 B 站音訊資訊");
        if (!document.RootElement.TryGetProperty("data", out var data)) return null;

        if (data.TryGetProperty("dash", out var dash)
            && dash.TryGetProperty("audio", out var audioTracks)
            && audioTracks.ValueKind == JsonValueKind.Array)
        {
            var audio = audioTracks.EnumerateArray()
                .OrderByDescending(item => item.TryGetProperty("bandwidth", out var bandwidth) ? bandwidth.GetInt32() : 0)
                .FirstOrDefault();
            if (audio.ValueKind != JsonValueKind.Undefined)
            {
                var audioUrl = GetString(audio, "baseUrl");
                if (string.IsNullOrWhiteSpace(audioUrl)) audioUrl = GetString(audio, "base_url");
                if (!string.IsNullOrWhiteSpace(audioUrl)) return audioUrl;
            }
        }

        if (data.TryGetProperty("durl", out var directTracks) && directTracks.ValueKind == JsonValueKind.Array)
        {
            var first = directTracks.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined) return GetString(first, "url");
        }
        return null;
    }

    private static async Task<string> FetchWbiMixinKeyAsync()
    {
        using var nav = await GetJsonAsync("https://api.bilibili.com/x/web-interface/nav");
        EnsureApiSuccess(nav.RootElement, "讀取 B 站 WBI 簽名資料");
        var imageUrl = nav.RootElement.GetProperty("data").GetProperty("wbi_img").GetProperty("img_url").GetString()!;
        var subUrl = nav.RootElement.GetProperty("data").GetProperty("wbi_img").GetProperty("sub_url").GetString()!;
        var imageKey = Path.GetFileNameWithoutExtension(new Uri(imageUrl).AbsolutePath);
        var subKey = Path.GetFileNameWithoutExtension(new Uri(subUrl).AbsolutePath);
        return BilibiliSubtitleParsing.GetMixinKey(imageKey, subKey);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/132.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Referrer = new Uri("https://www.bilibili.com/");
        client.DefaultRequestHeaders.Add("Origin", "https://www.bilibili.com");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        return client;
    }

    private static string? LoadCookieHeader()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("BILIBILI_COOKIE");
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) return BilibiliSubtitleParsing.ParseCookieHeader(fromEnvironment);

        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "bilibili_cookies.txt"),
            Path.Combine(AppContext.BaseDirectory, "bilibili_cookies.txt")
        }.Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            var parsed = BilibiliSubtitleParsing.ParseCookieHeader(File.ReadAllText(path));
            if (parsed is not null) return parsed;
        }
        return null;
    }

    private static async Task<JsonDocument> GetJsonAsync(string address, string? userAgent = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.ParseAdd(userAgent);
        }
        if (CookieHeader is not null && Uri.TryCreate(address, UriKind.Absolute, out var uri)
            && (uri.Host.Equals("api.bilibili.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("www.bilibili.com", StringComparison.OrdinalIgnoreCase)))
        {
            request.Headers.TryAddWithoutValidation("Cookie", CookieHeader);
        }
        using var response = await Client.SendAsync(request, timeout.Token);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException("B 站拒絕了字幕請求（HTTP 403）。影片可能需要登入或受到存取限制。");
        }
        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new InvalidOperationException($"B 站風控拒絕 API 請求（HTTP 412，端點 {new Uri(address).AbsolutePath}）。這不是字幕不存在；稍後重試，或確認 B 站頁面在瀏覽器可正常觀看。此程式目前未使用瀏覽器登入狀態。");
        }
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        return await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
    }

    private static void EnsureApiSuccess(JsonElement root, string action)
    {
        if (root.TryGetProperty("code", out var code) && code.TryGetInt32(out var value) && value != 0)
        {
            var message = root.TryGetProperty("message", out var messageValue) ? messageValue.GetString() : null;
            if (value == -101)
            {
                throw new InvalidOperationException($"{action}需要 B 站登入。請將登入後匯出的 Netscape Cookie 存成專案根目錄的 bilibili_cookies.txt，再重試。Cookie 檔只保留在本機，切勿分享。");
            }
            throw new InvalidOperationException($"{action}失敗（B 站代碼 {value}）：{message ?? "未知錯誤"}");
        }
    }

    private static string GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

}
