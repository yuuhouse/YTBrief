[HackMD好讀版](https://hackmd.io/@yuuhouse/rkMXMDkEGl)

![YTBrief](https://hackmd.io/_uploads/By2BuPJNGx.png)

# YTBrief

Local YouTube and Bilibili video summarization helper for Windows.

What it does:

1. Paste a YouTube or Bilibili URL
2. For YouTube, download audio with `yt-dlp`, then transcribe it with faster-whisper
3. For Bilibili, fetch an available subtitle directly from Bilibili's API without using `yt-dlp`
4. Produce a timestamped transcript and a ChatGPT-ready prompt

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

## Unit Tests

Run the offline unit test suite with:

```powershell
dotnet run --project tests/OpenYTBrief.UnitTests/OpenYTBrief.UnitTests.csproj
```

## Output

Each run creates a folder under `output\TIMESTAMP` with:

- `video.mkv`
- `audio.wav`
- `metadata.txt`
- `chatgpt_prompt.txt`
- `transcript.txt` when a transcript is available
- `subtitle.json` for Bilibili videos when a subtitle track is available

Each transcript line includes the segment start and end time, for example:
`[00:01:23.400 --> 00:01:27.100] transcript text`

## Screenshot
![CMD](https://meee.com.tw/WrVmB4Q.png)
![CMD](https://meee.com.tw/mxMRs7Q.png)
![Chatgpt](https://meee.com.tw/NDxejgO.png)

## Requirements

- Windows
- .NET 8 or newer
- `yt-dlp.exe`
- `ffmpeg.exe`
- `ffprobe.exe`
- Python 3.10+ for faster-whisper installation

## Notes

- YouTube uses the existing `yt-dlp` and faster-whisper workflow. Bilibili uses its video metadata and player subtitle APIs directly and does not invoke `yt-dlp`.
- If Bilibili has no accessible subtitle, YTBrief tries its playback API for audio and uses the existing local Whisper and ffmpeg tools. This path does not use `yt-dlp`.
- If Bilibili requires login, put an exported Netscape-format Cookie file named `bilibili_cookies.txt` in the project folder. This file is ignored by Git and should not be shared.
- This version produces a prompt and attempts to paste and submit it in ChatGPT web.
- If faster-whisper is missing, the app tries to install it from `update\Install-Whisper.bat`.
- `yt-dlp` update warnings are suppressed.
- Video merging uses `mkv` to avoid Opus merge failures.
- faster-whisper output is written as UTF-8 to avoid `cp950` encoding errors.
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
