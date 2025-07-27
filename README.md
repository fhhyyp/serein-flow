# 自述
基于Dotnet 8 的流程可视化框架，运行依赖、运行环境、编辑环境全部开源，可二次开发。
不定期在Bilibili个人空间上更新相关的视频。
https://space.bilibili.com/33526379


# 计划任务 2025年7月28日更新
* 完善“流程图转c#代码”功能，正在构思热更新的实现方式。
* 仍在构思如何实现多客户端的远程控制逻辑。

# 如何绘制流程？
  * 准备阶段
     1. 编译 Serein.Workbench 项目，确保你有一个可运行的工作台。
     2. 新增一个类库项目，添加 **Serein.Library** 项目引用（也可在Negut上下载）。
  * 开始
     1. 类库项目中，添加一个类，使用 **DynamicFlow** 特性标记你的类。
     2. 类中，使用 **NodeAction** 特性标记你的方法。
     3. 编译项目，将生成的Dll文件以拖拽方式，放入工作台左侧的“类库”面板中，工作台会自动加载该Dll文件，并显示出你标记的节点方法。
  * 绘制流程
     1. 在工作台中，新建一个画布。
     2. 右键按住左侧的“类库”面板中的方法，拖拽到画布上，工作台会自动生成一个节点。
     3. 创建第二个节点，鼠标放在节点的“连接器”上，按住鼠标左键拖拽到第一个节点的“连接器”上，工作台会自动生成一条连线，表示两个节点之间的连接关系。
     4. 创建更多的节点，在它们之间创建链接。
  * 运行流程
     1. 在当前画布中，确保你有一个起始节点（右键点击节点，选择“设置为起始节点”）。
     2. 点击工作台顶部的“运行”按钮，选择从当前画布运行，工作台会自动运行当前画布中的流程（从起始节点开始）。
     3. 其它方法：
        * 需要单独从某个节点开始运行时，在画布空白区域按住右键移动进行框选。选择你想开始运行的节点（只能选取一个），按下 F5 即可开始运行。
        * 在工作台菜单栏的“运行”按钮下拉菜单中，选择“运行所有画布”，工作台会自动运行所有画布中的流程（从每个画布的起始节点开始）。 
  * 保存项目与加载项目
     1. 流程图绘制完成后，在工作台菜单栏的“视图”按钮下拉菜单中，选择“保存项目”，工作台将弹出文件选择器，由你选定本地路径进行保存。
     2. 需要加载其它项目时，在工作台菜单栏的“视图”按钮下拉菜单中，选择“加载本地项目”，工作台将弹出文件选择器，由你选定本地路径进行加载。
  * 查看输出
     1. 在工作台菜单栏的“视图”按钮下拉菜单中，选择“输出窗口”，工作台会自动打开一个输出窗口，显示当前工作台输出的信息（包含绘制异常、运行异常等等）。
   
# 如何让我的方法成为节点？
使用 **NodeAction** 特性标记你的方法。
* 动作节点 - Action
* 触发器节点 - Flipflop
* UI节点 - UI
# 关于 IFlowContext 说明（重要）
 * 基本说明：IFlowContext 是节点之间传递数据的接口、载体，其实例由 FlowEnvironment 运行环境自动实现，内部提供全局单例的环境接口，用以注册、获取实例（单例模式），一般情况下，你无须关注 FlowEnvironment 对外暴露的属性方法。
 * 重要概念：
   * 每个节点其实对应类库中的某一个方法，这些方法组合起来，加上预先设置的逻辑分支，就是一个完整的节点流。在这个节点流当中，从第一个节点开始、直到所有可达的节点，都会通过同一个流程上下文传递、共享数据。为了符合多线程操作的理念，每个运行起来的节点流之间，流程数据并不互通，从根本隔绝了“脏数据”的产生。
 * 一些重要的属性：
    * RunState - 流程状态：
      * 简述：枚举，标识流程运行的状态（初始化，运行中，运行完成）
      * 场景：类库代码中创建了运行时间较长的异步任务、或开辟了另一个线程进行循环操作时，可以在方法入参定义一个 IFlowContext 类型入参，然后在代码中使用形成闭包，以及时判断流程是否已经结束。另外的，如果想监听项目停止运行，可以订阅 context.Env.OnFlowRunComplete 事件。
    * NextOrientation - 即将进入的分支：
      * 简述：流程分支枚举， Upstream（上游分支）、IsSucceed（真分支）、IsFail（假分支），IsError（异常分支）。
      * 场景：允许你在类库代码中操作该属性，手动控制当前节点运行完成后，下一个会执行哪一个类别的节点。
    * Exit() - 结束流程
      * 简述：顾名思义，能够让你在类库代码中提前结束当前流程运行

# 关于 DynamicNodeType 枚举的补充说明。
## 1. 不生成节点控件的枚举值：
* **Init - 初始化方法**
  * 入参：**IFlowContext**（有且只有一个参数）。
  * 返回值：自定义，但不会处理返回值，支持异步等待。
  * 描述：在运行时首先被调用。语义类似于构造方法。建议在Init方法内初始化类、注册类等一切需要在构造函数中执行的方法。
* **Loading - 加载方法**
  * 入参：**IFlowContext**（有且只有一个参数）。
  * 返回值：自定义，但不会处理返回值，支持异步等待。
  * 描述：当所有Dll的Init方法调用完成后，首先调用、也才会调用DLL的Loading方法。建议在Loading方法内进行业务上的初始化（例如启动Web，启动第三方服务）。
* **Exit - 结束方法**
  * 入参：**IFlowContext**（有且只有一个参数）。
  * 返回值：自定义，但不会处理返回值，支持异步等待。
  * 描述：当结束/手动结束运行时，会调用所有Dll的Exit方法。使用场景类似于：终止内部的其它线程，通知其它进程关闭，例如停止第三方服务。
## 2. 基础节点
* **FLowCall - 流程接口**
    * 入参：根据目标节点变化。
    * 描述：有时我们需要将流程图的逻辑解耦，单独使用某个画布编写逻辑，抽取出一个通用的处理模板，在其它流程图中像接口一样进行调用。
    * 使用方式：
        1. 创建两个画布，分别命名画布A，画布B。
        2. 在画布A上，选择希望暴露出去的一个节点，勾选“全局公开”。
        3. 在画布B上，拖拽创建“流程接口”节点，画布选择“画布A”，节点选择公开的节点。
        4. 在画布B上正常调用即可。
     * 关于“参数共享”设置的说明：
        * 如果启用了，那么流程接口对应的节点，入参参数将在全局保持一致，一处地方修改后，处处同步（仅限于启用了这一设置的相同的流程接口节点）。
        * 如果没有启用，那么流程接口将会从目标节点拷贝相同的入参，单独使用，并不受目标节点的入参参数修改而修改。
* **Script - 脚本节点**
  * 入参：可选可变
  * 描述：有时我们需要定义一个临时的类对象，但又不想在代码中写死属性，又或者某些流程操作中，因为业务场景需要布置大量的逻辑判断，导致流程图变得极为臃肿不堪入目，于是引入了脚本节点。脚本节点动态能力强，不同于表达式使用递归下降，而是基于AST抽象语法树调用相应的C#代码，性能至少差强人意。
  * 使用方式：
  ```
  // 入参是一个对象，入参每次是 info ，有 Var 、 Data属性。
  class Info{
    string PlcName;
    string Content;
    string LogType;
    DateTime LogTime;
  }
  
  plc = global("JC-PLC"); // 获取全局数据节点中的数据
  
  log = new Info() { // 创建一个类
    Content =  plc + " " + info.Var + " - 状态 Value  : " + info.Data,
    PlcName = plc.Name,
    LogType = "info",
    LogTime = now(), // 脚本默认挂载的方法，获取当前时间
  };
  return log; // 返回对象
  ```
* **GlobalData - 全局数据节点**
  * 入参：KeyName ，在整个流程环境中标识某个数据的key值。
  * 描述：有时需要获取其它节点的数据，但如果强行在两个节点之间进行连线，会让项目流程图变得无比丑陋，如果在类库代码中自己对全局数据进行维护，可能也不太优雅，所以引入了全局数据节点（全局变量）
  * 使用方式：全局数据节点实质上只是一个节点容器，这意味着你能将任意节点拖拽到该容器节点上，当流程执行到这个容器节点（全局数据节点）时，会自动调用容器内部的节点对应的方法，并将返回的数据保存在运行环境维护的Map中。
  * 其它获取到全局数据的方式：
    1. 表达式 ：
       ~~~
       @Get #KeyName# // 使用##符号表达全局数据KeyName的标识符
       ~~~
    2. Script代码：
       ~~~~
       data = global("YOUR-NAME"); // 获取全局数据节点中的数据
       ~~~~
    3. C# 代码中（不建议）：
       ~~~
       SereinEnv.GetFlowGlobalData("KeyName"); // 获取全局数据
       SereinEnv.AddOrUpdateFlowGlobalData("KeyName", obj); // 设置/更新全局数据，不建议
       ~~~
## 3. 从DLL生成控件的枚举值：
* **Action - 动作**
  * 入参：自定义。如果入参类型为IFlowContext，会传入当前的上下文；如果入参类型为IFlowNode，会传入节点对应的实体Model。如果不显式指定参数来源，参数会尝试获取运行时上一节点返回值，并根据当前入参类型尝试进行类型转换。
  * 返回值：自定义，支持异步等待。
  * 描述：同步执行对应的方法。
* **Flipflop - 触发器**
  * 全局触发器
    * 入参：依照Action节点。
    * 返回值：Task`<IFlipflopContext<TResult>>`
    * 描述：运行开始时，所有无上级节点的触发器节点（在当前分支中作为起始节点），分别建立新的线程运行，然后异步等待触发（如果有）。这种触发器拥有独自的IFlowContext上下文（共用同一个Ioc），执行完成之后，会重新从分支起点的触发器开始等待。
  * 分支中的触发器
    * 入参：依照Action节点。
    * 返回值：Task`<IFlipflopContext<TResult>>`
    * 描述：接收上一节点传递的上下文，同样进入异步等待，但执行完成后不会再次等待自身（只会触发一次）。
  * 关于 IFlipflopContext`<TResult>` 接口
    * 基本说明：IFlipflopContext是一个接口，你无须关心内部实现。
    * 参数描述：State，状态枚举描述（Succeed、Cancel、Error、Cancel），如果返回Cancel，则不会执行后继分支，如果返回其它状态，则会获取对应的后继分支，开始执行。
    * 参数描述：Type，触发状态描述（External外部触发，Overtime超时触发），当你在代码中的其他地方主动触发了触发器，则该次触发类型为External，当你在创建触发器后超过了指定时间（创建触发器时会要求声明超时时间），则会自动触发，但触发类型为Overtime，触发参数未你在创建触发器时指定的值）
    * 参数描述：Value，触发时传递的参数。
	* 使用场景：配合 FlowTrigger`<TEnum>` 使用，例如定时从PLC中获取状态，当某个变量发生改变时，会通知相应的触发器，如果需要，可以传递对应的数据。
* **UI - 自定义控件**
  * 入参：默认使用上一节点返回值。
  * 返回值：IEmbeddedContent 接口
  * 描述：将类库中的WPF UserControl嵌入并显示在一个节点上，显示在工作台UI中。例如在视觉处理流程中，需要即时的显示图片。
  * 关于 IEmbeddedContent 接口
    * IEmbeddedContent 需要由你实现，框架并不负责
    ```
	        [DynamicFlow("[界面显示]")]
	        internal class FlowControl
	        {
		        [NodeAction(NodeType.UI)]
		        public async Task<IEmbeddedContent> CreateImageControl(FlowContext context)
		        {
			        WpfUserControlAdapter adapter = null;
			        // 其实你也可以直接创建实例
			        // 但如果你的实例化操作涉及到了对UI元素修改，还是建议像这里一样使用异步方法
			        await context.Env.UIContextOperation.InvokeAsync(() =>
			        {
				        var userControl = new UserControl();
				        adapter = new WpfUserControlAdapter(userControl, userControl);
			        });
			        return adapter;
		        }
	        }
	    public class WpfUserControlAdapter : IEmbeddedContent
	    {
		    private readonly UserControl userControl;
		    private readonly IFlowControl flowControl;

		    public WpfUserControlAdapter(UserControl userControl, IFlowControl flowControl)
		    {
			    this.userControl = userControl;
			    this.flowControl= flowControl;
		    }

		    public IFlowControl GetFlowControl()
		    {
			    return flowControl;
		    }

		    public object GetUserControl()
		    {
			    return userControl;
		    }
	    }
    ```
  
## 演示：
    ![image1](https://github.com/fhhyyp/serein-flow/blob/cc5f8255135b96c6bb3669bc4aa8d8167a71c262/Image/%E6%BC%94%E7%A4%BA%20-%201.png)
    ![image2](https://github.com/fhhyyp/serein-flow/blob/cc5f8255135b96c6bb3669bc4aa8d8167a71c262/Image/%E6%BC%94%E7%A4%BA%20-%202.png)
    ![image3](https://github.com/fhhyyp/serein-flow/blob/8f17b786f3585cabfeef60d9ab871d43b69e5461/Image/%E6%BC%94%E7%A4%BA%20-%203.png)
    ![image4](https://github.com/fhhyyp/serein-flow/blob/8f17b786f3585cabfeef60d9ab871d43b69e5461/Image/%E6%BC%94%E7%A4%BA%20-%204.png)
