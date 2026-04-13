using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace CPlayer.WinForms.Core
{
    public static class FFmpegLoader
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private static bool _registered = false;

        public static void RegisterBinaries()
        {
            if (_registered) return;

            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            
            // 优先级 1: 本地开发/手动拷贝路径
            var pathLocal = Path.Combine(basePath, "libs", "ffmpeg");
            // 优先级 2: NuGet 标准 Native 路径 (win-x64)
            var pathNuGet = Path.Combine(basePath, "runtimes", "win-x64", "native");

            string finalPath = "";
            if (Directory.Exists(pathLocal) && Directory.EnumerateFiles(pathLocal, "avcodec*.dll").Any())
            {
                finalPath = pathLocal;
            }
            else if (Directory.Exists(pathNuGet) && Directory.EnumerateFiles(pathNuGet, "avcodec*.dll").Any())
            {
                finalPath = pathNuGet;
            }
            else
            {
                finalPath = basePath;
            }

            // 关键：告诉 Windows OS 加载器去这个目录找依赖 DLL (如 avutil, swresample 等)
            SetDllDirectory(finalPath);
            ffmpeg.RootPath = finalPath;

            _registered = true;
        }
    }
}
