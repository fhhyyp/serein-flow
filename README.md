# 自述
基于WPF（Dotnet 8）的流程可视化编辑器，需二次开发。Bilibili个人空间：https://space.bilibili.com/33526379，不定期更新相关的视频。

# 如何加载我的DLL？
使用 **DynamicFlow** 特性标记你的类，可以参照 **MyDll** 与 **SereinWAT** 的实现。编译为 Dll文件 后，拖入到软件中即可。

# 如何让我的方法成为节点？
使用 **MethodDetail** 特性标记你的方法。
* 动作节点 - Action
* 触发器节点 - Flipflop

# 关于 DynamicNodeType 枚举的补充说明。

## 1. 不生成节点控件的枚举值：
  * **Init - 初始化方法**
    * 入参：**DynamicContext**（有且只有一个参数）。
    * 返回值：自定义，但软件目前不支持接收返回值。
    * 描述：在运行时首先被调用。语义类似于构造方法。建议在Init方法内初始化类、注册类等一切需要在构造函数中执行的方法。

  * **Loading - 加载方法**
    * 入参：**DynamicContext**（有且只有一个参数）。
    * 返回值：自定义，但软件目前不支持接收返回值。
    * 描述：当所有Dll的Init方法调用完成后，首先调用、也才会调用DLL的Loading方法。建议在Loading方法内进行业务上的初始化（例如启动Web，启动第三方服务）。

  * **Exit - 结束方法**
    * 入参：**DynamicContext**（有且只有一个参数）。
    * 返回值：自定义，但软件目前不支持接收返回值。
    * 描述：当结束/手动结束运行时，会调用所有Dll的Exit方法。使用场景类似于：终止内部的其它线程，通知其它进程关闭，例如停止第三方服务。

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
      * 返回值：Task<FlipflopContext>
      * 描述：运行开始时，所有无上级节点的触发器节点（在当前分支中作为起始节点），分别建立新的线程运行，然后异步等待触发（如果有）。这种触发器拥有独自的DynamicContext上下文（共用同一个Ioc），执行完成之后，会重新从分支起点的触发器开始等待。
    * 分支中的触发器
        * 入参：依照Action节点。
        * 返回值：Task<FlipflopContext>
        * 描述：接收上一节点传递的上下文，同样进入异步等待，但执行完成后不会再次等待自身（只会触发一次）。
    * FlipflopContext
      * 描述：内部有一套枚举描述，Succeed、Cancel，如果返回Succeed，会通过所有下级节点集合创建Task集合，然后调用WaitAll()进行等待（每个Task会新建新的DynamicContext上下文（共用同一个Ioc））。如果返回Cancel，则什么也不做。
    * 使用场景：配合TcsSignal<TEnum>使用，定时从PLC中获取状态，当某个变量发生改变时，会通知持有TaskCompletionSource的触发器，如果需要，可以传递对应的数据。
演示：
![image](https://github.com/fhhyyp/serein-flow/blob/cc5f8255135b96c6bb3669bc4aa8d8167a71c262/Image/%E6%BC%94%E7%A4%BA%20-%201.png)
![image](https://github.com/fhhyyp/serein-flow/blob/cc5f8255135b96c6bb3669bc4aa8d8167a71c262/Image/%E6%BC%94%E7%A4%BA%20-%202.png)

