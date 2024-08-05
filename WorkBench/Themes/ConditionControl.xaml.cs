using DynamicDemo.Themes.Condition;
using System.Windows;
using System.Windows.Controls;

namespace DynamicDemo.Themes
{

    /// <summary>
    /// ConditionControl.xaml 的交互逻辑
    /// </summary>
    public partial class ConditionControl : UserControl
    {
        public ConditionControl()
        {
            InitializeComponent();
        }

        //private void ConditionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    var selectedType = (ConditionType)((ComboBoxItem)ConditionTypeComboBox.SelectedItem).Tag;
        //    UpdateInputVisibility(selectedType);
        //}

        //private void UpdateInputVisibility(ConditionType type)
        //{
        //    ValueTextBox.Visibility = Visibility.Collapsed;
        //    Value2TextBox.Visibility = Visibility.Collapsed;

        //    switch (type)
        //    {
        //        case ConditionType.GreaterThan:
        //        case ConditionType.LessThan:
        //        case ConditionType.EqualTo:
        //        case ConditionType.Contains:
        //        case ConditionType.DoesNotContain:
        //            ValueTextBox.Visibility = Visibility.Visible;
        //            break;
        //        case ConditionType.InRange:
        //        case ConditionType.NotInRange:
        //            ValueTextBox.Visibility = Visibility.Visible;
        //            Value2TextBox.Visibility = Visibility.Visible;
        //            break;
        //        case ConditionType.IsTrue:
        //        case ConditionType.IsFalse:
        //        case ConditionType.IsNotEmpty:
        //            // No additional input needed
        //            break;
        //        case ConditionType.NotInSpecificRange:
        //            // Handle specific range input, possibly with a different control
        //            break;
        //    }
        //}

        private void OnAddConditionClicked(object sender, RoutedEventArgs e)
        {
            // 示例：添加一个IntConditionNode
            var intConditionNode = new IntConditionNode { Condition = ConditionType.GreaterThan, Value = 10 };
            AddConditionNode(intConditionNode);
        }

        public void AddConditionNode(ConditionNode node)
        {
            UserControl control = null;

            if (node is IntConditionNode)
            {
                control = new IntConditionControl { DataContext = node };
            }
            else if (node is BoolConditionNode)
            {
                control = new BoolConditionControl { DataContext = node };
            }
            else if (node is StringConditionNode)
            {
                control = new StringConditionControl { DataContext = node };
            }

            if (control != null)
            {
                ConditionsPanel.Children.Add(control);
            }
        }

    }
}
