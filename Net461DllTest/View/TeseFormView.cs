using Net461DllTest.Data;
using Net461DllTest.Signal;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Net461DllTest.View
{
    public partial class TeseFormView : Form
    {
        [AutoInjection]
        public MyData MyData { get; set; }

        public TeseFormView()
        {
            InitializeComponent();
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MyData.Count = 0;
        }
    }
}
