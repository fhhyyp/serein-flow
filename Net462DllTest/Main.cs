using Serein.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net462DllTest.Properties
{
    /*
     理想的项目架构：

    每一种功能拆分为新的项目

    FlowEnv - LoginControl：


               LoginControl     
               ↙       ↘
   (View-Interaction)    (Node-Interaction)  
     ↓                          ↕                    
    View ←→ ViewModel ←→  Trigger ← SingleEnum   
                 ↓             ↓  ↖  
                 ↓             ↓     ↖
                Node  →→→    Model →  Event(OnDataChanged)


    视图驱动触发器，触发器驱动数据。
    数据驱动触发器，触发器驱动视图。

    所以，这个结构≈事件驱动。


    动态的配置事件触发的原因、过程与结果。
    
     */

    public class My
    {
        public void Run()
        {
            
        }
    }
}
