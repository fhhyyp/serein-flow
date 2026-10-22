import { createApp } from 'vue';
import './style.css';
import SecurityGate from './SecurityGate.vue';
const createAppResult = createApp(SecurityGate);
createAppResult.mount('#app');
