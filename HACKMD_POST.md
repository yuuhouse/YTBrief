# OpenYTBrief — 本地化 YouTube 下載與自動產生 ChatGPT 摘要的工具

> 發佈平台：HackMD
> 作者：OpenYTBrief 開發者團隊
> 最後更新：2026-07-11

概述
----
OpenYTBrief 是一個專為 Windows 設計的輕量工具，能夠在本機下載 YouTube 的音訊、使用 Whisper（whisper.cpp 或 Python whisper）進行語音轉錄，並產生一份可以直接貼到 ChatGPT 的 OpenBrief 風格 prompt。使用者可以在離線環境先取得逐字稿與摘要 prompt，再貼到 ChatGPT 或其他 LLM 工具做進一步分析。


如何使用（快速上手）
-----------------
前提條件：
- Windows
- .NET 8 SDK（若從原始碼執行）或已編譯的 OpenYTBrief.exe
- yt-dlp.exe（建議放在專案資料夾或 PATH）
- Python 3.10+（若用 Python whisper）或 whisper.cpp 可執行檔與模型檔（若用 whisper.cpp）
- ffmpeg.exe（若需要在 whisper.cpp 模式下轉檔）

從原始碼執行：
```powershell
dotnet run --project OpenYTBrief.csproj -- "https://www.youtube.com/watch?v=..."
```

或使用已編譯的 exe：
```powershell
OpenYTBrief.exe "https://www.youtube.com/watch?v=..."
```

主要輸出（每次執行會建立 output\TIMESTAMP）：
- audio.*（yt-dlp 下載的音訊檔，例如 audio.webm）
- transcript.txt（Whisper 轉錄完成後的逐字稿）
- chatgpt_prompt.txt（針對 ChatGPT 的摘要 prompt）
- metadata.txt（影片標題、頻道、長度與 URL）

自動送出至 ChatGPT（說明與限制）
---------------------------
- 程式會把產生的 prompt 複製到剪貼簿，並嘗試以系統預設瀏覽器開啟 https://chat.openai.com/chat，然後模擬 Ctrl+V 與 Enter 來貼上並送出。
- 這種方法屬於模擬鍵盤輸入（SendKeys），屬於 best-effort：成敗取決於瀏覽器是否為前景視窗、是否已登入、以及瀏覽器及系統安全策略是否允許模擬輸入。
- 若你不希望自動送出或希望更可靠的自動化，建議：
  - 加入 `--auto-send` 選項（可將自動送出改為選用而非預設）
  - 使用 Playwright 或 Puppeteer 以程式化方式登入、等待元素載入並填入對話框（更穩定但需要額外安裝 Node 與 Playwright）

已知問題與修正
------------
- 曾經出現剪貼簿操作失敗（ExternalException）導致程式崩潰，已改為在 STA 執行緒內進行多次重試（最多 5 次）以避免此情況。
- 程式會在缺少 whisper.exe 時嘗試呼叫 `update\Install-Whisper.bat` 進行自動安裝，但自動安裝可能有環境差異，建議先檢查腳本內容再執行。

近期技術細節（供開發者參考）
-----------------
- 程式語言：C# (.NET 8, Windows)
- 主要流程在 Program.cs：下載（yt-dlp）→ 轉檔（ffmpeg，必要時）→ 轉錄（whisper.cpp 或 python -m whisper）→ 產生 prompt → 嘗試自動貼上/送出
- 新增/修改：
  - 只下載音訊（yt-dlp -f bestaudio）
  - 新增 FindDownloadedAudio helper
  - Transcribe 方法改接受 ffmpeg 路徑以在必要時轉檔
  - TrySetClipboard 加入重試以避免 ExternalException

安全與隱私說明
-------------
- 程式會自動從 GitHub 下載最新的 yt-dlp.exe（若本地沒有），目前未對下載內容做 checksum 驗證 — 若部署在敏感或受控環境，建議自行維護 yt-dlp 的二進位或加入 SHA256 驗證。
- 自動把 prompt 貼到 chat.openai.com 會將本地音訊轉錄內容傳送到 OpenAI 的服務（若你在使用 web UI）。請在使用前確認是否符合你的隱私與合規要求。

後續計畫（下一步建議）
------------------
- 改用 Playwright/Puppeteer 做更可靠的 web 自動化（等待登入、定位輸入欄、插入內容並送出） — 我可以幫你加入本地 Node/Playwright 範例腳本。
- 新增 `--auto-send` 選項，將自動送出改為選用（預設關閉）。
- 若希望把 build artifacts 移出 repo，我可以更新 .gitignore 並協助把發行檔移到 release artifacts。

檔案與參考
---------
- 專案位置：倉儲根目錄
- 主要檔案：Program.cs、OpenYTBrief.csproj、update\Install-Whisper.bat
- NEXT_STEPS.md（專案根目錄）包含可選下一步。

感謝
---
歡迎在 HackMD 留下回饋或在專案 repo 開 issue 討論改進想法。若你要我幫忙把 Playwright 自動化加入、或新增 `--auto-send` 參數、或把變更 commit，回覆告訴我要哪一項即可。