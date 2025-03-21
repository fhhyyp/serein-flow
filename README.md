# 自述
基于Dotnet 8 的流程可视化编辑器，需二次开发。
不定期在Bilibili个人空间上更新相关的视频。
https://space.bilibili.com/33526379


# 计划任务 2024年10月28日更新
* 重新完善远程管理与远程客户端的功能（目前仅支持远程修改节点属性、添加/移除节点、启动流程、停止流程）
* 重新完善节点树视图、IOC容器对象视图（目前残废版）
* 计划实现单步执行（暂未想到如何在不影响异步流程的前提下停止流程）
* 考虑模仿AST的方式，将流程图以“Node token → C# Code”的方式进行原生代码支持
# 如何加载我的DLL？
为你的工程添加**Serein.Library**项目引用（也可在Negut上下载），使用 **DynamicFlow** 特性标记你的类，可以参照 **Net461DllTest** 的实现（该示例工程的设计并不完善，并未做依赖分离，仅做参考）。编译为 Dll文件 后，拖入到软件中即可。
如果你不想下载整个工程文件，“FLowEdit”目录下放有“FlowEdit可视化流程编辑器.zip”压缩包，可以直接解压使用（但可能需要你安装 .Net8 运行环境）。
# 如何让我的方法成为节点？
使用 **NodeAction** 特性标记你的方法。
* 动作节点 - Action
* 触发器节点 - Flipflop
* UI节点 - UI
# 关于 IDynamicContext 说明（重要）**
 * 基本说明：IDynamicContext 是节点之间传递数据的接口、载体，其实例由 FlowEnvironment 运行环境自动实现，内部提供全局单例的环境接口，用以注册、获取实例（单例模式），一般情况下，你无须关注 FlowEnvironment 对外暴露的属性方法。
 * 重要概念：
   * 每个节点其实对应类库中的某一个方法，这些方法组合起来，加上预先设置的逻辑分支，就是一个完整的节点流。在这个节点流当中，从第一个节点开始、直到所有可达的节点，都会通过同一个流程上下文传递、共享数据。为了符合多线程操作的理念，每个运行起来的节点流之间，流程数据并不互通，从根本隔绝了“脏数据”的产生。
 * 一些重要的属性：
    * RunState - 流程状态：
      * 简述：枚举，标识流程运行的状态（初始化，运行中，运行完成）
      * 场景：类库代码中创建了运行时间较长的异步任务、或开辟了另一个线程进行循环操作时，可以在方法入参定义一个 IDynamicContext 类型入参，然后在代码中使用形成闭包，以及时判断流程是否已经结束。另外的，如果想监听项目停止运行，可以订阅 context.Env.OnFlowRunComplete 事件。
    * NextOrientation - 即将进入的分支：
      * 简述：流程分支枚举， Upstream（上游分支）、IsSucceed（真分支）、IsFail（假分支），IsError（异常分支）。
      * 场景：允许你在类库代码中操作该属性，手动控制当前节点运行完成后，下一个会执行哪一个类别的节点。
    * Exit() - 结束流程
      * 简述：顾名思义，能够让你在类库代码中提前结束当前流程运行

# 关于 DynamicNodeType 枚举的补充说明。
## 1. 不生成节点控件的枚举值：
* **Init - 初始化方法**
  * 入参：**IDynamicContext**（有且只有一个参数）。
  * 返回值：自定义，但不会处理返回值，支持异步等待。
  * 描述：在运行时首先被调用。语义类似于构造方法。建议在Init方法内初始化类、注册类等一切需要在构造函数中执行的方法。
* **Loading - 加载方法**
  * 入参：**IDynamicContext**（有且只有一个参数）。
  * 返回值：自定义，但不会处理返回值，支持异步等待。
  * 描述：当所有Dll的Init方法调用完成后，首先调用、也才会调用DLL的Loading方法。建议在Loading方法内进行业务上的初始化（例如启动Web，启动第三方服务）。
* **Exit - 结束方法**
  * 入参：**IDynamicContext**（有且只有一个参数）。
  * 返回值：自定义，但不会处理返回值，支持异步等待。
  * 描述：当结束/手动结束运行时，会调用所有Dll的Exit方法。使用场景类似于：终止内部的其它线程，通知其它进程关闭，例如停止第三方服务。
## 2. 基础节点
* **Script - 脚本节点**
  * 入参：可选可变
  * 描述：有时我们需要定义一个临时的类对象，但又不想在代码中写死属性，又或者某些流程操作中，因为业务场景需要布置大量的逻辑判断，导致流程图变得极为臃肿不堪入目，于是引入了脚本节点。脚本节点动态能力强，不同于表达式使用递归下降，而是基于AST抽象语法树调用相应的C#代码，性能至少差强人意。
  * 使用方式：
  ```
  // 定义一个类
  class Info{
    string PlcName;
    string Content;
    string LogType;
    DateTime LogTime;
  }

   // 获取必要的参数
  let flow = GetFlowApi(); // 脚本默认挂载的方法，获取脚本流程的API
  let context = GetFlowContext(); // 脚本解释器内置的方法，用以获取当前流程上下文
  
  let plc = flow.GetGlobalData("JC-PLC"); // 流程API对应的方法，获取全局数据
  let arg = flow.GetArgData(context, 0); // 从当前流程上下文获取第一个入参
  //let arg = flow.GetFlowData(context); // 从当前流程上下文获取运行时上一个节点的返回对象
  let varInfo = arg.Var; // 获取入参对象的Var属性
  let data = arg.Data; // 获取入参对象的Data属性

  let log = new Info(); // 创建一个类
  log.Content =  plc + " " + varInfo + " - 状态 Value  : " + data;
  log.PlcName = plc.Name;
  log.LogType = "info";
  log.LogTime = GetNow(); // 脚本默认挂载的方法，获取当前时间
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
       let flow = GetFlowApi(); // 获取流程API
       let data = flow.GetGlobalData("KeyName"); // 获取全局数据
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
  * 增加描述：如果入参数据为某个对象，需要得到其属性/字段，可以在表达式输入框使用“ .[property]/[field]< type> [op] value ”的方式判断条件，注意，必须使用“.”符号，这有助于显然的表达需要从入参对象中取内部某个值，另外，也可以在入参数据编辑框，使用“@Get .[property]/[field]”的方式重新定义入参数据。
  * 表达式符号说明：
  * [property] /[field] : 属性/字段
  * [op] : 操作符
    1. bool表达式：==
    2. 数值表达式 ：==、>=、 <=、in a-b （表示判断是否在a至b的数值范围内）, !in a-b（取反）；
    3. 文本表达式：==/equals（等于）、!=/notequals（不等于）、c/contains（出现过）、nc/doesnotcontain（没有出现过）、sw/startswith（开头等于）、ew/endswith（结尾等于）
  * [value] ： 条件值
## 3. 从DLL生成控件的枚举值：
* **Action - 动作**
  * 入参：自定义。如果入参类型为IDynamicContext，会传入当前的上下文；如果入参类型为NodeBase，会传入节点对应的Model。如果不显式指定参数来源，参数会尝试获取运行时上一节点返回值，并根据当前入参类型尝试进行类型转换。
  * 返回值：自定义，支持异步等待。
  * 描述：同步执行对应的方法。
* **Flipflop - 触发器**
  * 全局触发器
    * 入参：依照Action节点。
    * 返回值：Task`<IFlipflopContext<TResult>>`
    * 描述：运行开始时，所有无上级节点的触发器节点（在当前分支中作为起始节点），分别建立新的线程运行，然后异步等待触发（如果有）。这种触发器拥有独自的DynamicContext上下文（共用同一个Ioc），执行完成之后，会重新从分支起点的触发器开始等待。
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
		  public async Task<IEmbeddedContent> CreateImageControl(DynamicContext context)
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
    ![image](https://github.com/fhhyyp/serein-flow/blob/cc5f8255135b96c6bb3669bc4aa8d8167a71c262/Image/%E6%BC%94%E7%A4%BA%20-%201.png)
    ![image](https://github.com/fhhyyp/serein-flow/blob/cc5f8255135b96c6bb3669bc4aa8d8167a71c262/Image/%E6%BC%94%E7%A4%BA%20-%202.png)
    ![image](https://github.com/fhhyyp/serein-flow/blob/8f17b786f3585cabfeef60d9ab871d43b69e5461/Image/%E6%BC%94%E7%A4%BA%20-%203.png)
    ![image](https://github.com/fhhyyp/serein-flow/blob/8f17b786f3585cabfeef60d9ab871d43b69e5461/Image/%E6%BC%94%E7%A4%BA%20-%204.png)
