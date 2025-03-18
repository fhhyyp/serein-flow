using Microsoft.Win32;
using Serein.NodeFlow.Tool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Serein.Workbench.Themes
{
    /// <summary>
    /// DynamicCompilerApp.xaml 的交互逻辑
    /// </summary>
    public partial class DynamicCompilerView : Window
    {
        private static int count = 1;
        private readonly DynamicCompiler _compiler;
        /// <summary>
        /// 脚本代码
        /// </summary>
        public string ScriptCode { get => codeEditor.Text; set => codeEditor.Text = value; } 
        /// <summary>
        /// 编译成功回调
        /// </summary>
        public Action<Assembly> OnCompileComplete { get; set; }
        public DynamicCompilerView()
        {
            InitializeComponent();
            textboxAssemblyName.Text = $"FlowLibrary{count}";
            _compiler = new DynamicCompiler();
            // 初始化代码编辑器
            //codeEditor.Text = 
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "DLL文件|*.dll|所有文件|*.*",
                Title = "选择要引用的DLL文件"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _compiler.AddReference(openFileDialog.FileName);
                    lstReferences.Items.Add(openFileDialog.FileName);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"添加引用失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void btnBatchAdd_Click(object sender, RoutedEventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "选择包含DLL文件的文件夹";
                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        string[] dllFiles = Directory.GetFiles(folderDialog.SelectedPath, "*.dll", SearchOption.AllDirectories);
                        int successCount = 0;
                        int failCount = 0;

                        foreach (string dllFile in dllFiles)
                        {
                            try
                            {
                                _compiler.AddReference(dllFile);
                                lstReferences.Items.Add(dllFile);
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                failCount++;
                                System.Diagnostics.Debug.WriteLine($"添加引用失败 {dllFile}: {ex.Message}");
                            }
                        }

                        System.Windows.MessageBox.Show($"批量添加完成！\n成功：{successCount}个\n失败：{failCount}个",
                            "批量添加结果",
                            MessageBoxButton.OK,
                            failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"批量添加过程中发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (lstReferences.SelectedItem != null)
            {
                lstReferences.Items.Remove(lstReferences.SelectedItem);
            }
        }

        private void lstReferences_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstReferences.SelectedItem != null)
            {
                System.Windows.MessageBox.Show(lstReferences.SelectedItem.ToString(), "引用路径", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void btnCompile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtErrors.Clear();
                string code = codeEditor.Text;
                Assembly assembly = _compiler.Compile(code, textboxAssemblyName.Text);
                


                if (assembly != null)
                {
                    txtErrors.Text = "编译成功！";
                    txtErrors.Background = System.Windows.Media.Brushes.LightGreen;
                    OnCompileComplete.Invoke(assembly);
                    count++;
                }
            }
            catch (Exception ex)
            {
                txtErrors.Text = $"编译错误：\n{ex.Message}";
                txtErrors.Background = System.Windows.Media.Brushes.LightPink;
            }
        }
    }
}
