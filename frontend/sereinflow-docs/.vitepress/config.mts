import { defineConfig } from 'vitepress';
import sereinlang from './sereinlang.tmLanguage.json';
const repository = 'https://github.com/fhhyyp/serein-flow';
const sections = {
  guide: {
    zh: ['从零开始', ['快速开始', 'getting-started'], ['创建第一个流程', 'first-flow'], ['用 API 运行流程', 'run-with-api']],
    en: ['Getting started', ['Quick start', 'getting-started'], ['Create your first flow', 'first-flow'], ['Run a flow with the API', 'run-with-api']],
  },
  modules: {
    zh: ['认识各模块', ['先看全貌', ''], ['项目与流程', 'projects-and-flows'], ['节点与类库', 'nodes-and-libraries'], ['版本与发布', 'versions'], ['运行与结果', 'runs'], ['调试流程', 'debugging'], ['公开流程接口', 'public-interfaces'], ['环境设置与密钥', 'environment']],
    en: ['Concepts', ['Overview', ''], ['Projects and flows', 'projects-and-flows'], ['Nodes and libraries', 'nodes-and-libraries'], ['Versions and publishing', 'versions'], ['Runs and results', 'runs'], ['Debugging', 'debugging'], ['Public interfaces', 'public-interfaces'], ['Environment and keys', 'environment']],
  },
  script: {
    zh: ['SereinScript', ['使用与开发概览', ''], ['在流程中使用脚本', 'usage'], ['扩展 ScriptAdapter', 'script-adapter'], ['ScriptModules 使用与扩展', 'script-modules']],
    en: ['SereinScript', ['Usage and development', ''], ['Use scripts in flows', 'usage'], ['Extend ScriptAdapter', 'script-adapter'], ['ScriptModules usage and extensions', 'script-modules']],
  },
  library: {
    zh: ['编写节点类库', ['先看全貌', ''], ['写出第一个节点', 'first-node'], ['节点参数与执行方式', 'node-contracts'], ['注册自己的服务', 'services'], ['使用内置服务', 'built-in-services'], ['加载 native DLL', 'native-dll'], ['打包、上传与排错', 'package-upload']],
    en: ['Node libraries', ['Overview', ''], ['Your first node', 'first-node'], ['Node contracts', 'node-contracts'], ['Register services', 'services'], ['Built-in services', 'built-in-services'], ['Load native DLLs', 'native-dll'], ['Package and upload', 'package-upload']],
  },
  integration: {
    zh: ['服务与接入', ['三种使用场景', ''], ['嵌入 ASP.NET Core', 'hosting'], ['内部服务怎样注册', 'service-registration'], ['在 .NET 程序中调用', 'client-di']],
    en: ['Integration', ['Three use cases', ''], ['Host in ASP.NET Core', 'hosting'], ['Internal service registration', 'service-registration'], ['Call from .NET', 'client-di']],
  },
  api: {
    zh: ['REST API', ['API 概览', ''], ['地址、密钥与报错', 'basics'], ['项目与流程', 'projects'], ['流程版本', 'versions'], ['运行、事件与工件', 'runs'], ['节点类库', 'libraries'], ['公开流程接口', 'public-interfaces'], ['环境设置与密钥', 'environment'], ['调试会话', 'debug']],
    en: ['REST API', ['API overview', ''], ['URLs, keys, and errors', 'basics'], ['Projects and flows', 'projects'], ['Flow versions', 'versions'], ['Runs, events, and artifacts', 'runs'], ['Node libraries', 'libraries'], ['Public interfaces', 'public-interfaces'], ['Environment and keys', 'environment'], ['Debug sessions', 'debug']],
  },
} as const;
type SidebarPageDefinition = readonly [text: string, slug: string];
type SidebarSectionDefinition = {
  text: string;
  items: Array<{ text: string; link: string }>;
};

function sidebar(locale: 'zh' | 'en') {
  const prefix = locale === 'en' ? '/en' : '';
  const sectionDefinitions = Object.entries(sections);
  const sectionMappings = sectionDefinitions.map((sectionDefinition) => {
    const section = sectionDefinition[0];
    const localizedLabels = sectionDefinition[1][locale];
    const heading = localizedLabels[0];
    const pageDefinitions = localizedLabels.slice(1) as readonly SidebarPageDefinition[];
    const pageItems = pageDefinitions.map((pageDefinition) => {
      const pageText = pageDefinition[0];
      const slug = pageDefinition[1];
      return { text: pageText, link: `${prefix}/${section}/${slug}` };
    });
    const sectionItems: SidebarSectionDefinition[] = [{ text: heading, items: pageItems }];
    const sectionMapping: [string, SidebarSectionDefinition[]] = [`${prefix}/${section}/`, sectionItems];
    return sectionMapping;
  });
  const configuredSidebar = Object.fromEntries(sectionMappings);
  return configuredSidebar;
}

function nav(locale: 'zh' | 'en') {
  const prefix = locale === 'en' ? '/en' : '';
  const items: Array<readonly [string, string]> = locale === 'en'
    ? [['Get started', '/guide/getting-started'], ['Concepts', '/modules/'], ['SereinScript', '/script/'], ['Node libraries', '/library/'], ['Integration', '/integration/'], ['REST API', '/api/']]
    : [['开始使用', '/guide/getting-started'], ['认识各模块', '/modules/'], ['SereinScript', '/script/'], ['编写节点类库', '/library/'], ['服务与接入', '/integration/'], ['REST API', '/api/']];
  const navigationItems = items.map((item) => {
    const text = item[0];
    const route = item[1];
    return { text, link: prefix + route };
  });
  return navigationItems;
}

const chineseNavigation = nav('zh');
const chineseSidebar = sidebar('zh');
const englishNavigation = nav('en');
const englishSidebar = sidebar('en');

const documentationOptions: Parameters<typeof defineConfig>[0] = {
  srcDir: 'content',
  title: 'SereinFlow',
  description: 'SereinFlow 工作流编排与执行文档',
  cleanUrls: true,
  markdown: {
    languages: [sereinlang],
  },
  sitemap: { hostname: 'http://sereinflow.ningxi.cc' },
  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/favicon.svg' }],
    ['script', {}, `(() => {
      try {
        const path = window.location.pathname
        if (path.startsWith('/en/')) {
          return
        }
        const saved = window.localStorage.getItem('sereinflow-docs-language')
        const language = navigator.languages?.[0] || navigator.language || 'zh'
        const preferred = saved || (/^zh(?:-|$)/i.test(language) ? 'zh' : 'en')
        if (preferred === 'en') {
          const localizedPath = path === '/' ? '/' : path
          const targetUrl = '/en' + localizedPath + window.location.search + window.location.hash
          window.location.replace(targetUrl)
        }
      } catch { /* Keep the Chinese page if detection is unavailable. */ }
    })()`],
  ],
  locales: {
    root: {
      label: '简体中文',
      lang: 'zh-CN',
      description: 'SereinFlow 工作流编排与执行文档',
      themeConfig: {
        nav: chineseNavigation,
        sidebar: chineseSidebar,
        footer: { message: 'SereinFlow 使用与开发文档' },
        outline: { label: '本页目录', level: [2, 3] },
        docFooter: { prev: '上一页', next: '下一页' },
        returnToTopLabel: '回到顶部',
        sidebarMenuLabel: '目录',
        darkModeSwitchLabel: '外观',
      },
    },
    en: {
      label: 'English',
      lang: 'en-US',
      description: 'SereinFlow workflow orchestration and execution documentation',
      themeConfig: {
        nav: englishNavigation,
        sidebar: englishSidebar,
        footer: { message: 'SereinFlow user and developer documentation' },
        outline: { label: 'On this page', level: [2, 3] },
        docFooter: { prev: 'Previous', next: 'Next' },
        returnToTopLabel: 'Back to top',
        sidebarMenuLabel: 'Menu',
        darkModeSwitchLabel: 'Appearance',
      },
    },
  },
  themeConfig: {
    siteTitle: 'SereinFlow',
    socialLinks: [{ icon: 'github', link: repository, ariaLabel: 'GitHub repository' }],
    search: {
      provider: 'local',
      options: {
        locales: {
          root: { translations: {
              button: { buttonText: '搜索文档', buttonAriaLabel: '搜索文档' },
              modal: {
                displayDetails: '显示详细列表', resetButtonTitle: '重置搜索',
                backButtonTitle: '关闭搜索', noResultsText: '没有结果',
                footer: {
                  selectText: '选择', selectKeyAriaLabel: '回车', navigateText: '导航',
                  navigateUpKeyAriaLabel: '上箭头', navigateDownKeyAriaLabel: '下箭头',
                  closeText: '关闭', closeKeyAriaLabel: 'Esc',
                },
              },
            } },
          en: { translations: {
              button: { buttonText: 'Search docs', buttonAriaLabel: 'Search docs' },
              modal: {
                displayDetails: 'Display detailed list', resetButtonTitle: 'Reset search',
                backButtonTitle: 'Close search', noResultsText: 'No results',
                footer: {
                  selectText: 'Select', selectKeyAriaLabel: 'Enter', navigateText: 'Navigate',
                  navigateUpKeyAriaLabel: 'Arrow up', navigateDownKeyAriaLabel: 'Arrow down',
                  closeText: 'Close', closeKeyAriaLabel: 'Escape',
                },
              },
            } },
        },
      },
    },
  },
};
const documentationConfig = defineConfig(documentationOptions);
export default documentationConfig;
