---
layout: home

hero:
  name: SereinFlow
  text: 从第一个流程到自己的节点
  tagline: 跟着例子使用 SereinFlow、编写类库和接入服务。文章只讲当前代码提供的接口。
  actions:
    - theme: brand
      text: 开始使用
      link: /guide/getting-started
    - theme: alt
      text: 编写节点类库
      link: /library/

features:
  - title: 第一次使用
    details: 启动服务，在网页中创建一个项目，让第一个节点真正运行。
    link: /guide/first-flow
    linkText: 跟着做
  - title: 看懂各模块
    details: 项目、流程、节点、版本和运行分别负责什么，以及它们怎样连在一起。
    link: /modules/
    linkText: 从全貌开始
  - title: 扩展自己的节点
    details: 用 C# 写节点、注册服务、加载 native DLL，再打包放到项目里使用。
    link: /library/
    linkText: 从第一个节点开始
  - title: 放进自己的程序
    details: 了解宿主如何注册内部服务，以及 .NET 客户端怎样调用 SereinFlow。
    link: /integration/
    linkText: 选择接入方式
---

## 这套文档怎么读

刚开始用 SereinFlow，请依次看[快速开始](/guide/getting-started)、[创建第一个流程](/guide/first-flow)。想加入自己的功能，从[写出第一个节点](/library/first-node)开始，再读[服务注入](/library/services)和[native DLL 加载](/library/native-dll)。

要把 SereinFlow 放进 ASP.NET Core 程序，请读[服务与接入](/integration/)；只需要调用已有服务，请读[.NET 客户端](/integration/client-di)或 [REST API](/api/)。
