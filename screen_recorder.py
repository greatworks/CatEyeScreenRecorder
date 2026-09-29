import datetime as _dt
import os
import queue
import re
import shutil
import signal
import subprocess
import sys
import threading
import time
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk


APP_NAME = "免费 Windows 屏幕录像"
DEFAULT_OUTPUT_DIR = Path.home() / "Videos" / "ScreenRecordings"


class RecorderApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(APP_NAME)
        self.geometry("760x560")
        self.minsize(720, 520)

        self.ffmpeg_path = self.find_ffmpeg()
        self.process = None
        self.started_at = None
        self.output_file = None
        self.log_queue = queue.Queue()
        self.audio_devices = []

        self.output_dir = tk.StringVar(value=str(DEFAULT_OUTPUT_DIR))
        self.mode = tk.StringVar(value="full")
        self.fps = tk.IntVar(value=30)
        self.quality = tk.StringVar(value="均衡")
        self.capture_audio = tk.BooleanVar(value=False)
        self.audio_device = tk.StringVar(value="")
        self.area_x = tk.IntVar(value=0)
        self.area_y = tk.IntVar(value=0)
        self.area_w = tk.IntVar(value=max(self.winfo_screenwidth(), 1280))
        self.area_h = tk.IntVar(value=max(self.winfo_screenheight(), 720))
        self.status = tk.StringVar(value="就绪")
        self.elapsed = tk.StringVar(value="00:00:00")

        self.create_widgets()
        self.after(120, self.refresh_audio_devices)
        self.after(250, self.poll_log_queue)
        self.after(500, self.tick_timer)
        self.protocol("WM_DELETE_WINDOW", self.on_close)

    def find_ffmpeg(self):
        local_path = Path(__file__).with_name("ffmpeg.exe")
        if local_path.exists():
            return str(local_path)
        return shutil.which("ffmpeg")

    def create_widgets(self):
        self.columnconfigure(0, weight=1)
        self.rowconfigure(3, weight=1)

        header = ttk.Frame(self, padding=(18, 16, 18, 8))
        header.grid(row=0, column=0, sticky="ew")
        header.columnconfigure(0, weight=1)

        ttk.Label(header, text="免费 Windows 屏幕录像", font=("Segoe UI", 18, "bold")).grid(
            row=0, column=0, sticky="w"
        )
        ttk.Label(
            header,
            text="一个小巧的 FFmpeg 录屏工具，支持全屏和自定义区域。",
            foreground="#555",
        ).grid(row=1, column=0, sticky="w", pady=(4, 0))

        self.ffmpeg_label = ttk.Label(header, text=self.ffmpeg_status_text(), foreground=self.ffmpeg_status_color())
        self.ffmpeg_label.grid(row=0, column=1, rowspan=2, sticky="e")

        body = ttk.Frame(self, padding=(18, 8, 18, 12))
        body.grid(row=1, column=0, sticky="nsew")
        body.columnconfigure(1, weight=1)

        ttk.Label(body, text="保存到").grid(row=0, column=0, sticky="w", pady=6)
        ttk.Entry(body, textvariable=self.output_dir).grid(row=0, column=1, sticky="ew", padx=(12, 8), pady=6)
        ttk.Button(body, text="浏览", command=self.choose_output_dir).grid(row=0, column=2, pady=6)

        ttk.Label(body, text="录制范围").grid(row=1, column=0, sticky="w", pady=6)
        mode_frame = ttk.Frame(body)
        mode_frame.grid(row=1, column=1, sticky="w", padx=(12, 0), pady=6)
        ttk.Radiobutton(mode_frame, text="全屏", variable=self.mode, value="full", command=self.sync_area_state).pack(
            side="left"
        )
        ttk.Radiobutton(mode_frame, text="自定义区域", variable=self.mode, value="area", command=self.sync_area_state).pack(
            side="left", padx=(18, 0)
        )

        area = ttk.LabelFrame(body, text="自定义区域")
        area.grid(row=2, column=0, columnspan=3, sticky="ew", pady=(8, 4))
        for col in range(8):
            area.columnconfigure(col, weight=1)

        for col, (label, variable) in enumerate(
            [("X", self.area_x), ("Y", self.area_y), ("宽度", self.area_w), ("高度", self.area_h)]
        ):
            ttk.Label(area, text=label).grid(row=0, column=col * 2, sticky="e", padx=(10, 4), pady=10)
            spin = ttk.Spinbox(area, from_=0, to=99999, increment=10, textvariable=variable, width=8)
            spin.grid(row=0, column=col * 2 + 1, sticky="w", padx=(0, 10), pady=10)

        ttk.Label(body, text="FPS").grid(row=3, column=0, sticky="w", pady=6)
        ttk.Spinbox(body, from_=10, to=60, increment=5, textvariable=self.fps, width=8).grid(
            row=3, column=1, sticky="w", padx=(12, 0), pady=6
        )

        ttk.Label(body, text="画质").grid(row=4, column=0, sticky="w", pady=6)
        quality_box = ttk.Combobox(
            body,
            textvariable=self.quality,
            values=("文件更小", "均衡", "高画质"),
            state="readonly",
            width=18,
        )
        quality_box.grid(row=4, column=1, sticky="w", padx=(12, 0), pady=6)

        ttk.Label(body, text="声音").grid(row=5, column=0, sticky="w", pady=6)
        audio_frame = ttk.Frame(body)
        audio_frame.grid(row=5, column=1, columnspan=2, sticky="ew", padx=(12, 0), pady=6)
        audio_frame.columnconfigure(1, weight=1)
        ttk.Checkbutton(audio_frame, text="录制麦克风", variable=self.capture_audio).grid(row=0, column=0, sticky="w")
        self.audio_combo = ttk.Combobox(audio_frame, textvariable=self.audio_device, state="readonly")
        self.audio_combo.grid(row=0, column=1, sticky="ew", padx=(12, 8))
        ttk.Button(audio_frame, text="刷新", command=self.refresh_audio_devices).grid(row=0, column=2)

        controls = ttk.Frame(self, padding=(18, 0, 18, 12))
        controls.grid(row=2, column=0, sticky="ew")
        controls.columnconfigure(2, weight=1)

        self.start_button = ttk.Button(controls, text="开始录制", command=self.start_recording)
        self.start_button.grid(row=0, column=0, padx=(0, 8))
        self.stop_button = ttk.Button(controls, text="停止", command=self.stop_recording, state="disabled")
        self.stop_button.grid(row=0, column=1)
        ttk.Label(controls, textvariable=self.elapsed, font=("Consolas", 14, "bold")).grid(row=0, column=3, sticky="e")

        log_frame = ttk.LabelFrame(self, text="状态", padding=(10, 8))
        log_frame.grid(row=3, column=0, sticky="nsew", padx=18, pady=(0, 18))
        log_frame.columnconfigure(0, weight=1)
        log_frame.rowconfigure(1, weight=1)
        ttk.Label(log_frame, textvariable=self.status).grid(row=0, column=0, sticky="w", pady=(0, 6))

        self.log_text = tk.Text(log_frame, height=10, wrap="word", state="disabled", font=("Consolas", 9))
        self.log_text.grid(row=1, column=0, sticky="nsew")
        scroll = ttk.Scrollbar(log_frame, orient="vertical", command=self.log_text.yview)
        scroll.grid(row=1, column=1, sticky="ns")
        self.log_text.configure(yscrollcommand=scroll.set)

        self.sync_area_state()

    def ffmpeg_status_text(self):
        if self.ffmpeg_path:
            return f"FFmpeg: {self.ffmpeg_path}"
        return "未找到 FFmpeg"

    def ffmpeg_status_color(self):
        return "#167a3a" if self.ffmpeg_path else "#b42318"

    def choose_output_dir(self):
        chosen = filedialog.askdirectory(initialdir=self.output_dir.get() or str(Path.home()))
        if chosen:
            self.output_dir.set(chosen)

    def sync_area_state(self):
        enabled = self.mode.get() == "area"
        state = "normal" if enabled else "disabled"
        for child in self.winfo_children():
            self.set_area_child_state(child, state)

    def set_area_child_state(self, widget, state):
        if isinstance(widget, ttk.LabelFrame) and str(widget.cget("text")) == "自定义区域":
            for child in widget.winfo_children():
                if isinstance(child, ttk.Spinbox):
                    child.configure(state=state)
            return
        for child in widget.winfo_children():
            self.set_area_child_state(child, state)

    def refresh_audio_devices(self):
        if not self.ffmpeg_path:
            self.audio_devices = []
            self.audio_combo["values"] = []
            self.log("请安装 FFmpeg，或把 ffmpeg.exe 放到 screen_recorder.py 同一目录。")
            return

        def worker():
            command = [self.ffmpeg_path, "-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"]
            try:
                result = subprocess.run(command, capture_output=True, text=True, encoding="utf-8", errors="replace")
                devices = self.parse_audio_devices(result.stderr)
                self.log_queue.put(("audio_devices", devices))
            except OSError as exc:
                self.log_queue.put(("log", f"无法读取音频设备：{exc}"))

        threading.Thread(target=worker, daemon=True).start()

    def parse_audio_devices(self, text):
        devices = []
        in_audio_section = False
        for line in text.splitlines():
            if "DirectShow audio devices" in line:
                in_audio_section = True
                continue
            if "DirectShow video devices" in line:
                in_audio_section = False
                continue
            if in_audio_section:
                match = re.search(r'"([^"]+)"', line)
                if match and "Alternative name" not in line:
                    devices.append(match.group(1))
        return sorted(set(devices))

    def start_recording(self):
        if self.process:
            return
        if not self.ffmpeg_path:
            messagebox.showerror(
                "需要 FFmpeg",
                "未找到 FFmpeg。请安装 FFmpeg，或把 ffmpeg.exe 放到本文件夹，然后重启程序。",
            )
            return

        try:
            output_dir = Path(self.output_dir.get()).expanduser()
            output_dir.mkdir(parents=True, exist_ok=True)
        except OSError as exc:
            messagebox.showerror("保存目录错误", str(exc))
            return

        self.output_file = output_dir / f"recording-{_dt.datetime.now():%Y%m%d-%H%M%S}.mp4"
        command = self.build_ffmpeg_command(self.output_file)
        self.log("")
        self.log("开始录制：")
        self.log(" ".join(f'"{part}"' if " " in str(part) else str(part) for part in command))

        try:
            self.process = subprocess.Popen(
                command,
                stdin=subprocess.PIPE,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.PIPE,
                text=True,
                encoding="utf-8",
                errors="replace",
                creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0,
            )
        except OSError as exc:
            self.process = None
            messagebox.showerror("无法开始录制", str(exc))
            return

        self.started_at = time.monotonic()
        self.status.set(f"正在录制到 {self.output_file}")
        self.start_button.configure(state="disabled")
        self.stop_button.configure(state="normal")
        threading.Thread(target=self.read_process_log, daemon=True).start()
        threading.Thread(target=self.wait_for_process, daemon=True).start()

    def build_ffmpeg_command(self, output_file):
        quality_args = {
            "文件更小": ["-preset", "veryfast", "-crf", "30"],
            "均衡": ["-preset", "veryfast", "-crf", "24"],
            "高画质": ["-preset", "fast", "-crf", "18"],
        }[self.quality.get()]

        command = [
            self.ffmpeg_path,
            "-y",
            "-hide_banner",
            "-f",
            "gdigrab",
            "-framerate",
            str(self.fps.get()),
        ]

        if self.mode.get() == "area":
            command.extend(
                [
                    "-offset_x",
                    str(max(0, self.area_x.get())),
                    "-offset_y",
                    str(max(0, self.area_y.get())),
                    "-video_size",
                    f"{max(2, self.area_w.get())}x{max(2, self.area_h.get())}",
                ]
            )

        command.extend(["-i", "desktop"])

        if self.capture_audio.get() and self.audio_device.get():
            command.extend(["-f", "dshow", "-i", f"audio={self.audio_device.get()}"])

        command.extend(
            [
                "-vcodec",
                "libx264",
                *quality_args,
                "-pix_fmt",
                "yuv420p",
                "-movflags",
                "+faststart",
            ]
        )

        if self.capture_audio.get() and self.audio_device.get():
            command.extend(["-acodec", "aac", "-b:a", "160k"])

        command.append(str(output_file))
        return command

    def stop_recording(self):
        if not self.process:
            return
        self.status.set("正在停止，请稍等，程序正在封装 MP4 文件...")
        try:
            if self.process.stdin:
                self.process.stdin.write("q\n")
                self.process.stdin.flush()
        except OSError:
            if os.name == "nt":
                self.process.send_signal(signal.CTRL_BREAK_EVENT)
            else:
                self.process.terminate()

    def read_process_log(self):
        if not self.process or not self.process.stderr:
            return
        for line in self.process.stderr:
            line = line.strip()
            if line:
                self.log_queue.put(("log", line))

    def wait_for_process(self):
        if not self.process:
            return
        return_code = self.process.wait()
        self.log_queue.put(("finished", return_code))

    def poll_log_queue(self):
        try:
            while True:
                event, payload = self.log_queue.get_nowait()
                if event == "log":
                    self.log(payload)
                elif event == "audio_devices":
                    self.audio_devices = payload
                    self.audio_combo["values"] = payload
                    if payload and not self.audio_device.get():
                        self.audio_device.set(payload[0])
                    self.log(f"找到音频设备：{len(payload)}")
                elif event == "finished":
                    self.finish_recording(payload)
        except queue.Empty:
            pass
        self.after(250, self.poll_log_queue)

    def finish_recording(self, return_code):
        file_text = f" 已保存：{self.output_file}" if self.output_file and self.output_file.exists() else ""
        if return_code == 0:
            self.status.set(f"录制已停止。{file_text}")
        else:
            self.status.set(f"录制结束，退出码 {return_code}。{file_text}")
        self.process = None
        self.started_at = None
        self.start_button.configure(state="normal")
        self.stop_button.configure(state="disabled")

    def tick_timer(self):
        if self.started_at:
            elapsed = int(time.monotonic() - self.started_at)
            hours, remainder = divmod(elapsed, 3600)
            minutes, seconds = divmod(remainder, 60)
            self.elapsed.set(f"{hours:02}:{minutes:02}:{seconds:02}")
        else:
            self.elapsed.set("00:00:00")
        self.after(500, self.tick_timer)

    def log(self, message):
        self.log_text.configure(state="normal")
        self.log_text.insert("end", message + "\n")
        self.log_text.see("end")
        self.log_text.configure(state="disabled")

    def on_close(self):
        if self.process:
            if not messagebox.askyesno("正在录制", "要停止录制并关闭程序吗？"):
                return
            self.stop_recording()
            self.after(900, self.destroy)
            return
        self.destroy()


if __name__ == "__main__":
    if sys.platform != "win32":
        print("此录屏工具面向 Windows，因为它使用 FFmpeg gdigrab。")
    app = RecorderApp()
    app.mainloop()
