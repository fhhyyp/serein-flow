using IoTClient.Enums;
using Net462DllTest.Enums;
using System;
using static Net462DllTest.Signal.PlcVarInfo;

namespace Net462DllTest.Signal
{

    [AttributeUsage(AttributeTargets.Field)]
    public class PlcVarInfoAttribute : Attribute
    {
        public PlcVarInfo Info { get; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="dateType">数据类型</param>
        /// <param name="address">变量地址</param>
        /// <param name="notificationType">通知行为类型</param>
        /// <param name="isReadOnly">是否只读</param>
        /// <param name="isTimingRead">是否定时刷新</param>
        /// <param name="interval">刷新间隔（ms）</param>
        public PlcVarInfoAttribute(DataTypeEnum dateType,
                                string address,
                                OnNotificationType notificationType,
                                bool isReadOnly = false,
                                bool isTimingRead = true,
                                int interval = 1000
                                )
        {
            Info = new PlcVarInfo()
            {
                DataType = dateType,
                IsReadOnly = isReadOnly,
                Address = address,
                Interval = interval,
                IsTimingRead= isTimingRead,
                NotificationType = notificationType,
            };
        }
    }

    public class PlcVarInfo
    {
        public enum OnNotificationType
        {
            /// <summary>
            /// 刷新时通知（每次写入Model后都会触发相应的触发器）
            /// </summary>
            OnRefresh,
            /// <summary>
            /// 改变时才通知（与Model对应数据不一致时才会触发相应的触发器）
            /// </summary>
            OnChanged,
        }

        /// <summary>
        /// 变量类型
        /// </summary>
        public PlcVarName Name { get; set; }
        /// <summary>
        /// 数据类型
        /// </summary>
        public DataTypeEnum DataType { get; set; }
        /// <summary>
        /// 变量地址
        /// </summary>
        public string Address { get; set; }
        /// <summary>
        /// 变量是否只读
        /// </summary>
        public bool IsReadOnly { get; set; }
        /// <summary>
        /// 是否定时刷新
        /// </summary>
        public bool IsTimingRead { get; set; }
        /// <summary>
        /// 刷新间隔（ms）
        /// </summary>
        public int Interval { get; set; } = 100; // 100ms
        public OnNotificationType NotificationType { get; set; } = OnNotificationType.OnChanged;
        public override string ToString()
        {
            if (IsTimingRead)
            {
                return $"数据:{Name},类型:{DataType},地址:{Address},只读:{IsReadOnly}，自动刷新:{IsTimingRead},刷新间隔:{Interval}";
            }
            else
            {
                return $"数据:{Name},类型:{DataType},地址:{Address},只读:{IsReadOnly}";
            }
        }
    }


}
