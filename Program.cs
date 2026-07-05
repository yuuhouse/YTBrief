using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

var appDir = AppContext.BaseDirectory;
var workRoot = Directory.GetCurrentDirectory();

var url = options.Url;
if (string.IsNullOrWhiteSpace(url))
{
    Console.Write("貼上 YouTube 網址: ");
    url = Console.ReadLine();
}

if (string.IsNullOrWhiteSpace(url))
{
    Console.Error.WriteLine("沒有輸入網址。");
    return 1;
}

var ytdlp = ResolveRequiredTool("yt-dlp.exe", workRoot, appDir);
var ffmpeg = ResolveRequiredTool("ffmpeg.exe", workRoot, appDir);
if (ytdlp is null || ffmpeg is null)
{
    return 1;
}

var runId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
var outputDir = Path.GetFullPath(Path.Combine(workRoot, "output", runId));
Directory.CreateDirectory(outputDir);

var videoTemplate = Path.Combine(outputDir, "video.%(ext)s");
var audioPath = Path.Combine(outputDir, "audio.wav");
var transcriptPath = Path.Combine(outputDir, "transcript.txt");
var promptPath = Path.Combine(outputDir, "chatgpt_prompt.txt");
var metadataPath = Path.Combine(outputDir, "metadata.txt");

Console.WriteLine();
Console.WriteLine($"輸出資料夾: {outputDir}");

try
{
    Console.WriteLine();
    Console.WriteLine("1/4 下載影片...");
    await RunProcess(
        ytdlp,
        [
            "--no-update",
            "--no-playlist",
            "-f", "bv*+ba/b",
            "--merge-output-format", "mkv",
            "--ffmpeg-location", Path.GetDirectoryName(ffmpeg)!,
            "-o", videoTemplate,
            url
        ],
        outputDir);

    var videoPath = FindDownloadedVideo(outputDir);
    if (videoPath is null)
    {
        throw new InvalidOperationException("yt-dlp 沒有產生可用的影片檔。");
    }

    Console.WriteLine();
    Console.WriteLine("2/4 擷取 Whisper 用音訊...");
    await RunProcess(
        ffmpeg,
        [
            "-y",
            "-i", videoPath,
            "-vn",
            "-ac", "1",
            "-ar", "16000",
            "-c:a", "pcm_s16le",
            audioPath
        ],
        outputDir);

    Console.WriteLine();
    Console.WriteLine("3/4 讀取影片資訊...");
    var title = await CaptureProcess(ytdlp, ["--no-update", "--no-playlist", "--print", "%(title)s", url], outputDir);
    var channel = await CaptureProcess(ytdlp, ["--no-update", "--no-playlist", "--print", "%(channel,uploader)s", url], outputDir);
    var duration = await CaptureProcess(ytdlp, ["--no-update", "--no-playlist", "--print", "%(duration_string)s", url], outputDir);
    await File.WriteAllTextAsync(metadataPath, $"URL: {url}{Environment.NewLine}Title: {title.Trim()}{Environment.NewLine}Channel: {channel.Trim()}{Environment.NewLine}Duration: {duration.Trim()}{Environment.NewLine}", Encoding.UTF8);

    Console.WriteLine();
    Console.WriteLine("4/4 Whisper 轉錄...");
    var whisper = ResolveWhisper(options.WhisperPath);
    if (whisper is null)
    {
        Console.WriteLine("找不到本機 Whisper，正在嘗試自動安裝...");
        var installed = await TryAutoInstallWhisper(workRoot, appDir, outputDir);
        whisper = installed ? ResolveWhisper(options.WhisperPath) : null;
        if (whisper is null)
        {
            await WriteMissingWhisperPrompt(promptPath, url, title.Trim(), channel.Trim(), duration.Trim());
            Console.WriteLine();
            Console.WriteLine("找不到本機 Whisper。已先建立 ChatGPT prompt 範本，但 transcript.txt 尚未產生。");
            Console.WriteLine("可先重跑讓它再嘗試自動安裝，或手動執行 update\\Install-Whisper.cmd 後重跑；也可用 --whisper 指定 whisper.exe 路徑。");
            Console.WriteLine($"Prompt: {promptPath}");
            return 2;
        }
    }

    await Transcribe(whisper, audioPath, transcriptPath, outputDir, options);

    if (!File.Exists(transcriptPath))
    {
        throw new InvalidOperationException("Whisper 執行完成，但沒有產生 transcript.txt。");
    }

    var transcript = await File.ReadAllTextAsync(transcriptPath, Encoding.UTF8);
    var prompt = BuildChatGptPrompt(url, title.Trim(), channel.Trim(), duration.Trim(), transcript);
    await File.WriteAllTextAsync(promptPath, prompt, Encoding.UTF8);
    TrySetClipboard(prompt);

    Console.WriteLine();
    Console.WriteLine("完成。");
    Console.WriteLine($"影片: {videoPath}");
    Console.WriteLine($"逐字稿: {transcriptPath}");
    Console.WriteLine($"ChatGPT 摘要 prompt: {promptPath}");
    Console.WriteLine("如果剪貼簿可用，prompt 已複製，可以直接貼到 ChatGPT。");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"失敗: {ex.Message}");
    Console.Error.WriteLine($"保留中間檔案: {outputDir}");
    return 1;
}

static string? ResolveRequiredTool(string fileName, string workRoot, string appDir)
{
    foreach (var baseDir in new[] { workRoot, appDir })
    {
        var candidate = Path.Combine(baseDir, fileName);
        if (IsUsableToolCandidate(candidate))
        {
            return candidate;
        }
    }

    var fromPath = FindOnPath(fileName);
    if (fromPath is not null)
    {
        return fromPath;
    }

    if (fileName.Equals("yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
    {
        var downloaded = TryDownloadYtDlp(workRoot, appDir);
        if (downloaded is not null)
        {
            return downloaded;
        }
    }

    Console.Error.WriteLine($"找不到 {fileName}。請把它放在程式旁邊，或加入 PATH。");
    return null;
}

static bool IsUsableToolCandidate(string path)
{
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
    {
        return false;
    }

    var info = new FileInfo(path);
    if (info.Length <= 0)
    {
        return false;
    }

    if (Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
    {
        return HasWindowsPeHeader(path);
    }

    return true;
}

static bool HasWindowsPeHeader(string path)
{
    try
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt16() != 0x5A4D)
        {
            return false;
        }

        stream.Seek(0x3C, SeekOrigin.Begin);
        var peOffset = reader.ReadUInt32();
        stream.Seek(peOffset, SeekOrigin.Begin);
        return reader.ReadUInt16() == 0x4550;
    }
    catch
    {
        return false;
    }
}

static string? TryDownloadYtDlp(string workRoot, string appDir)
{
    var targets = new[]
    {
        Path.Combine(workRoot, "yt-dlp.exe"),
        Path.Combine(appDir, "yt-dlp.exe")
    }
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

    foreach (var target in targets)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var bytes = client.GetByteArrayAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe")
                .GetAwaiter()
                .GetResult();

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, bytes);

            if (IsUsableToolCandidate(target))
            {
                Console.WriteLine($"已自動下載 yt-dlp: {target}");
                return target;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"自動下載 yt-dlp 失敗: {ex.Message}");
        }
    }

    return null;
}

static WhisperRunner? ResolveWhisper(string? explicitPath)
{
    if (!string.IsNullOrWhiteSpace(explicitPath))
    {
        return IsUsableToolCandidate(explicitPath)
            ? new WhisperRunner(Path.GetFullPath(explicitPath), [])
            : null;
    }

    var baseDir = AppContext.BaseDirectory;
    var cwd = Directory.GetCurrentDirectory();
    foreach (var candidate in new[]
    {
        Path.Combine(cwd, "whisper.exe"),
        Path.Combine(cwd, "whisper-cli.exe"),
        Path.Combine(cwd, "main.exe"),
        Path.Combine(baseDir, "whisper.exe"),
        Path.Combine(baseDir, "whisper-cli.exe"),
        Path.Combine(baseDir, "main.exe"),
    })
    {
        if (IsUsableToolCandidate(candidate))
        {
            return new WhisperRunner(candidate, []);
        }
    }

    var whisper = FindOnPath("whisper.exe") ?? FindOnPath("whisper");
    if (whisper is not null)
    {
        return new WhisperRunner(whisper, []);
    }

    var python = FindOnPath("python.exe") ?? FindOnPath("python") ?? FindOnPath("py.exe") ?? FindOnPath("py");
    if (python is not null && HasPythonWhisperModule(python))
    {
        return new WhisperRunner(python, ["-m", "whisper"]);
    }

    return null;
}

static string? FindOnPath(string fileName)
{
    var path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrWhiteSpace(path))
    {
        return null;
    }

    foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        var candidate = Path.Combine(dir.Trim('"'), fileName);
        if (IsUsableToolCandidate(candidate))
        {
            return candidate;
        }
    }

    return null;
}

static string? FindDownloadedVideo(string outputDir)
{
    var preferred = new[] { "video.mkv", "video.mp4", "video.webm" };
    foreach (var fileName in preferred)
    {
        var candidate = Path.Combine(outputDir, fileName);
        if (File.Exists(candidate) && new FileInfo(candidate).Length > 0)
        {
            return candidate;
        }
    }

    return Directory
        .EnumerateFiles(outputDir, "video.*")
        .Where(path =>
        {
            var name = Path.GetFileName(path);
            if (name.Contains(".temp.", StringComparison.OrdinalIgnoreCase) ||
                name.Contains(".f", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return new FileInfo(path).Length > 0;
        })
        .OrderByDescending(path => new FileInfo(path).Length)
        .FirstOrDefault();
}

static async Task<bool> TryAutoInstallWhisper(string workRoot, string appDir, string outputDir)
{
    foreach (var script in new[]
    {
        Path.Combine(workRoot, "update", "Install-Whisper.bat"),
        Path.Combine(appDir, "update", "Install-Whisper.bat"),
        Path.Combine(appDir, "..", "update", "Install-Whisper.bat"),
    })
    {
        var fullPath = Path.GetFullPath(script);
        if (!File.Exists(fullPath))
        {
            continue;
        }

        Console.WriteLine($"執行: {fullPath}");
        var exitCode = await PipeProcess(
            "cmd.exe",
            ["/c", fullPath],
            outputDir,
            capture: null,
            environment: new Dictionary<string, string> { ["OYTB_NO_PAUSE"] = "1" });
        return exitCode == 0;
    }

    Console.WriteLine("找不到 update\\Install-Whisper.bat。");
    return false;
}

static bool HasPythonWhisperModule(string python)
{
    try
    {
        using var process = new Process();
        process.StartInfo.FileName = python;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.ArgumentList.Add("-m");
        process.StartInfo.ArgumentList.Add("whisper");
        process.StartInfo.ArgumentList.Add("--help");
        process.Start();
        if (!process.WaitForExit(20_000))
        {
            process.Kill(entireProcessTree: true);
            return false;
        }

        return process.ExitCode == 0;
    }
    catch
    {
        return false;
    }
}

static async Task Transcribe(WhisperRunner whisper, string audioPath, string transcriptPath, string outputDir, CliOptions options)
{
    var exe = Path.GetFileName(whisper.FileName).ToLowerInvariant();
    var mode = options.WhisperMode;
    if (mode == WhisperMode.Auto)
    {
        mode = whisper.ArgumentPrefix.Count == 0 && (exe.Contains("whisper-cli") || exe == "main.exe")
            ? WhisperMode.WhisperCpp
            : WhisperMode.OpenAiWhisper;
    }

    if (mode == WhisperMode.WhisperCpp)
    {
        var modelPath = options.ModelPath ?? Path.Combine(Directory.GetCurrentDirectory(), "models", "ggml-base.bin");
        if (!File.Exists(modelPath))
        {
            throw new InvalidOperationException($"whisper.cpp 模式需要模型檔，預設位置是 {modelPath}，或用 --model 指定。");
        }

        var outputBase = Path.Combine(outputDir, "transcript");
        await RunProcess(
            whisper.FileName,
            [
                "-m", modelPath,
                "-f", audioPath,
                "-l", options.Language,
                "-otxt",
                "-of", outputBase
            ],
            outputDir);

        var cppOutput = outputBase + ".txt";
        if (File.Exists(cppOutput))
        {
            File.Copy(cppOutput, transcriptPath, overwrite: true);
        }

        return;
    }

    var whisperArgs = whisper.ArgumentPrefix
        .Concat([
            audioPath,
            "--model", options.Model,
            "--language", options.Language,
            "--fp16", "False",
            "--output_format", "txt",
            "--output_dir", outputDir
        ])
        .ToArray();

    await RunProcess(
        whisper.FileName,
        whisperArgs,
        outputDir);

    var openAiOutput = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(audioPath) + ".txt");
    if (File.Exists(openAiOutput))
    {
        File.Copy(openAiOutput, transcriptPath, overwrite: true);
    }
}

static async Task RunProcess(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
{
    var exitCode = await PipeProcess(fileName, arguments, workingDirectory, capture: null);
    if (exitCode != 0)
    {
        throw new InvalidOperationException($"{Path.GetFileName(fileName)} 結束碼 {exitCode}");
    }
}

static async Task<string> CaptureProcess(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
{
    var builder = new StringBuilder();
    var exitCode = await PipeProcess(fileName, arguments, workingDirectory, builder);
    if (exitCode != 0)
    {
        return "";
    }

    return builder.ToString();
}

static async Task<int> PipeProcess(
    string fileName,
    IReadOnlyList<string> arguments,
    string workingDirectory,
    StringBuilder? capture,
    IReadOnlyDictionary<string, string>? environment = null)
{
    using var process = new Process();
    process.StartInfo.FileName = fileName;
    process.StartInfo.WorkingDirectory = workingDirectory;
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.RedirectStandardOutput = true;
    process.StartInfo.RedirectStandardError = true;
    process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
    process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
    process.StartInfo.Environment["PYTHONIOENCODING"] = "utf-8";
    process.StartInfo.Environment["PYTHONUTF8"] = "1";
    foreach (var arg in arguments)
    {
        process.StartInfo.ArgumentList.Add(arg);
    }

    if (environment is not null)
    {
        foreach (var pair in environment)
        {
            process.StartInfo.Environment[pair.Key] = pair.Value;
        }
    }

    process.OutputDataReceived += (_, e) =>
    {
        if (e.Data is null)
        {
            return;
        }

        capture?.AppendLine(e.Data);
        if (capture is null)
        {
            Console.WriteLine(e.Data);
        }
    };
    process.ErrorDataReceived += (_, e) =>
    {
        if (e.Data is not null)
        {
            Console.Error.WriteLine(e.Data);
        }
    };

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    await process.WaitForExitAsync();
    return process.ExitCode;
}

static string BuildChatGptPrompt(string url, string title, string channel, string duration, string transcript)
{
    return $"""
你是專業的 YouTube 影片研究助理。請根據下方影片資訊與逐字稿，用繁體中文整理成 OpenBrief 風格摘要。

請輸出：
1. 3 句內總覽
2. 重點條列，保留具體數字、專有名詞、事件、因果關係
3. 時間軸摘要，如果逐字稿沒有時間戳就用段落順序
4. 可執行建議或值得追問的問題
5. 可能的偏見、缺漏或需要查證之處

影片資訊：
- 標題：{Fallback(title)}
- 頻道：{Fallback(channel)}
- 長度：{Fallback(duration)}
- URL：{url}

逐字稿：
{transcript}
""";
}

static async Task WriteMissingWhisperPrompt(string promptPath, string url, string title, string channel, string duration)
{
    var prompt = $"""
本機已下載影片，但尚未完成 Whisper 轉錄。請先補上逐字稿，再用下方格式請 ChatGPT 摘要。

影片資訊：
- 標題：{Fallback(title)}
- 頻道：{Fallback(channel)}
- 長度：{Fallback(duration)}
- URL：{url}

請把逐字稿貼在這行下面：

""";
    await File.WriteAllTextAsync(promptPath, prompt, Encoding.UTF8);
}

static string Fallback(string value) => string.IsNullOrWhiteSpace(value) ? "未知" : value.Trim();

static void TrySetClipboard(string text)
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return;
    }

    try
    {
        var thread = new Thread(() => System.Windows.Forms.Clipboard.SetText(text));
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }
    catch
    {
        // Clipboard is best-effort only; the prompt file is the source of truth.
    }
}

static void PrintHelp()
{
    Console.WriteLine("""
OpenYTBrief - YouTube 下載、Whisper 自動安裝/轉錄、產生摘要 prompt

用法:
  OpenYTBrief.exe <YouTube網址>
  OpenYTBrief.exe <YouTube網址> --whisper C:\path\whisper.exe
  OpenYTBrief.exe <YouTube網址> --whisper C:\path\whisper-cli.exe --whisper-mode cpp --model C:\path\ggml-base.bin

選項:
  --model <nameOrPath>        OpenAI Whisper CLI 的模型名稱，預設 base；whisper.cpp 模式則是模型檔路徑
  --language <lang>           Whisper 語言，預設 zh
  --whisper <path>            指定 whisper.exe / whisper-cli.exe
  --whisper-mode <auto|openai|cpp>
  --help
""");
}

enum WhisperMode
{
    Auto,
    OpenAiWhisper,
    WhisperCpp
}

sealed record WhisperRunner(string FileName, IReadOnlyList<string> ArgumentPrefix);

sealed record CliOptions(
    string? Url,
    string Model,
    string Language,
    string? WhisperPath,
    string? ModelPath,
    WhisperMode WhisperMode,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        string? url = null;
        var model = "base";
        var language = "zh";
        string? whisperPath = null;
        string? modelPath = null;
        var mode = WhisperMode.Auto;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                case "/?":
                    showHelp = true;
                    break;
                case "--model":
                    model = RequireValue(args, ref i, arg);
                    modelPath = model;
                    break;
                case "--language":
                    language = RequireValue(args, ref i, arg);
                    break;
                case "--whisper":
                    whisperPath = RequireValue(args, ref i, arg);
                    break;
                case "--whisper-mode":
                    var rawMode = RequireValue(args, ref i, arg).ToLowerInvariant();
                    mode = rawMode switch
                    {
                        "openai" => WhisperMode.OpenAiWhisper,
                        "cpp" => WhisperMode.WhisperCpp,
                        "auto" => WhisperMode.Auto,
                        _ => throw new ArgumentException($"未知 whisper mode: {rawMode}")
                    };
                    break;
                default:
                    if (url is null && !arg.StartsWith("--", StringComparison.Ordinal))
                    {
                        url = arg;
                    }
                    else
                    {
                        throw new ArgumentException($"未知參數: {arg}");
                    }

                    break;
            }
        }

        return new CliOptions(url, model, language, whisperPath, modelPath, mode, showHelp);
    }

    static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{option} 需要值。");
        }

        index++;
        return args[index];
    }
}
