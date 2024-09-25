# 自述
基于WPF（Dotnet 8）的流程可视化编辑器，需二次开发。
不定期在Bilibili个人空间上更新相关的视频。
https://space.bilibili.com/33526379

# 计划任务 2024年9月17日更新
* （重要+优先）正在实现断点功能，单步执行
* 计划实现节点树视图、IOC容器对象视图
* 正在计划实现网络方面的通讯，方便传出、传入数据
* 正在计划新增基础节点“属性包装器”，用来收集各个节点的数据，包装成匿名对象/Json类型（暂时未想到如何设计）
* 正在计划实现对数据传递的包装， 尽可能避免拆箱、装箱，优化传递效率。（可能做不到）


# 如何加载我的DLL？
使用 **DynamicFlow** 特性标记你的类，可以参照 **MyDll** 与 **SereinWAT** 的实现。编译为 Dll文件 后，拖入到软件中即可。

# 如何让我的方法成为节点？
使用 **NodeAction** 特性标记你的方法。
* 动作节点 - Action
* 触发器节点 - Flipflop

# 关于 DynamicNodeType 枚举的补充说明。

## 1. 不生成节点控件的枚举值：
  * **Init - 初始化方法**
    * 入参：**IDynamicContext**（有且只有一个参数）。
    * 返回值：自定义，但软件目前不支持接收返回值。
    * 描述：在运行时首先被调用。语义类似于构造方法。建议在Init方法内初始化类、注册类等一切需要在构造函数中执行的方法。

  * **Loading - 加载方法**
    * 入参：**IDynamicContext**（有且只有一个参数）。
    * 返回值：自定义，但软件目前不支持接收返回值。
    * 描述：当所有Dll的Init方法调用完成后，首先调用、也才会调用DLL的Loading方法。建议在Loading方法内进行业务上的初始化（例如启动Web，启动第三方服务）。

  * **Exit - 结束方法**
    * 入参：**IDynamicContext**（有且只有一个参数）。
    * 返回值：自定义，但软件目前不支持接收返回值。
    * 描述：当结束/手动结束运行时，会调用所有Dll的Exit方法。使用场景类似于：终止内部的其它线程，通知其它进程关闭，例如停止第三方服务。
  * **关于IDynamicContext说明**
    * 基本说明：IDynamicContext是动态上下文接口，内部提供用以注册、获取实例的IOC容器SereinIoc接口，以及运行环境IFlowEnvironment接口，一般情况下，你无须关注IFlowEnvironment对外暴露的属性方法。除此之外，还有一个用以创建定时循环任务的方法CreateTimingTask，通过该方法可以实现类似于定时器的功能，它的运行周期由运行环境进行管理。

## 2. 基础节点
 * 待更新

## 3. 从DLL生成控件的枚举值：
  * **Action - 动作**
    * 入参：自定义。如果传入DynamicContext，会传入当前的上下文；如果传入NodeBase，会传入节点对应的Model。第一个非[Explicit]特性的参数会尝试从上一节点的获取FlowData变量，并根据当前入参类型，尝试进行类型转换。
    * 返回值：自定义，返回值由对应的Model类的[object?]FlowData变量接收。
    * 描述：同步执行对应的方法。
    
  * **Flipflop - 触发器**
    * 全局触发器
      * 入参：依照Action节点。
      * 返回值：Task`<IFlipflopContext>`
      * 描述：运行开始时，所有无上级节点的触发器节点（在当前分支中作为起始节点），分别建立新的线程运行，然后异步等待触发（如果有）。这种触发器拥有独自的DynamicContext上下文（共用同一个Ioc），执行完成之后，会重新从分支起点的触发器开始等待。
    * 分支中的触发器
        * 入参：依照Action节点。
        * 返回值：Task`<IFlipflopContext>`
        * 描述：接收上一节点传递的上下文，同样进入异步等待，但执行完成后不会再次等待自身（只会触发一次）。
    * IFlipflopContext
      * 基本说明：IFlipflopContext是一个接口，你无须关心内部实现。
      * 参数描述：State，状态枚举描述（Succeed、Cancel、Error、Cancel），如果返回Cancel，则不会执行后继分支，如果返回其它状态，则会获取对应的后继分支，开始执行。
      * 参数描述：TriggerData，内部分别有状态描述、触发的参数。触发状态有两种（External外部触发，Overtime超时触发），当你在代码中的其他地方主动触发了触发器，则该次触发类型为External，当你在创建触发器后超过了指定时间（创建触发器时会要求声明超时时间），则会自动触发，但触发类型为Overtime，触发参数未你在创建触发器时指定的值）
    * 使用场景：配合ChannelFlowTrigger`<TEnum>`使用，定时从PLC中获取状态，当某个变量发生改变时，会通知持有 Channel`<TriggerData>`的触发器，如果需要，可以传递对应的数据。
演示：
![image](https://github.com/fhhyyp/serein-flow/blob/cc5f8255135b96c6bb3669bc4aa8d8167a71c262/Image/%E6%BC%94%E7%A4%BA%20-%201.png)
![image](https://github.com/fhhyyp/serein-flow/blob/cc5f8255135b96c6bb3669bc4aa8d8167a71c262/Image/%E6%BC%94%E7%A4%BA%20-%202.png)

