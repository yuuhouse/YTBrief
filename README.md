# OpenYTBrief

Local YouTube download and transcription helper for Windows.

What it does:

1. Paste a YouTube URL
2. Download the video with `yt-dlp`
3. Extract audio with `ffmpeg`
4. Auto-detect Whisper and install it if needed
5. Produce a transcript and a ChatGPT-ready prompt

## Run It

Double-click:
- [YTBrief-Launcher.bat](YTBrief-Launcher.bat)

Launcher behavior:

- In the project root, double-clicking prefers `dotnet run`
- If an `.exe` is present, it uses that instead
- It works without publishing first
- The launcher inside `publish` can also run independently

Or use the command line:

```powershell
dotnet run --project OpenYTBrief.csproj -- "https://www.youtube.com/watch?v=..."
```

## Update Tools

Update scripts live in `update`:

- [update/Update-YtDlp.bat](update/Update-YtDlp.bat)
- [update/Install-Whisper.bat](update/Install-Whisper.bat)
- [update/Update-All.bat](update/Update-All.bat)

## Output

Each run creates a folder under `output\TIMESTAMP` with:

- `video.mkv`
- `audio.wav`
- `metadata.txt`
- `chatgpt_prompt.txt`
- `transcript.txt` when Whisper finishes successfully

## Requirements

- Windows
- .NET 8 or newer
- `yt-dlp.exe`
- `ffmpeg.exe`
- `ffprobe.exe`
- Python 3.10+ for Whisper installation

## Notes

- This version produces a prompt you can paste into ChatGPT.
- It does not automatically send content into ChatGPT web.
- If Whisper is missing, the app tries to install it from `update\Install-Whisper.bat`.
- `yt-dlp` update warnings are suppressed.
- Video merging uses `mkv` to avoid Opus merge failures.
- Whisper output is forced to UTF-8 to avoid `cp950` encoding errors.
- CPU `FP16` warnings are disabled.
