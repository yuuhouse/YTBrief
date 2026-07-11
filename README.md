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

## Recent changes (最近改動)

- 下載流程：已改為只用 yt-dlp 直接下載音訊 (bestaudio)，不再預先下載完整影片再用 ffmpeg 抽取。下載會輸出到 output\TIMESTAMP\audio.*。
- 轉錄流程：Transcribe 已修改為接受 yt-dlp 下載的音訊檔；若使用 whisper.cpp 並且音訊不是 WAV，程式會在有 ffmpeg 時自動把音訊轉為 16kHz、單聲道、pcm_s16le 的 WAV。使用 Python whisper 時可直接傳入多種音訊格式。
- ChatGPT 自動化：在產生 prompt 並寫入檔案、複製到剪貼簿後，程式會自動開啟 https://chat.openai.com/chat 並嘗試貼上（Ctrl+V）並送出（Enter）。此為 best-effort 行為，可能會因瀏覽器焦點或登入狀態而失敗。
- 其他：新增 FindDownloadedAudio helper，用以尋找 yt-dlp 產生的音訊檔；Transcribe 也接收可選的 ffmpeg 路徑以在必要時轉檔。
- 建置驗證：已在本機使用 dotnet build 編譯通過（0 error）。

# 下一步（可選，選一）

- 若要更可靠的自動化（更高成功率與可控），可改用自動化瀏覽器工具（Puppeteer/Playwright）來打開網頁、等待登入狀態、把文字放到正確的輸入欄再送出。我可以幫你加入一個本地 Playwright/Node 腳本並呼叫它（需要安裝 Node 與 Playwright）。
- 或者新增一個 CLI 選項 --auto-send 來讓自動送出成為選用而非預設。
- 或將 changes commit 並把 build artifacts 從 repo 中移除（建議）。

