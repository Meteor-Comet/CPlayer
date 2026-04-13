using System;
using System.IO;
using System.Linq;
using FFmpeg.AutoGen;

namespace CPlayer.WinForms.Core
{
    public static class FFmpegLoader
    {
        private static bool _registered = false;

        public static void RegisterBinaries()
        {
            if (_registered) return;

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            
            // 优先级 1: 本地开发/手动拷贝路径
            var pathLocal = Path.Combine(basePath, "libs", "ffmpeg");
            // 优先级 2: NuGet 标准 Native 路径 (win-x64)
            var pathNuGet = Path.Combine(basePath, "runtimes", "win-x64", "native");

            if (Directory.Exists(pathLocal) && Directory.EnumerateFiles(pathLocal, "avcodec*.dll").Any())
            {
                ffmpeg.RootPath = pathLocal;
            }
            else if (Directory.Exists(pathNuGet) && Directory.EnumerateFiles(pathNuGet, "avcodec*.dll").Any())
            {
                ffmpeg.RootPath = pathNuGet;
            }
            else
            {
                // 优先级 3: 直接在根目录查找
                ffmpeg.RootPath = basePath;
            }

            _registered = true;
        }
    }
}
