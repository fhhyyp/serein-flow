using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace Serein.Workbench.Tool
{


    /*public class EmbeddedHost : HwndHost
    {
        private readonly IntPtr _hwnd;

        public EmbeddedHost(IEmbeddedContent content)
        {
            _hwnd = content.GetWindowHandle();
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            if (_hwnd == IntPtr.Zero)
                throw new InvalidOperationException("无效的窗口句柄");

            // 设置窗口为子窗口（必须去掉 WS_POPUP，添加 WS_CHILD）
            SetWindowLongPtr(_hwnd, GWL_STYLE, GetWindowLongPtr(_hwnd, GWL_STYLE) | WS_CHILD);
            SetParent(_hwnd, hwndParent.Handle);

            // 让窗口填充整个区域
            SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, (int)ActualWidth, (int)ActualHeight,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);

            return new HandleRef(this, _hwnd);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            // 窗口销毁时的操作（如果需要）
        }

        // WinAPI 导入
        private const int GWL_STYLE = -16;
        private const int WS_CHILD = 0x40000000;

        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLongPtr(IntPtr hWnd, int nIndex, int dwNewLong);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
    }*/
}
