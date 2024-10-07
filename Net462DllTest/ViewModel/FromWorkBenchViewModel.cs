using IoTClient;
using Net462DllTest.Trigger;
using Net462DllTest.Signal;
using Net462DllTest.Utils;
using Serein.Library.Attributes;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Net462DllTest.LogicControl;

namespace Net462DllTest.ViewModel
{
    public class FromWorkBenchViewModel : INotifyPropertyChanged
    {
        private readonly SiemensPlcDevice Device;
        private readonly ViewManagement viewManagement;
        public FromWorkBenchViewModel(SiemensPlcDevice Device,ViewManagement viewManagement)
        {
            this.Device = Device;
            this.viewManagement = viewManagement;
            InitCommand(); 
            
        }


        #region 属性绑定
        private string _spcaeNumber;
        public string SpcaeNumber
        {
            get { return _spcaeNumber; }
            set
            {
                if (_spcaeNumber != value)
                {
                    _spcaeNumber = value;
                    OnPropertyChanged(nameof(SpcaeNumber));
                }
            }
        }

        private CommandSignal _selectedSignal;
        public CommandSignal SelectedSignal
        {
            get { return _selectedSignal; }
            set
            {
                if (_selectedSignal != value)
                {
                    _selectedSignal = value;
                    OnPropertyChanged(nameof(SelectedSignal));
                }
            }
        }

        private string _deviceInfo;
        public string DeviceInfo
        {
            get { return _deviceInfo; }
            set
            {
                if (_deviceInfo != value)
                {
                    _deviceInfo = value;
                    OnPropertyChanged(nameof(DeviceInfo));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        #endregion

        #region 操作绑定

        /// <summary>
        /// 查看PLC信息
        /// </summary>
        public RelayCommand CommandViewPlcInfo { get; private set; }
        /// <summary>
        /// 调取车位
        /// </summary>
        public RelayCommand CommandGetParkingSpace { get; private set; }

        public void InitCommand()
        {
            CommandViewPlcInfo = new RelayCommand((p) =>
            {
                DeviceInfo = Device?.ToString();
            });
            CommandGetParkingSpace = new RelayCommand((p) =>
            {
                viewManagement.TriggerSignal(SelectedSignal, SpcaeNumber);
            });

        }

        #endregion


    }


    

}
