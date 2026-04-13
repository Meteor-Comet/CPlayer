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

            // 【Debug 诊断】: 关键！请告诉我运行后弹窗里显示的路径是什么
            System.Windows.Forms.MessageBox.Show($"FFmpeg Path Selected: {finalPath}", "Debug Path Info");

            SetDllDirectory(finalPath);
            ffmpeg.RootPath = finalPath;

            // 尝试手动预加载最核心的 avutil，如果这步失败，说明架构(x64/x86)不匹配或文件损坏
            IntPtr hUtil = LoadLibrary(Path.Combine(finalPath, "avutil-59.dll"));
            if (hUtil == IntPtr.Zero)
            {
                 System.Windows.Forms.MessageBox.Show($"CRITICAL: Failed to Load avutil-59.dll from {finalPath}. Error Code: {Marshal.GetLastWin32Error()}", "DLL Load Error");
            }

            _registered = true;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}
