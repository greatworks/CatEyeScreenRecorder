"""Decode exported video through FFmpeg and compare dimensions, pixels, and duration."""
import json
import math
import re
import subprocess
import sys
from pathlib import Path

import numpy as np
from PIL import Image

root = Path(__file__).resolve().parent.parent
folder = Path(sys.argv[1])
ffmpeg, ffprobe = root / "tools/ffmpeg.exe", root / "tools/ffprobe.exe"


def probe(name):
    return json.loads(subprocess.check_output([
        str(ffprobe), "-v", "error", "-count_frames", "-show_streams", "-show_format", "-of", "json", str(folder / name)
    ]))["streams"][0]


def decode(name, frames=None):
    command = [str(ffmpeg), "-v", "error", "-i", str(folder / name)]
    if frames is not None:
        command += ["-frames:v", str(frames)]
    return subprocess.check_output(command + ["-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1"])


results = {}
for filename, source, width, height in [
    ("sample-lossless.mkv", "sample-source.png", 1920, 1080),
    ("odd-lossless.mkv", "odd-source.png", 321, 181),
    ("negative-lossless.mkv", "odd-source.png", 321, 181),
]:
    metadata = probe(filename)
    assert (metadata["width"], metadata["height"]) == (width, height)
    expected = np.asarray(Image.open(folder / source).convert("RGB"))
    actual = np.frombuffer(decode(filename, 1), np.uint8).reshape(height, width, 3)
    assert np.array_equal(expected, actual), f"Lossless pixel mismatch: {filename}"
    results[filename] = {"width": width, "height": height, "pixel_identical": True, "bytes": (folder / filename).stat().st_size}

for filename, width, height in [("sample-hq.mp4", 1920, 1080), ("odd-hq.mp4", 321, 181)]:
    metadata = probe(filename)
    assert (metadata["width"], metadata["height"]) == (width, height)
    results[filename] = {"width": width, "height": height, "bytes": (folder / filename).stat().st_size}

source = np.asarray(Image.open(folder / "sample-source.png").convert("RGB"), dtype=np.float64)
decoded = np.frombuffer(decode("sample-hq.mp4", 1), np.uint8).reshape(1080, 1920, 3).astype(np.float64)
mse = np.mean((source - decoded) ** 2)
psnr = 10 * math.log10(255 ** 2 / mse)
# MP4 uses 4:2:0 for common players: preserve structure while measuring (not claiming) RGB loss.
comparison = subprocess.run([str(ffmpeg), "-hide_banner", "-i", str(folder / "sample-hq.mp4"),
    "-i", str(folder / "sample-source.png"), "-lavfi", "ssim", "-frames:v", "1", "-f", "null", "NUL"], capture_output=True, text=True)
assert comparison.returncode == 0
ssim = float(re.search(r"All:([0-9.]+)", comparison.stderr).group(1))
assert ssim > .99, f"Unexpected HQ structure loss: SSIM {ssim}"
Image.fromarray(decoded.astype(np.uint8)).save(folder / "sample-hq-decoded.png")
results["sample-hq.mp4"]["psnr_db"] = round(psnr, 2)
results["sample-hq.mp4"]["ssim"] = ssim
results["raw_equivalent_bytes"] = 1920 * 1080 * 3 * 30

metadata = probe("pause.mkv")
active = float((folder / "pause-duration.txt").read_text(encoding="utf-8-sig"))
frames = np.frombuffer(decode("pause.mkv"), np.uint8).reshape(-1, 180, 320, 3)
colors = {tuple(frame[90, 160]) for frame in frames}
assert colors == {(255, 0, 0), (0, 0, 255)}, f"Pause interval leaked into the video: {colors}"
duration = len(frames) / 15
assert abs(duration - active) <= 1 / 15 + .01
results["pause"] = {"active_seconds": active, "video_seconds": duration, "paused_content_excluded": True}
(folder / "video-verification.json").write_text(json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")
print(json.dumps(results, indent=2, ensure_ascii=False))
print("PASS: full-resolution output, exact RGB lossless, high-quality image, pause timeline.")
