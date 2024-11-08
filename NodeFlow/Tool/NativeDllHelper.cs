using Serein.Library;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Tool
{

    internal class NativeDllHelper
    {
        
        // 引入 Windows API 函数
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);



        // 引入 Unix/Linux 的动态库加载函数
        [DllImport("libdl.so.2", SetLastError = true)]
        private static extern IntPtr dlopen(string filename, int flag);

        [DllImport("libdl.so.2", SetLastError = true)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libdl.so.2", SetLastError = true)]
        private static extern int dlclose(IntPtr handle);

        private const int RTLD_NOW = 2;

        //  bool LoadDll(string file)
        //  void LoadAllDll(string path, bool isRecurrence = true);

        private static List<IntPtr> Nints = new List<nint>();

        /// <summary>
        /// 加载单个Dll
        /// </summary>
        /// <param name="file"></param>
        public static bool LoadDll(string file)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return LoadWindowsLibrarie(file);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return LoadLinuxLibrarie(file);
            }
            else
            {
                SereinEnv.WriteLine(InfoType.ERROR, "非预期的OS系统");
                return false;
            }
        }

        public static void LoadAllDll(string path, bool isRecurrence = true)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var file in Directory.GetFiles(path, "*.dll"))
                {
                    LoadWindowsLibrarie(file);
                }
              
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                foreach (var file in Directory.GetFiles(path, "*.so"))
                {
                    LoadLinuxLibrarie(file);
                }
            }
            else
            {
                SereinEnv.WriteLine(InfoType.ERROR, "非预期的OS系统");
            }
            
            foreach (var dir in Directory.GetDirectories(path))
            {
                LoadAllDll(dir, true);
            }

        }


        /// <summary>
        /// 加载Windows类库
        /// </summary>
        /// <param name="path"></param>
        /// <param name="isRecurrence">是否递归加载</param>
        private static bool LoadWindowsLibrarie(string file)
        {
            IntPtr hModule = IntPtr.Zero;
            try
            {
                hModule = LoadLibrary(file);
                // 加载 DLL
                if (hModule != IntPtr.Zero)
                {
                    Nints.Add(hModule);
                    SereinEnv.WriteLine(InfoType.INFO, $"Loaded: {file}");
                    return true;
                }
                else
                {
                    SereinEnv.WriteLine(InfoType.INFO, $"Failed to load {file}: {Marshal.GetLastWin32Error()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"Error loading {file}: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// 加载Linux类库
        /// </summary>
        /// <param name="file"></param>
        private static bool LoadLinuxLibrarie(string file)
        {

            IntPtr handle = IntPtr.Zero;

            try
            {
                handle = dlopen(file, RTLD_NOW);
                if (handle != IntPtr.Zero)
                {
                    Nints.Add(handle);
                    SereinEnv.WriteLine(InfoType.INFO, $"Loaded: {file}");
                    return true;
                    // 可以调用共享库中的函数
                    // IntPtr procAddress = dlsym(handle, "my_function");
                }
                else
                {
                    SereinEnv.WriteLine(InfoType.INFO, $"Failed to load {file}: {Marshal.GetLastWin32Error()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"Error loading {file}: {ex.Message}");
                return false;
            }


           
        }






        /// <summary>
        /// 卸载所有已加载DLL
        /// </summary>
        public static void FreeLibrarys()
        {
            for (int i = 0; i < Nints.Count; i++)
            {
                IntPtr hModule = Nints[i];
                FreeLibrary(hModule);
            }
            Nints.Clear();
        }
    }
}
