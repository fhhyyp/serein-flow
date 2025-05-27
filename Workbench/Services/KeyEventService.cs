
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using static System.Windows.Forms.AxHost;

namespace Serein.Workbench.Services
{
    public delegate void KeyDownEventHandler(Key key);
    public delegate void KeyUpEventHandler(Key key);

    /// <summary>
    /// 全局按键事件服务
    /// </summary>
    public interface IKeyEventService
    {
        event KeyDownEventHandler OnKeyDown;
        event KeyUpEventHandler OnKeyUp;

        /// <summary>
        /// 获取某个按键状态
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool GetKeyState(Key key);

        /// <summary>
        /// 按下了某个键
        /// </summary>
        /// <param name="key"></param>
        void KeyDown(Key key);

        /// <summary>
        /// 抬起了某个键
        /// </summary>
        /// <param name="key"></param>
        void KeyUp(Key key);

    }

    /// <summary>
    /// 管理按键状态
    /// </summary>
    public class KeyEventService : IKeyEventService
    {
        /// <summary>
        /// 按键按下
        /// </summary>
        public event KeyDownEventHandler OnKeyDown;
        /// <summary>
        /// 按键松开
        /// </summary>
        public event KeyUpEventHandler OnKeyUp;

        public KeyEventService()
        {
            var arr = Enum.GetValues<Key>();
            KeysState = new bool[arr.Length];

            // 绑定快捷键
            //HotKeyManager.SetHotKey(saveMenuItem, new KeyGesture(Key.S, KeyModifiers.Control));
        }

        private readonly bool[] KeysState;
        public bool GetKeyState(Key key)
        {
            return KeysState[(int)key];
        }




        public void KeyDown(Key key)
        {
            KeysState[(int)key] = true;
            OnKeyDown?.Invoke(key);
            Debug.WriteLine($"按键按下事件：{key}");
        }

        public void KeyUp(Key key)
        {
            KeysState[(int)key] = false;
            OnKeyUp?.Invoke(key);
            Debug.WriteLine($"按键抬起事件：{key}");

        }
    }
}
