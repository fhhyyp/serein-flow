
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
    /// <summary>
    /// 全局按键事件委托
    /// </summary>
    /// <param name="key"></param>
    public delegate void KeyDownEventHandler(Key key);

    /// <summary>
    /// 全局按键抬起事件委托
    /// </summary>
    /// <param name="key"></param>
    public delegate void KeyUpEventHandler(Key key);

    /// <summary>
    /// 全局按键事件服务
    /// </summary>
    public interface IKeyEventService
    {
        /// <summary>
        /// 按键按下事件
        /// </summary>
        event KeyDownEventHandler OnKeyDown;

        /// <summary>
        /// 按键抬起事件
        /// </summary>
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

        /// <summary>
        /// 全局按键事件服务构造函数
        /// </summary>
        public KeyEventService()
        {
            var arr = Enum.GetValues<Key>();
            KeysState = new bool[arr.Length];

            // 绑定快捷键
            //HotKeyManager.SetHotKey(saveMenuItem, new KeyGesture(Key.S, KeyModifiers.Control));
        }

        private readonly bool[] KeysState;

        /// <summary>
        /// 获取某个按键的状态
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool GetKeyState(Key key)
        {
            return KeysState[(int)key];
        }



        /// <summary>
        /// 按键按下事件
        /// </summary>
        /// <param name="key"></param>
        public void KeyDown(Key key)
        {
            KeysState[(int)key] = true;
            OnKeyDown?.Invoke(key);
            //Debug.WriteLine($"按键按下事件：{key}");
        }

        /// <summary>
        /// 按键抬起事件
        /// </summary>
        /// <param name="key"></param>
        public void KeyUp(Key key)
        {
            KeysState[(int)key] = false;
            OnKeyUp?.Invoke(key);
            //Debug.WriteLine($"按键抬起事件：{key}");

        }
    }
}
