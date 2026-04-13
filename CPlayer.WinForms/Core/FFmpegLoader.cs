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
            bool is64 = IntPtr.Size == 8;
            string arch = is64 ? "win-x64" : "win-x86";
            
            // 按照架构区分搜索路径
            var pathLocal = Path.Combine(basePath, "libs", is64 ? "ffmpeg" : "ffmpeg_x86");
            var pathNuGet = Path.Combine(basePath, "runtimes", arch, "native");

            string finalPath = "";
            if (Directory.Exists(pathLocal) && Directory.GetFiles(pathLocal, "avcodec*.dll").Any())
            {
                finalPath = pathLocal;
            }
            else if (Directory.Exists(pathNuGet) && Directory.GetFiles(pathNuGet, "avcodec*.dll").Any())
            {
                finalPath = pathNuGet;
            }
            else
            {
                finalPath = basePath;
            }

            SetDllDirectory(finalPath);
            ffmpeg.RootPath = finalPath;

            _registered = true;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}
