import argparse
from pathlib import Path

from faster_whisper import WhisperModel


def format_timestamp(seconds: float) -> str:
    milliseconds = max(0, round(seconds * 1000))
    hours, remainder = divmod(milliseconds, 3_600_000)
    minutes, remainder = divmod(remainder, 60_000)
    seconds, milliseconds = divmod(remainder, 1_000)
    return f"{hours:02d}:{minutes:02d}:{seconds:02d}.{milliseconds:03d}"


def main() -> None:
    parser = argparse.ArgumentParser(description="Transcribe one audio file with faster-whisper")
    parser.add_argument("audio")
    parser.add_argument("output")
    parser.add_argument("--model", default="base")
    parser.add_argument("--language", default="zh")
    args = parser.parse_args()

    # CPU/int8 works on ordinary Windows installations. Users with a configured
    # CUDA environment can opt into GPU mode with FASTER_WHISPER_DEVICE=gpu.
    import os

    device = os.environ.get("FASTER_WHISPER_DEVICE", "cpu")
    compute_type = os.environ.get("FASTER_WHISPER_COMPUTE_TYPE", "int8" if device == "cpu" else "float16")
    model = WhisperModel(args.model, device=device, compute_type=compute_type)
    segments, _ = model.transcribe(args.audio, language=args.language)

    output = Path(args.output)
    lines = []
    for segment in segments:
        text = segment.text.strip()
        if text:
            lines.append(
                f"[{format_timestamp(segment.start)} --> {format_timestamp(segment.end)}] {text}"
            )

    output.write_text("\n".join(lines) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
