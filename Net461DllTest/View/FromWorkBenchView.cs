using Net461DllTest.Device;
using Net461DllTest.Signal;
using Net461DllTest.ViewModel;
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

namespace Net461DllTest
{
    public partial class FromWorkBenchView : Form
    {
        private FromWorkBenchViewModel ViewModel;

        public FromWorkBenchView(IFlowEnvironment env)
        {
            ViewModel = env.IOC.Instantiate<FromWorkBenchViewModel>();
            InitializeComponent();
            Init();
        }

        public void Init()
        {
            listBox1.Items.Clear();
            var enumValues = Enum.GetValues(typeof(OrderSignal)).Cast<OrderSignal>();
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
            
            if (!string.IsNullOrEmpty(type) &&  Enum.TryParse(type, out OrderSignal signal) && Enum.IsDefined(typeof(OrderSignal), signal))
            {
                Console.WriteLine($"Trigger : {type}");
                ViewModel.Trigger(signal,textBoxSpaceNum.Text);
            }
            
        }
        
        
    }
}
