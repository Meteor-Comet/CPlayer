using System;
using System.IO;
using FFmpeg.AutoGen;

namespace CPlayer.WinForms.Core
{
    public static class FFmpegLoader
    {
        public static void RegisterBinaries(string path = @"libs\ffmpeg")
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var fullPath = Path.Combine(basePath, path);
            ffmpeg.RootPath = fullPath;
        }
    }
}
