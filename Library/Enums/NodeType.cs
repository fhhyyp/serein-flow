using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 用来判断该方法属于什么节点，使运行环境决定方法的运行逻辑
    /// </summary>
    public enum NodeType
    {

        /// <summary>
        /// 初始化，流程启动时执行（不生成节点）
        /// <para>可以异步等待</para>
        /// </summary>
        Init,
        /// <summary>
        /// 开始载入，流程启动时执行（不生成节点）
        /// <para>可以异步等待</para>
        /// </summary>
        Loading,
        /// <summary>
        /// 结束，流程结束时执行（不生成节点）
        /// <para>可以异步等待</para>
        /// </summary>
        Exit,


        /// <summary>
        /// <para>触发器节点，必须为标记在可异步等待的方法，建议与继承了 FlowTriggerk&lt;TEnum&gt; 的实例对象搭配使用</para>
        /// <para>方法返回值必须为Task&lt;IFlipflopContext&lt;TResult&gt;&gt;，若为其它返回值，将不会创建节点。</para>
        /// <para>触发器根据在分支中的位置，分为两种类型：流程分支中的触发器、全局触发器</para>
        /// <para>一般的触发器：存在于分支某处，也可能是分支的终点，但一定不是流程的起点与分支的起点。</para>
        /// <para>一般的触发器行为：在当前分支中执行一次之后不再执行，一般用于等待某个操作的响应。</para>
        /// <para>一般的触发器入参：如果使用了 FlowTriggerk&lt;TEnum&gt; ，就会至少有一个枚举类型的参数，参数类型与 TEnum 泛型一致。</para>
        /// <para>全局触发器：没有上游分支、同时并非流程的起始节点。</para>
        /// <para>全局触发器行为：全局触发器会循环执行，直到流程结束。</para>
        /// <para>一般的触发器入参：如果使用了 FlowTriggerk&lt;TEnum&gt; ，就会至少有一个枚举类型的参数，参数类型与 TEnum 泛型一致。</para>
        /// </summary>
        Flipflop,
        /// <summary>
        /// <para>动作节点，可以异步等待</para>
        /// <para>如果不显式的设置入参数据（例如文本、@Get取值表达式），就会默认使用该节点的运行时上一个节点的数据。</para>
        /// <para>假如上一节点是某个对象，但入参需要的是对象中某个属性/字段，则建议使用取值表达式、表达式节点获取所需要的数据。</para>
        /// <para>关于@Get取值表达式的使用方法：</para>
        ///  <para>public class UserInfo                                                         </para>
        ///  <para>{                                                                             </para>
        ///  <para>    public string Name; // 取值表达式：@Get .Name                       </para>
        ///  <para>    public string[] PhoneNums; // 获取第1项的取值表达式：@Get .PhoneNums[0]   </para>
        ///  <para> }                                                                            </para>
        /// <para>取值表达式可以符合直觉的如此获取实例成员：@Get .Data.Array[2].Data......</para>
        /// <para>格式说明：@Get大小写不敏感，然后空一格，需要标记“.”，然后才是获取成员名称（成员名称大小写敏感）。</para>
        /// </summary>
        Action,
    }

    
    
    class UserInfo
    {
        public string Name;
        public int Id;
        public string[] PhoneNums;
    }
    
    
    

    /// <summary>
    /// 生成的节点控件
    /// </summary>
    public enum NodeControlType
    {
        /// <summary>
        /// 预料之外的情况
        /// </summary>
        None,
        /// <summary>
        /// 动作节点
        /// </summary>
        Action,
        /// <summary>
        /// 触发器节点
        /// </summary>
        Flipflop,

        /// <summary>
        /// 表达式操作节点
        /// </summary>
        [Description("base")]
        ExpOp,
        /// <summary>
        /// 表达式操作节点
        /// </summary>
        [Description("base")]
        ExpCondition,
        /// <summary>
        /// 条件节点区域
        /// </summary>
        [Description("base")] 
        ConditionRegion,
        /// <summary>
        /// 全局数据
        /// </summary>
        [Description("base")] 
        GlobalData,
        /// <summary>
        /// 脚本节点
        /// </summary>
        [Description("base")] 
        Script,
    }

}
