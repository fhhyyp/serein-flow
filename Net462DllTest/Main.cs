using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net462DllTest.Properties
{
    /*
     理想的项目架构：
    FlowEnv - LoginControl：


                LoginControl     
               ↙       ↘
   (View-Interaction)   (Node-Interaction)  
    ↓                          ↕                    
    View ←→ ViewModel ←→ Trigger ← SingleEnum   
                                ↓
                               Model
                               · DataChanged → Trigger


    视图驱动触发器，触发器驱动数据。
    数据驱动触发器，触发器驱动视图。

    所以，这个结构≈事件驱动。


    动态的配置事件触发的原因、过程与结果。
    
     */
}
