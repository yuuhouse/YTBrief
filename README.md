[HackMD好讀版](https://hackmd.io/@yuuhouse/rkMXMDkEGl)

![YTBrief](https://hackmd.io/_uploads/By2BuPJNGx.png)

# YTBrief

Windows YouTube and Bilibili transcript and summarization helper.

What it does:

1. Paste a YouTube or Bilibili URL.
2. For YouTube, download audio with `yt-dlp` and transcribe it with the configured local Whisper tool.
3. For Bilibili, request video information and subtitles directly from Bilibili's API. This path does not use `yt-dlp`.
4. If a Bilibili subtitle is unavailable, try the Bilibili playback API for audio and transcribe it with local Whisper.
5. Save a timestamped transcript and a ChatGPT prompt.

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

- `metadata.txt`
- `chatgpt_prompt.txt`
- `transcript.txt` when a transcript is available
- `subtitle.json` for Bilibili videos when a subtitle track is available
- `audio.*` for YouTube audio, or `bilibili_audio.m4s` and `audio.wav` when Bilibili subtitle fallback uses Whisper

Each transcript line includes the segment start and end time, for example:
`[00:01:23.400 --> 00:01:27.100] transcript text`

## Screenshot
![CMD](https://meee.com.tw/WrVmB4Q.png)
![CMD](https://meee.com.tw/mxMRs7Q.png)
![Chatgpt](https://meee.com.tw/NDxejgO.png)

## Bilibili Setup

Bilibili may require a signed-in session to provide subtitle information. Add your own Cookie locally:

1. Sign in to Bilibili in your browser.
2. Export Bilibili cookies in Netscape format, or copy the request Cookie header from your browser's developer tools.
3. Save the export as `bilibili_cookies.txt` in the project folder. A raw Cookie header can be one line, for example:

   ```text
   SESSDATA=your_value; bili_jct=your_value; DedeUserID=your_value
   ```

YTBrief reads this file when it starts. Restart YTBrief after changing it. The file is ignored by Git, but `.gitignore` does not protect it from being included in a manually created ZIP; never share or commit it. Cookies expire and may need to be exported again.

### Bilibili errors

- `-101` means Bilibili did not accept a signed-in session. Check that the Cookie file is present, valid, and was saved before launching YTBrief.
- HTTP `412` means Bilibili blocked an API request through risk control. It does not mean the video has no subtitles. Wait before retrying; repeated requests may continue to be blocked.
- Bilibili's APIs can change, so direct API access is not guaranteed for every video or account.

## Requirements

- Windows
- .NET 8 SDK and runtime to build and run from a source checkout
- Python 3.10+ for faster-whisper; the app can try to install faster-whisper when it is missing
- An NVIDIA GPU and a compatible NVIDIA driver when using `YTBrief-Launcher.bat`; the launcher selects CUDA
- Internet access for downloading `yt-dlp`, faster-whisper's CUDA libraries, and the speech model on first use
- For Bilibili subtitles: a valid local Cookie may be required
- For Bilibili audio fallback: `ffmpeg.exe` and the configured local Whisper tool; ffmpeg is not bundled

## First-time setup from GitHub

- The repository does not include the external `.exe` tools. YTBrief can download `yt-dlp.exe` when needed; install `ffmpeg.exe` yourself if you need Bilibili audio fallback.
- The first transcription may take longer: the installer downloads faster-whisper and its CUDA dependencies, then faster-whisper downloads the selected model. These files are cached for later runs.
- The launcher is configured for NVIDIA CUDA. Computers without a supported NVIDIA GPU cannot use this launcher configuration.
- Bilibili may require a personal Cookie. Create `bilibili_cookies.txt` locally as described above; do not upload it to GitHub.

## Notes

- Bilibili uses its video metadata, WBI signing, player subtitle, and playback APIs directly; it does not invoke `yt-dlp`.
- This version produces a prompt and attempts to paste and submit it in ChatGPT web.
- If faster-whisper is missing, the app tries to install it from `update\Install-Whisper.bat`.
- `yt-dlp` update warnings are suppressed.
- Video merging uses `mkv` to avoid Opus merge failures.
- faster-whisper output is written as UTF-8 to avoid `cp950` encoding errors.
- CPU `FP16` warnings are disabled.

## Recent changes (最近改動)

- Bilibili：新增直接 API 字幕擷取、WBI 簽名、Cookie 設定，以及無字幕時的音訊與 Whisper 備援；不經過 yt-dlp。
- 下載流程：已改為只用 yt-dlp 直接下載音訊 (bestaudio)，不再預先下載完整影片再用 ffmpeg 抽取。下載會輸出到 output\TIMESTAMP\audio.*。
- 轉錄流程：Transcribe 已修改為接受 yt-dlp 下載的音訊檔；若使用 whisper.cpp 並且音訊不是 WAV，程式會在有 ffmpeg 時自動把音訊轉為 16kHz、單聲道、pcm_s16le 的 WAV。使用 Python whisper 時可直接傳入多種音訊格式。
- ChatGPT 自動化：在產生 prompt 並寫入檔案、複製到剪貼簿後，程式會自動開啟 https://chat.openai.com/chat 並嘗試貼上（Ctrl+Shift+V）並送出（Enter）。此為 best-effort 行為，可能會因瀏覽器焦點或登入狀態而失敗。
- 其他：新增 FindDownloadedAudio helper，用以尋找 yt-dlp 產生的音訊檔；Transcribe 也接收可選的 ffmpeg 路徑以在必要時轉檔。
- 建置驗證：已在本機使用 dotnet build 編譯通過（0 error）。

# QA：下載音訊時出現 HTTP Error 403

## 症狀

Launcher 在「下載音訊（只抓 audio）」階段失敗，訊息類似：

```text
ERROR: unable to download video data: HTTP Error 403: Forbidden
失敗: yt-dlp.exe 結束碼 1
```

如果不同 YouTube 影片、`youtube.com/watch` 與 `youtu.be` 網址都出現相同錯誤，通常不是網址格式或影片本身的問題，而是 yt-dlp 的 YouTube extractor 已落後於 YouTube 的播放器變更。

## 修復方式

在專案根目錄執行：

```bat
update\Update-YtDlp.bat
```

目前更新腳本使用 yt-dlp nightly，因為 YouTube 的修復有時會先發布在 nightly、尚未進入 stable 版。也可以手動執行：

```bat
yt-dlp.exe --update-to nightly
```

確認版本：

```bat
yt-dlp.exe --version
```

更新後重新啟動 Launcher，再貼上原本的 YouTube URL 測試即可。此次案例更新至 `2026.08.18.122307` 後，`DsFrgeWhtIk` 已能成功下載 `audio.webm`。

## 注意事項

- 若從 `publish` 資料夾啟動，請更新該啟動位置所使用的 `yt-dlp.exe`，避免仍使用舊副本。
- 更新後仍失敗時，先確認實際執行的 yt-dlp 版本，再重新執行測試；不要只重貼同一個 URL。
- 這個 403 與 Whisper、ffmpeg 或輸出資料夾無關，應先修復 yt-dlp 下載階段。
