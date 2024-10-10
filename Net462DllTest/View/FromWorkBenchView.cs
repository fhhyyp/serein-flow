
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
            InitializeComponent();
            ViewModel = env.IOC.Get<FromWorkBenchViewModel>();
            if (ViewModel is null)
            {
                Console.WriteLine("创建对象并注入依赖项");
                ViewModel = env.IOC.Instantiate<FromWorkBenchViewModel>(); 
            }
            BindData();
        }

        private void BindData()
        {
            textBoxPlcInfo.DataBindings.Add(nameof(textBoxPlcInfo.Text), ViewModel, nameof(ViewModel.DeviceInfo), false, DataSourceUpdateMode.OnPropertyChanged);
            textBoxSpaceNum.DataBindings.Add(nameof(textBoxSpaceNum.Text), ViewModel, nameof(ViewModel.SpcaeNumber), false, DataSourceUpdateMode.OnPropertyChanged);

            listBoxCommand.DataSource = Enum.GetValues(typeof(CommandSignal));
            listBoxCommand.DataBindings.Add(nameof(listBoxCommand.SelectedItem), ViewModel, nameof(ViewModel.SelectedSignal), false, DataSourceUpdateMode.OnPropertyChanged);
            listBoxCommand.SelectedIndexChanged += (s, e) => listBoxCommand.DataBindings[nameof(listBoxCommand.SelectedItem)].WriteValue();

        }
        private void FromWorkBenchView_Load(object sender, EventArgs e)
        {

        }

        private void FromWorkBenchView_FormClosing(object sender, FormClosingEventArgs e)
        {
            ViewModel.CommandCloseForm?.Execute();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            ViewModel.CommandGetParkingSpace.Execute();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ViewModel.CommandViewPlcInfo.Execute();
        }
    }
}
