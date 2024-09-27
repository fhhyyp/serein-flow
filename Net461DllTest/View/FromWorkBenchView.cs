using Net461DllTest.Device;
using Net461DllTest.Signal;
using Net461DllTest.ViewModel;
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
        [AutoInjection]
        public FromWorkBenchViewModel ViewModel { get; set; }
        public FromWorkBenchView()
        {
            InitializeComponent();
            listBox1.Items.Clear();
            var enumValues = Enum.GetValues(typeof(OrderSignal)).Cast<OrderSignal>();
            foreach (var value in enumValues)
            {
                listBox1.Items.Add(value.ToString());
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            textBox1.Text =  ViewModel.GetDeviceInfo();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string type = listBox1.SelectedItem.ToString();
            
            if (Enum.TryParse(type, out OrderSignal signal) && Enum.IsDefined(typeof(OrderSignal), signal))
            {
                Console.WriteLine($"Trigger : {type}");
                ViewModel.Trigger(signal);
            }
            
        }
        
        
    }
}
