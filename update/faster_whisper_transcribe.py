import argparse
import os
from pathlib import Path


def add_cuda_dll_directories() -> None:
    # NVIDIA's pip wheels install CUDA DLLs under site-packages on Windows,
    # which is not on the DLL search path by default.
    if os.name != "nt":
        return

    try:
        import nvidia.cublas
        import nvidia.cudnn
    except ImportError:
        return

    for package in (nvidia.cublas, nvidia.cudnn):
        dll_dir = Path(package.__path__[0]) / "bin"
        if dll_dir.is_dir():
            os.add_dll_directory(str(dll_dir))


add_cuda_dll_directories()
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

    device = os.environ.get("FASTER_WHISPER_DEVICE", "cpu").strip().lower()
    if device == "gpu":
        device = "cuda"
    compute_type = os.environ.get("FASTER_WHISPER_COMPUTE_TYPE", "int8" if device == "cpu" else "float16")
    print(f"Loading faster-whisper model={args.model}, device={device}, compute_type={compute_type}", flush=True)
    model = WhisperModel(args.model, device=device, compute_type=compute_type)
    print("Model loaded successfully.", flush=True)
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
