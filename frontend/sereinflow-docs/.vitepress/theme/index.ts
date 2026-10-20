import DefaultTheme from 'vitepress/theme';
import { h } from 'vue';
import FlowHome from './components/FlowHome.vue';
import LanguageSwitch from './components/LanguageSwitch.vue';
import './custom.css';
import './portal.css';
export default {
  extends: DefaultTheme,
  Layout: () => {
    const hResult3 = h(DefaultTheme.Layout, null, {
      'home-hero-before': () => {
        const hResult = h(FlowHome);
        return hResult;
      },
      'nav-bar-content-after': () => {
        const hResult1 = h(LanguageSwitch);
        return hResult1;
      },
      'nav-screen-content-after': () => {
        const hResult2 = h(LanguageSwitch);
        return hResult2;
      },
    });
    return hResult3;
  },
};
