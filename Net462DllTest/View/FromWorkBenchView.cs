﻿using Net462DllTest.Device;
using Net462DllTest.Signal;
using Net462DllTest.ViewModel;
using Serein.Library.Api;
using Serein.Library.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Net462DllTest
{
    public partial class FromWorkBenchView : Form
    {
        private FromWorkBenchViewModel ViewModel;

        public FromWorkBenchView(IFlowEnvironment env)
        {
            ViewModel = env.IOC.Instantiate<FromWorkBenchViewModel>(); // 创建对象并注入依赖项
            InitializeComponent();
            Init();
        }

        public void Init()
        {
            listBox1.Items.Clear();
            var enumValues = Enum.GetValues(typeof(CommandSignal)).Cast<CommandSignal>();
            foreach (var value in enumValues)
            {
                listBox1.Items.Add(value.ToString());
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            textBoxPlcInfo.Text =  ViewModel.GetDeviceInfo();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if(listBox1.SelectedItem is null)
            {
                return;
            }
            string type = listBox1.SelectedItem.ToString();
            
            if (!string.IsNullOrEmpty(type) &&  Enum.TryParse(type, out CommandSignal signal) && Enum.IsDefined(typeof(CommandSignal), signal))
            {
                Console.WriteLine($"Trigger : {type}");
                ViewModel.Trigger(signal,textBoxSpaceNum.Text);
            }
            
        }
        
        
    }
}