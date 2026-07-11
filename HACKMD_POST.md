# YTBrief（懶人版）——按兩下就能把 YouTube 變成 ChatGPT 的摘要

:::success
> 一鍵將 YouTube 影片轉換成 ChatGPT 可分析的逐字稿與摘要 Prompt。
:::

![YTBrief](https://hackmd.io/_uploads/By2BuPJNGx.png)


## 如何使用

1. 下載專案
2. 雙擊：

```bat
YTBrief-Launcher.bat
```

3. 貼上 YouTube 影片網址
4. 等待處理完成，自動開啟 ChatGPT 貼上並送出摘要


## YTBrief功能

YTBrief 自動完成：

```
YouTube 影片
      ↓
yt-dlp 下載音訊
      ↓
Whisper 語音轉文字
      ↓
產生 ChatGPT Prompt
      ↓
生成摘要與分析，自動開啟 ChatGPT 貼上並送出摘要
```

適合用於：

- 長篇訪談整理
- 教學影片筆記
- 遊戲影片分析
- 新聞與評論摘要

完成後會在：

```text
output\時間戳資料夾
```

取得：

- `audio.webm`：影片音訊
- `transcript.txt`：影片逐字稿
- `chatgpt_prompt.txt`：可直接貼到 ChatGPT 的分析 Prompt


## 系統需求

必要：

- Windows
- `yt-dlp.exe`
- `ffmpeg.exe`

選用：

- Python 3.10+
- Whisper（本機語音轉錄）

若缺少 Whisper，Launcher 會嘗試自動安裝。

---

## 使用範例

一部 1 小時訪談：

```
貼上 YouTube 連結
        ↓
自動下載音訊
        ↓
產生逐字稿
        ↓
取得 ChatGPT Prompt
        ↓
生成摘要、重點與時間軸
```

---

## 注意事項

- 自動開啟 ChatGPT 與貼上功能屬於輔助功能，失敗時可手動複製 `chatgpt_prompt.txt`。
- 將逐字稿貼到 ChatGPT 時，內容會傳送至 OpenAI 服務，敏感資料請先匿名處理。
- Launcher 可能會自動下載 `yt-dlp.exe`，目前未提供 checksum 驗證，高安全環境建議自行確認來源。

---

![Screenshot 2026-07-11 145033](https://hackmd.io/_uploads/BktU_DyVzx.png)
