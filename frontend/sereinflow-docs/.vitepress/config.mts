import { defineConfig } from 'vitepress'

export default defineConfig({
  srcDir: 'content',
  title: 'SereinFlow',
  description: 'SereinFlow 工作流编排与执行文档',
  lang: 'zh-CN',
  head: [['link', { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' }]],
  cleanUrls: true,
  sitemap: { hostname: 'http://sereinflow.ningxi.cc' },
  themeConfig: {
    siteTitle: 'SereinFlow',
    nav: [
      { text: '开始使用', link: '/guide/getting-started' },
      { text: '认识各模块', link: '/modules/' },
      { text: '编写节点类库', link: '/library/' },
      { text: '服务与接入', link: '/integration/' },
      { text: 'REST API', link: '/api/' },
    ],
    sidebar: {
      '/guide/': [
        { text: '从零开始', items: [
          { text: '快速开始', link: '/guide/getting-started' },
          { text: '创建第一个流程', link: '/guide/first-flow' },
          { text: '用 API 运行流程', link: '/guide/run-with-api' },
        ] },
      ],
      '/modules/': [
        { text: '认识各模块', items: [
          { text: '先看全貌', link: '/modules/' },
          { text: '项目与流程', link: '/modules/projects-and-flows' },
          { text: '节点与类库', link: '/modules/nodes-and-libraries' },
          { text: '版本与发布', link: '/modules/versions' },
          { text: '运行与结果', link: '/modules/runs' },
          { text: '调试流程', link: '/modules/debugging' },
          { text: '公开流程接口', link: '/modules/public-interfaces' },
          { text: '环境设置与密钥', link: '/modules/environment' },
        ] },
      ],
      '/library/': [
        { text: '编写节点类库', items: [
          { text: '先看全貌', link: '/library/' },
          { text: '写出第一个节点', link: '/library/first-node' },
          { text: '节点参数与执行方式', link: '/library/node-contracts' },
          { text: '注册自己的服务', link: '/library/services' },
          { text: '使用内置服务', link: '/library/built-in-services' },
          { text: '加载 native DLL', link: '/library/native-dll' },
          { text: '打包、上传与排错', link: '/library/package-upload' },
        ] },
      ],
      '/integration/': [
        { text: '服务与接入', items: [
          { text: '三种使用场景', link: '/integration/' },
          { text: '嵌入 ASP.NET Core', link: '/integration/hosting' },
          { text: '内部服务怎样注册', link: '/integration/service-registration' },
          { text: '在 .NET 程序中调用', link: '/integration/client-di' },
        ] },
      ],
      '/api/': [
        { text: '调用前先了解', items: [
          { text: 'API 概览', link: '/api/' },
          { text: '地址、密钥与报错', link: '/api/basics' },
        ] },
        { text: '按模块查接口', items: [
          { text: '项目与流程', link: '/api/projects' },
          { text: '流程版本', link: '/api/versions' },
          { text: '运行、事件与工件', link: '/api/runs' },
          { text: '节点类库', link: '/api/libraries' },
          { text: '公开流程接口', link: '/api/public-interfaces' },
          { text: '环境设置与密钥', link: '/api/environment' },
          { text: '调试会话', link: '/api/debug' },
        ] },
      ],
    },
    search: {
      provider: 'local',
      options: {
        locales: {
          root: {
            translations: {
              button: { buttonText: '搜索文档', buttonAriaLabel: '搜索文档' },
              modal: {
                displayDetails: '显示详细列表',
                resetButtonTitle: '重置搜索',
                backButtonTitle: '关闭搜索',
                noResultsText: '没有结果',
                footer: {
                  selectText: '选择',
                  selectKeyAriaLabel: '回车',
                  navigateText: '导航',
                  navigateUpKeyAriaLabel: '上箭头',
                  navigateDownKeyAriaLabel: '下箭头',
                  closeText: '关闭',
                  closeKeyAriaLabel: 'Esc',
                },
              },
            },
          },
        },
      },
    },
    footer: { message: 'SereinFlow 使用与开发文档' },
    outline: { label: '本页目录', level: [2, 3] },
    docFooter: { prev: '上一页', next: '下一页' },
    returnToTopLabel: '回到顶部',
    sidebarMenuLabel: '目录',
    darkModeSwitchLabel: '外观',
  },
})
