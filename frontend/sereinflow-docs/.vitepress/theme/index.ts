import DefaultTheme from 'vitepress/theme'
import { h } from 'vue'
import FlowHome from './components/FlowHome.vue'
import './custom.css'
import './portal.css'

export default {
  extends: DefaultTheme,
  Layout: () => h(DefaultTheme.Layout, null, {
    'home-hero-before': () => h(FlowHome),
  }),
}
