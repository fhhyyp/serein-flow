using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Services
{
    delegate void KeyDownEventHandler(Key key);
    delegate void KeyUpEventHandler(Key key);

    /// <summary>
    /// 全局事件服务
    /// </summary>
    internal interface IKeyEventService
    {
        event KeyDownEventHandler KeyDown;
        event KeyUpEventHandler KeyUp;

        /// <summary>
        /// 获取某个按键状态
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        bool GetKeyState(Key key);
        /// <summary>
        /// 设置某个按键的状态
        /// </summary>
        /// <param name="key"></param>
        /// <param name="state"></param>
        void SetKeyState(Key key, bool statestate);
    }

    /// <summary>
    /// 管理按键状态
    /// </summary>
    internal class KeyEventService : IKeyEventService
    {

        /// <summary>
        /// 按键按下
        /// </summary>
        public event KeyDownEventHandler KeyDown;
        /// <summary>
        /// 按键松开
        /// </summary>
        public event KeyUpEventHandler KeyUp;

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
        public void SetKeyState(Key key, bool state)
        {
            if (state)
            {
                KeyDown?.Invoke(key);
            }
            else
            {
                KeyUp?.Invoke(key);
            }
            //Debug.WriteLine($"按键事件：{key} - {state}");
            KeysState[(int)key] = state;
        }
    }
}
