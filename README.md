# 自述
基于Dotnet 8 的流程可视化框架，运行依赖、运行环境、编辑环境全部开源，可二次开发。
不定期在Bilibili个人空间上更新相关的视频。
https://space.bilibili.com/33526379


# 计划任务 2025年6月1日更新
* 重新实现远程控制逻辑（添加对多画布多客户端在线支持）

# 如何加载我的DLL？
为你的工程添加**Serein.Library**项目引用（也可在Negut上下载），使用 **DynamicFlow** 特性标记你的类，可以参照 **Net461DllTest** 的实现（该示例工程的设计并不完善，并未做依赖分离，仅做参考）。编译为 Dll文件 后，拖入到软件中即可。
如果你不想下载整个工程文件，“FLowEdit”目录下放有“FlowEdit可视化流程编辑器.zip”压缩包，可以直接解压使用（但可能需要你安装 .Net8 运行环境）。
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
* **ExpOp- 表达式节点**
  * 入参: 自定义的表达式。
  * 取值表达式：@Get
    * 描述：有时节点返回了object，但下一个节点只需要对象中某个属性，而非整个对象。如果修改节点的定义，有可能破坏了代码的封装，为了解决这个痛点，于是增加了表达式功能。
    * 使用方法：
      1. 获取对象的属性成员：
         ~~~~
         @Get .[property]/[field]
         ~~~~
      2. 获取对象的数组成员中下标为22的项：
         ~~~~
         @Get .array[22]
         ~~~~
      3. 获取对象的字典成员中键为“33”的值：
         ~~~
         @Get .dict[33]
         ~~~
      4. 获取对象“ID”属性并转为int：
         ~~~
         @Get .ID<int>
         ~~~
      5. 获取KeyName为【 MyDevice 】全局数据：
         ~~~
         @Get #MyDevice#
         ~~~
      6. 从全局数据【 MyDevice 】中获取“IP”属性：
         ~~~
         @Get #MyDevice#.IP
         ~~~
  * 数据类型转换表达式：@Dtc
    * 描述：有时需要显式的设置节点参数值，但参数接收了其它的类型，需要经过一次转换，将显式的文本值转为入参数据类型。
    * 使用方法：
      ~~~
      @Dtc <long>1233
      @Dtc <bool>True
      @Dtc <DateTime>2024-12-24 11:13:42 （注：如果右值为“now”，则自动获取当前时间）
      ~~~
* **ExpCondition - 条件表达式节点**
  * 入参: 自定义。
  * 描述：与表达式节点不同，条件表达式节点是判断条件是否成立，如果成立，返回true，否则返回false，如果表达式执行失败，而进入 error 分支。
  * 使用方式：
    * 入参说明：默认从上一节点获取，也可以显式设定值，也可以参考表达式节点，使用“@Get .[property]/[field]”的方式重新定义入参数据，用以条件表达式判断。
    * 条件表达式：默认“PASS”，代表跳过判断直接进入下一个分支。
  * 条件表达式格式“.[property]/[field]< type> [op] value”，注意，开头必须使用“.”符号，这有助于显然的表达需要从入参对象中取内部某个值。
  * 表达式符号说明：
    * [property] /[field] : 属性/字段
    * [op] : 操作符
      1. bool表达式：==
      2. 数值表达式 ：==、>=、 <=、in a-b （表示判断是否在a至b的数值范围内）, !in a-b（取反）；
      3. 文本表达式：==/equals（等于）、!=/notequals（不等于）、c/contains（出现过）、nc/doesnotcontain（没有出现过）、sw/startswith（开头等于）、ew/endswith（结尾等于）
    * < type> ：指定需要转换为某个类型，可不选。
    * [value] ： 条件值
    * 使用示例：
    ~~~
    场景1：上一个节点传入了该对象（伪代码）：
    class Data
    {
        string Name; // 性能
        string Age; // 年龄，外部传入了文本类型
        string IdentityCardNumber; // 身份证号

    }
    需求：需要判断年龄是否在某个区间，例如需要大于18岁，小于35岁。
    条件表达式：.Age<int> in 18-35

  

    需求：需要判断是否是北京身份证（开头为”1100”）。
    条件表达式：.IdentityCardNumber sw 1100
    另一种方法：
          入参使用表达式：@Get .IdentityCardNumber
         条件表达式：sw 1100

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
