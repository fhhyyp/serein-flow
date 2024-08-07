using Newtonsoft.Json;
using System.Diagnostics;
using System.Windows;

namespace Serein.WorkBench
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public class TestObject
        {

            public NestedObject Data { get; set; }

            public class NestedObject
            {
                public int Code { get; set; }
                public int Code2 { get; set; }

                public string Tips { get; set; }

            }
            public string ToUpper(string input)
            {
                return input.ToUpper();
            }
        }

        

        public App()
        {

#if false //测试 操作表达式，条件表达式

            #region 测试数据
            string expression = "";

            var testObj = new TestObject
            {
                Data = new TestObject.NestedObject
                {
                    Code = 15,
                    Code2 = 20,
                    Tips = "测试数据"
                }
            };

            #endregion
            #region 对象操作表达式
            // 获取对象成员
            var result = SerinExpressionEvaluator.Evaluate("get .Data.Code", testObj);
            Debug.WriteLine(result); // 15

            // 设置对象成员
            SerinExpressionEvaluator.Evaluate("set .Data.Code = 20", testObj);
            Debug.WriteLine(testObj.Data.Code); // 20

            SerinExpressionEvaluator.Evaluate("set .Data.Tips = 123", testObj);
            // 调用对象方法
            result = SerinExpressionEvaluator.Evaluate($"invoke .ToUpper({SerinExpressionEvaluator.Evaluate("get .Data.Tips", testObj)})", testObj);
            Debug.WriteLine(result); // HELLO

            expression = "@number (@+1)/100";
            result = SerinExpressionEvaluator.Evaluate(expression, 2);
            Debug.WriteLine($"{expression}  ->  {result}"); // HELLO 
            #endregion
            #region 条件表达式

            expression = ".Data.Code == 15";
            var pass = SerinConditionParser.To(testObj, expression);
            Debug.WriteLine($"{expression}  -> " + pass);

            expression = ".Data.Code<int>[@*2] == 31";
            //expression = ".Data.Tips<string> contains 数据";
            pass = SerinConditionParser.To(testObj, expression);
            Debug.WriteLine($"{expression}  -> " + pass);

            expression = ".Data.Code<int> < 20";
            pass = SerinConditionParser.To(testObj, expression);
            Debug.WriteLine($"{expression}  -> " + pass);



            int i = 43;

            expression = "in 11-22";
            pass = SerinConditionParser.To(i, expression);
            Debug.WriteLine($"{i} {expression}  -> " + pass);

            expression = "== 43";
            pass = SerinConditionParser.To(i, expression);
            Debug.WriteLine($"{i} {expression}  -> " + pass);

            string str = "MY NAME IS COOOOL";
            expression = "c NAME";
            pass = SerinConditionParser.To(str, expression);
            Debug.WriteLine($"{str} {expression}  -> " + pass);

            #endregion
#endif

        }



        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit/**/(e);

            // 强制关闭所有窗口
            foreach (Window window in Windows)
            {
                window.Close();
            }
        }

        public static SereinOutputFileData? FData;
        public static string FileDataPath = "";
        private void Application_Startup(object sender, StartupEventArgs e)
        {
          
            // 示例：写入调试信息
            Debug.WriteLine("应用程序启动");

            // 检查是否传入了参数
            if (e.Args.Length == 1)
            {
                // 获取文件路径
                string filePath = e.Args[0];
                // 检查文件是否存在
                if (!System.IO.File.Exists(filePath))
                {
                    MessageBox.Show($"文件未找到：{filePath}");
                    Shutdown(); // 关闭应用程序
                    return;
                }

                try
                {
                    // 读取文件内容
                    string content = System.IO.File.ReadAllText(filePath); // 读取整个文件内容
                    FData = JsonConvert.DeserializeObject<SereinOutputFileData>(content);
                    FileDataPath = System.IO.Path.GetDirectoryName(filePath) ?? "";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取文件时发生错误：{ex.Message}");
                    Shutdown(); // 关闭应用程序
                }
            }
           //else if (1 == 1)
           //{
           //    string filePath = @"F:\临时\project\wat project.dnf";
           //    string content = System.IO.File.ReadAllText(filePath); // 读取整个文件内容
           //    FData = JsonConvert.DeserializeObject<SereinOutputFileData>(content);
           //    App.FileDataPath = System.IO.Path.GetDirectoryName(filePath);
           //}

        }
    }


}
