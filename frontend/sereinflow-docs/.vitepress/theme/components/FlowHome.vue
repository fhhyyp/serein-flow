<script setup lang="ts">
import { computed, ref } from 'vue'
import { useData, withBase } from 'vitepress'
import AmbientBackdrop from './AmbientBackdrop.vue'

const paused = ref(false)
const { page } = useData()
const english = computed(() => page.value.relativePath.startsWith('en/'))
const startLink = computed(() => withBase(english.value ? '/en/guide/getting-started' : '/guide/getting-started'))
</script>

<template>
  <main class="flow-home" :class="{ 'is-paused': paused }" aria-labelledby="flow-title">
    <AmbientBackdrop :paused="paused" />
    <div class="flow-paper" aria-hidden="true" />

    <div class="flow-shell">
      <section class="flow-hero" aria-labelledby="flow-title">
        <div class="flow-copy">
          <h1 v-if="english" id="flow-title">Let ideas<br /><span>flow freely.</span></h1>
          <h1 v-else id="flow-title">让逻辑，<br /><span>自由流动。</span></h1>
          <p class="flow-description">{{ english ? 'Visual orchestration that turns ideas into workflows.' : '可视化编排，让想法成为工作流。' }}</p>
          <div class="flow-actions">
            <a class="flow-primary" :href="startLink">
              {{ english ? 'Get started' : '开始使用' }}
              <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14m-6-6 6 6-6 6" /></svg>
            </a>
            <a class="flow-github" href="https://github.com/fhhyyp/serein-flow" target="_blank" rel="noopener noreferrer">GitHub ↗</a>
          </div>
        </div>

        <div class="flow-atmosphere" aria-hidden="true" />
      </section>

      <footer class="flow-footer">
        <span class="flow-mouse-hint">{{ english ? 'Move your mouse · Click to interact' : '移动鼠标 · 点击互动' }}</span>
        <button class="flow-motion-toggle" :aria-pressed="paused" @click="paused = !paused">
          <svg viewBox="0 0 16 16" aria-hidden="true"><path v-if="paused" d="m5 3 8 5-8 5V3Z" /><path v-else d="M5 3v10M11 3v10" /></svg>
          {{ english ? (paused ? 'Play animation' : 'Pause animation') : (paused ? '播放动效' : '暂停动效') }}
        </button>
      </footer>
    </div>
  </main>
</template>
