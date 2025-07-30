using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows;

namespace Serein.Workbench.Themes
{
    /// <summary>
    /// BindableRichTextBox 是一个可绑定的 RichTextBox 控件，允许将 FlowDocument 对象绑定到其 Document 属性。
    /// </summary>
    public partial class BindableRichTextBox : RichTextBox
    {
        /// <summary>
        /// BindableRichTextBox 的依赖属性，允许绑定 FlowDocument 对象到 RichTextBox 的 Document 属性。
        /// </summary>
        public new FlowDocument Document
        {
            get { return (FlowDocument)GetValue(DocumentProperty); }
            set { SetValue(DocumentProperty, value); }
        }
        // Using a DependencyProperty as the backing store for Document.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DocumentProperty =
            DependencyProperty.Register("Document", typeof(FlowDocument), typeof(BindableRichTextBox), new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnDucumentChanged)));
        private static void OnDucumentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            RichTextBox rtb = (RichTextBox)d;
            rtb.Document = (FlowDocument)e.NewValue;
        }
    }
}
