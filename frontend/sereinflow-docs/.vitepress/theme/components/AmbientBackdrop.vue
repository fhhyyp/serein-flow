<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'

const props = defineProps<{ paused: boolean }>()
const canvas = ref<HTMLCanvasElement>()
let sync = () => {}
let dispose = () => {}
watch(() => props.paused, () => sync())

onMounted(() => {
  const element = canvas.value!
  const parent = element.parentElement!
  const stage = parent.querySelector<HTMLElement>('.flow-atmosphere')!
  const context = element.getContext('2d')
  if (!context) return
  const ctx = context
  type Wave = { u: number; v: number; born: number; strength: number }
  type Drop = { u: number; v: number; born: number; duration: number; height: number }
  let width = 0, height = 0, cx = 0, cy = 0, rx = 1, ry = 1
  let frame = 0, lastTime = 0, time = 0, nextDrop = 0, serial = 0
  let visible = true, inside = false, lastWake = 0
  const pointer = { x: 0, y: 0, targetX: 0, targetY: 0, wakeX: 0, wakeY: 0 }
  const waves: Wave[] = [
    { u: .32, v: .24, born: -2.7, strength: 7 },
    { u: -.36, v: -.15, born: -1.1, strength: 8 },
    { u: .04, v: -.5, born: -.1, strength: 5 },
  ]
  const drops: Drop[] = [
    { u: -.16, v: .2, born: -.35, duration: 1.7, height: 205 },
    { u: .5, v: -.35, born: -.85, duration: 1.55, height: 165 },
    { u: -.6, v: -.2, born: -.2, duration: 1.9, height: 185 },
  ]
  // A sunflower distribution keeps the surface organic, without a visible grid.
  const particles = Array.from({ length: 2100 }, (_, index) => {
    const radius = Math.sqrt((index + .5) / 2100)
    const angle = index * 2.3999632297
    return { u: Math.cos(angle) * radius + Math.sin(index * 127.1) * .006, v: Math.sin(angle) * radius + Math.cos(index * 311.7) * .006, radius, size: .7 + index % 7 * .1, phase: index * .71 }
  })
  function addWave(u: number, v: number, strength: number) {
    waves.push({ u, v, born: time, strength })
    if (waves.length > 12) waves.shift()
  }
  function surface(u: number, v: number) {
    return Math.sin(u * 5.2 + time * .36) * 4 + Math.cos(v * 7.3 - time * .27) * 3
  }
  function draw() {
    if (!width || !height) return
    ctx.clearRect(0, 0, width, height)
    const wash = ctx.createRadialGradient(cx, cy - ry, 0, cx, cy, rx * 1.6)
    wash.addColorStop(0, 'rgba(137, 232, 205, .22)')
    wash.addColorStop(.48, 'rgba(168, 228, 224, .12)')
    wash.addColorStop(1, 'rgba(225, 244, 233, 0)')
    ctx.fillStyle = wash
    ctx.fillRect(0, 0, width, height)
    for (let i = 0; i < 42; i++) {
      const x = ((i * 137.3 + 53) % 997) / 997 * width + Math.sin(time * .14 + i) * 8
      const y = (((i * 293.7 + 71) % 991) / 991 * height + time * (2 + i % 3)) % height
      ctx.fillStyle = `rgba(81, 150, 129, ${.07 + i % 4 * .025})`
      ctx.beginPath()
      ctx.arc(x, y, i % 6 === 0 ? 1.2 : .6, 0, Math.PI * 2)
      ctx.fill()
    }
    for (const particle of particles) {
      const u = particle.u + Math.sin(time * .19 + particle.v * 4) * .018
      const v = particle.v + Math.cos(time * .16 + particle.u * 5) * .012
      let lift = surface(u, v)
      let energy = 0
      for (const wave of waves) {
        const age = time - wave.born
        const distance = Math.hypot(u - wave.u, v - wave.v) * rx
        const front = distance - age * 56
        const envelope = Math.exp(-(front * front) / 1300) * Math.max(0, 1 - age / 6)
        lift += Math.cos(front * .07) * envelope * wave.strength
        energy += envelope * .18
      }
      const edge = Math.pow(Math.max(0, 1 - particle.radius ** 4), 1.4)
      const shimmer = .76 + Math.sin(time * .65 + particle.phase) * .24
      ctx.fillStyle = `rgba(37, 130, 108, ${edge * (.43 + energy) * shimmer})`
      ctx.beginPath()
      ctx.ellipse(cx + u * rx, cy + v * ry - lift, particle.size * (1 + energy), particle.size * .7, 0, 0, Math.PI * 2)
      ctx.fill()
    }
    for (const wave of waves) {
      const age = time - wave.born
      const fade = Math.max(0, 1 - age / 6)
      const birth = Math.min(1, age * 5)
      const x = cx + wave.u * rx
      const y = cy + wave.v * ry - surface(wave.u, wave.v)
      for (let ring = 0; ring < 3; ring++) {
        const radius = Math.max(1, age * 56 - ring * 16)
        if (radius <= 1) continue
        ctx.beginPath()
        ctx.ellipse(x, y, radius, radius * ry / rx, -.025, 0, Math.PI * 2)
        ctx.strokeStyle = `rgba(51, 145, 123, ${fade * fade * birth * (.43 - ring * .1) * Math.min(1, wave.strength / 8)})`
        ctx.lineWidth = ring === 0 ? .8 : .55
        ctx.stroke()
      }
      if (age < .45) {
        ctx.beginPath()
        ctx.arc(x, y - Math.sin(age / .45 * Math.PI) * 8, 1.4 * (1 - age / .45), 0, Math.PI * 2)
        ctx.fillStyle = `rgba(58, 153, 131, ${(1 - age / .45) * .5})`
        ctx.fill()
      }
    }
    for (const drop of drops) {
      const progress = Math.max(0, Math.min(1, (time - drop.born) / drop.duration))
      const x = cx + drop.u * rx + (1 - progress) * 5
      const y = cy + drop.v * ry - surface(drop.u, drop.v) - (1 - progress ** 1.55) * drop.height
      const length = 16 + progress * 15
      const alpha = Math.min(1, progress * 5) * .65
      const trail = ctx.createLinearGradient(x, y - length, x, y)
      trail.addColorStop(0, 'rgba(65, 146, 130, 0)')
      trail.addColorStop(1, `rgba(65, 146, 130, ${alpha})`)
      ctx.strokeStyle = trail
      ctx.lineWidth = .8
      ctx.beginPath()
      ctx.moveTo(x + .5, y - length)
      ctx.lineTo(x, y)
      ctx.stroke()
      ctx.fillStyle = `rgba(54, 142, 120, ${alpha})`
      ctx.beginPath()
      ctx.ellipse(x, y, 1.05, 1.8, .03, 0, Math.PI * 2)
      ctx.fill()
    }
  }
  function animate(now: number) {
    const dt = lastTime ? Math.min((now - lastTime) / 1000, .05) : 0
    lastTime = now
    time += dt
    const follow = 1 - Math.exp(-dt * 9)
    pointer.x += (pointer.targetX - pointer.x) * follow
    pointer.y += (pointer.targetY - pointer.y) * follow
    if (time >= nextDrop) {
      serial++
      const angle = serial * 2.3999632297
      const radius = .18 + (serial * .173 % .6)
      drops.push({ u: Math.cos(angle) * radius, v: Math.sin(angle) * radius, born: time, duration: 2.1 + serial % 4 * .18, height: Math.min(310, height * .37) + serial % 3 * 16 })
      nextDrop = time + .36
    }
    for (let i = drops.length - 1; i >= 0; i--) {
      const drop = drops[i]
      if (time - drop.born >= drop.duration) {
        addWave(drop.u, drop.v, 5 + i % 3)
        drops.splice(i, 1)
      }
    }
    while (waves.length && time - waves[0].born > 6) waves.shift()
    if (inside && time - lastWake > .24 && Math.hypot(pointer.x - pointer.wakeX, pointer.y - pointer.wakeY) > 20) {
      const u = (pointer.x - cx) / rx
      const v = (pointer.y - cy) / ry
      if (u * u + v * v < 1.25) addWave(u, v, 4)
      pointer.wakeX = pointer.x
      pointer.wakeY = pointer.y
      lastWake = time
    }
    draw()
    frame = requestAnimationFrame(animate)
  }
  sync = () => {
    cancelAnimationFrame(frame)
    lastTime = 0
    if (!props.paused && !document.hidden && visible) frame = requestAnimationFrame(animate)
    else draw()
  }
  function resize() {
    const rect = element.getBoundingClientRect()
    const area = stage.getBoundingClientRect()
    width = rect.width
    height = rect.height
    cx = area.left - rect.left + area.width * .52
    cy = area.top - rect.top + area.height * .61
    rx = Math.max(1, area.width * .56)
    ry = Math.max(1, Math.min(area.height * .22, rx * .37))
    const ratio = Math.min(window.devicePixelRatio || 1, 1.75)
    element.width = Math.round(width * ratio)
    element.height = Math.round(height * ratio)
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0)
    draw()
  }
  function move(event: PointerEvent) {
    if (props.paused) return
    const rect = element.getBoundingClientRect()
    pointer.targetX = event.clientX - rect.left
    pointer.targetY = event.clientY - rect.top
    if (!inside) {
      pointer.x = pointer.targetX
      pointer.y = pointer.targetY
      pointer.wakeX = pointer.x
      pointer.wakeY = pointer.y
    }
    inside = true
  }
  function leave() { inside = false }
  function click(event: PointerEvent) {
    if (props.paused || event.button !== 0 || (event.target as Element).closest('a, button, input')) return
    const rect = element.getBoundingClientRect()
    addWave((event.clientX - rect.left - cx) / rx, (event.clientY - rect.top - cy) / ry, 14)
  }
  const resizeObserver = new ResizeObserver(resize)
  resizeObserver.observe(parent)
  resizeObserver.observe(stage)
  const visibilityObserver = new IntersectionObserver(([entry]) => {
    visible = entry.isIntersecting
    sync()
  })
  visibilityObserver.observe(parent)
  parent.addEventListener('pointermove', move, { passive: true })
  parent.addEventListener('pointerdown', click, { passive: true })
  parent.addEventListener('pointerleave', leave)
  document.addEventListener('visibilitychange', sync)
  resize()
  sync()
  dispose = () => {
    cancelAnimationFrame(frame)
    resizeObserver.disconnect()
    visibilityObserver.disconnect()
    parent.removeEventListener('pointermove', move)
    parent.removeEventListener('pointerdown', click)
    parent.removeEventListener('pointerleave', leave)
    document.removeEventListener('visibilitychange', sync)
    sync = () => {}
  }
})
onUnmounted(() => dispose())
</script>

<template>
  <canvas ref="canvas" class="flow-backdrop" aria-hidden="true" />
</template>
