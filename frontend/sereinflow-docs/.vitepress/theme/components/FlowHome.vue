<script setup lang="ts">
import { ref } from 'vue'
import { withBase } from 'vitepress'
import AmbientBackdrop from './AmbientBackdrop.vue'

const paused = ref(false)
</script>

<template>
  <main class="flow-home" :class="{ 'is-paused': paused }" aria-labelledby="flow-title">
    <AmbientBackdrop :paused="paused" />
    <div class="flow-paper" aria-hidden="true" />

    <div class="flow-shell">
      <section class="flow-hero" aria-labelledby="flow-title">
        <div class="flow-copy">
          <h1 id="flow-title">让逻辑，<br /><span>自由流动。</span></h1>
          <p class="flow-description">可视化编排，让想法成为工作流。</p>
          <div class="flow-actions">
            <a class="flow-primary" :href="withBase('/guide/getting-started')">
              开始使用
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14m-6-6 6 6-6 6" /></svg>
            </a>
          </div>
        </div>

        <div class="flow-atmosphere" aria-hidden="true" />
      </section>

      <footer class="flow-footer">
        <span class="flow-mouse-hint">移动鼠标 · 点击互动</span>
        <button class="flow-motion-toggle" :aria-pressed="paused" @click="paused = !paused">
          <svg viewBox="0 0 16 16" aria-hidden="true"><path v-if="paused" d="m5 3 8 5-8 5V3Z" /><path v-else d="M5 3v10M11 3v10" /></svg>
          {{ paused ? '播放动效' : '暂停动效' }}
        </button>
      </footer>
    </div>
  </main>
</template>
