# CPlayer.WinForms

A high-performance, self-contained WinForms media player control built with FFmpeg and NAudio. Ready for NuGet packaging and integration into any .NET Framework 4.7.2+ application.

## Features

- 🎬 **Hardware-accelerated video decoding** via FFmpeg 7.x
- 🔊 **Audio playback** via NAudio WaveOut
- ⏩ **Variable playback speed** (0.5x – 2.0x) with real-time clock sync
- 🎨 **Bilibili-style modern UI** with animated seek bar
- ⌨️ **Keyboard shortcuts**: Space (play/pause), ←/→ (seek ±5s), ↑/↓ (volume ±5%)
- 📦 **NuGet-ready** packaging included

## Requirements

- .NET Framework 4.7.2+
- FFmpeg 7.x 64-bit shared DLLs in `libs/ffmpeg/` next to the executable

## Quick Start

```csharp
var player = new CPlayer.WinForms.UI.MediaPlayerControl();
this.Controls.Add(player);
player.LoadAndPlay(@"C:\video.mp4");
```

## FFmpeg Setup

Download FFmpeg 7.x Windows 64-bit shared build from [ffmpeg.org](https://ffmpeg.org/download.html) and place the `.dll` files in a `libs/ffmpeg/` folder alongside your executable.

## NuGet

```
Install-Package CPlayer.WinForms
```

## License

MIT
