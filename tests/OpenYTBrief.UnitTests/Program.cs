using System.Text.Json;

var passed = 0;
var failed = 0;

void Check(string name, bool condition)
{
    if (condition)
    {
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    else
    {
        failed++;
        Console.WriteLine($"FAIL {name}");
    }
}

Check("parses Bilibili BV URL and part query", BilibiliSubtitleParsing.TryParseVideoUrl(
    "https://www.bilibili.com/video/BV185nh6dEPV/?p=3", out var parsed)
    && parsed.Bvid == "BV185nh6dEPV" && parsed.Page == 3);
Check("defaults Bilibili part to one", BilibiliSubtitleParsing.TryParseVideoUrl(
    "https://www.bilibili.com/video/BV185nh6dEPV/", out var defaultPart) && defaultPart.Page == 1);
Check("rejects lookalike host", !BilibiliSubtitleParsing.TryParseVideoUrl(
    "https://bilibili.com.example.org/video/BV185nh6dEPV/", out _));
Check("rejects non-Bilibili URL", !BilibiliSubtitleParsing.TryParseVideoUrl(
    "https://www.youtube.com/watch?v=BV185nh6dEPV", out _));
Check("rejects invalid Bilibili path", !BilibiliSubtitleParsing.TryParseVideoUrl(
    "https://www.bilibili.com/read/cv123", out _));
Check("allows HTTPS Bilibili CDN assets", BilibiliSubtitleParsing.IsAllowedAssetUri(
    "https://upos-sz-mirrorcos.bilivideo.com/audio.m4s"));
Check("rejects non-Bilibili asset hosts", !BilibiliSubtitleParsing.IsAllowedAssetUri(
    "https://example.org/audio.m4s"));
Check("rejects non-HTTPS asset URLs", !BilibiliSubtitleParsing.IsAllowedAssetUri(
    "http://upos-sz-mirrorcos.bilivideo.com/audio.m4s"));
Check("parses a raw Bilibili Cookie header", BilibiliSubtitleParsing.ParseCookieHeader("SESSDATA=abc; bili_jct=xyz") == "SESSDATA=abc; bili_jct=xyz");
Check("extracts only Bilibili cookies from Netscape format", BilibiliSubtitleParsing.ParseCookieHeader(
    "# Netscape HTTP Cookie File\n.bilibili.com\tTRUE\t/\tTRUE\t0\tSESSDATA\tsecret\n.example.org\tTRUE\t/\tTRUE\t0\tother\tignored") == "SESSDATA=secret");
Check("prefers traditional Chinese subtitle", BilibiliSubtitleParsing.LanguageRank("zh-Hant", "繁體中文")
    < BilibiliSubtitleParsing.LanguageRank("zh-Hans", "簡體中文"));
Check("prefers Chinese to other languages", BilibiliSubtitleParsing.LanguageRank("zh", "中文")
    < BilibiliSubtitleParsing.LanguageRank("en", "English"));
Check("formats timestamp with hours and milliseconds", BilibiliSubtitleParsing.FormatTimestamp(3661.234) == "01:01:01.234");
Check("clamps negative timestamp to zero", BilibiliSubtitleParsing.FormatTimestamp(-2) == "00:00:00.000");

using (var subtitleJson = JsonDocument.Parse("""
    {"body":[{"from":1.25,"to":2.5,"content":"  第一段  "},{"from":3,"to":4,"content":""},{"from":4,"to":5,"content":"第二段"}]}
    """))
{
    var transcript = BilibiliSubtitleParsing.ParseSubtitleTranscript(subtitleJson.RootElement);
    Check("formats subtitle segments and skips blank text", transcript ==
        $"[00:00:01.250 --> 00:00:02.500] 第一段{Environment.NewLine}[00:00:04.000 --> 00:00:05.000] 第二段");
}

using (var malformedJson = JsonDocument.Parse("{}"))
{
    var threw = false;
    try { BilibiliSubtitleParsing.ParseSubtitleTranscript(malformedJson.RootElement); }
    catch (InvalidOperationException) { threw = true; }
    Check("rejects subtitle JSON without body", threw);
}

var signatureParametersA = new Dictionary<string, string> { ["aid"] = "123", ["cid"] = "456" };
var signatureParametersB = new Dictionary<string, string> { ["cid"] = "456", ["aid"] = "123" };
var signatureA = BilibiliSubtitleParsing.CreateWbiSignature(signatureParametersA, "test-mixin-key", 1700000000);
var signatureB = BilibiliSubtitleParsing.CreateWbiSignature(signatureParametersB, "test-mixin-key", 1700000000);
Check("WBI signature is stable regardless of input parameter order", signatureA == signatureB && signatureA.Length == 32);
Check("WBI signature changes when signed timestamp changes", signatureA != BilibiliSubtitleParsing.CreateWbiSignature(signatureParametersA, "test-mixin-key", 1700000001));
Check("WBI mixin key has expected length", BilibiliSubtitleParsing.GetMixinKey(new string('a', 32), new string('b', 32)).Length == 32);

Console.WriteLine($"{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;
