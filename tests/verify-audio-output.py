"""Decode exported tracks: timing, tone mix, silence, and unchanged video packets."""
import json
import subprocess
import sys
from pathlib import Path
import numpy as np

root = Path(__file__).resolve().parent.parent
folder = Path(sys.argv[1])
ffmpeg, probe = root / 'tools/ffmpeg.exe', root / 'tools/ffprobe.exe'

def metadata(path):
    return json.loads(subprocess.check_output([str(probe), '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)]))

def video_hash(path):
    return subprocess.check_output([str(ffmpeg), '-v', 'error', '-i', str(path), '-map', '0:v:0', '-c:v', 'copy', '-f', 'hash', '-hash', 'sha256', '-']).decode().strip()

results = {}
for ext in ('.mp4', '.mkv'):
    path = folder / ('mixed' + ext)
    meta = metadata(path)
    audio = next(s for s in meta['streams'] if s['codec_type'] == 'audio')
    video = next(s for s in meta['streams'] if s['codec_type'] == 'video')
    assert audio['codec_name'] == 'aac' and audio['sample_rate'] == '48000' and audio['channels'] == 2
    assert (video['width'], video['height']) == (320, 180)
    assert abs(float(meta['format']['duration']) - 3) < .06
    assert video_hash(path) == video_hash(folder / ('video' + ext)), 'Mux changed compressed video data'
    decoded = subprocess.check_output([str(ffmpeg), '-v', 'error', '-i', str(path), '-map', '0:a:0', '-f', 'f32le', '-ac', '2', '-ar', '48000', '-'])
    wave = np.frombuffer(decoded, dtype='<f4').reshape(-1, 2)[:, 0]
    # MKV retains AAC encoder priming; allow one AAC packet of timestamp padding.
    assert 3 <= len(wave) / 48000 < 3.06
    rms = lambda a, b: float(np.sqrt(np.mean(wave[int(a*48000):int(b*48000)] ** 2)))
    assert rms(.04, .20) < .001, 'Leading silence lost'
    assert rms(2.06, 2.20) < .001, 'Silent device interval shifted or lost'
    assert rms(.4, .8) > .12 and rms(1.2, 1.8) > .12, 'Tone missing before/after pause'
    segment = wave[int(.4*48000):int(.9*48000)]
    spectrum = abs(np.fft.rfft(segment * np.hanning(len(segment))))
    freqs = np.fft.rfftfreq(len(segment), 1/48000)
    level = lambda hz: float(spectrum[np.abs(freqs-hz) < 8].max())
    assert level(440) > 100 and level(660) > 100, 'One source missing from mix'
    paused_tone = abs(np.fft.rfft(wave * np.hanning(len(wave))))
    paused_freqs = np.fft.rfftfreq(len(wave), 1/48000)
    assert paused_tone[np.abs(paused_freqs-1760) < 5].max() < 2, 'Audio captured during pause leaked into output'
    results[path.name] = dict(codec='aac', rate=48000, channels=2, duration=float(meta['format']['duration']),
                              tone_440=True, tone_660=True, silence=True, pause_removed=True, video_packets_unchanged=True)

assert not any(s['codec_type'] == 'audio' for s in metadata(folder / 'silent.mp4')['streams'])
results['silent.mp4'] = dict(video_only=True)
(folder / 'audio-verification.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
print(json.dumps(results, indent=2))
