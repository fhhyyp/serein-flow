<script setup lang="ts">
import { history, historyKeymap, defaultKeymap, indentWithTab } from '@codemirror/commands'
import { javascript } from '@codemirror/lang-javascript'
import { bracketMatching, defaultHighlightStyle, foldGutter, indentOnInput, syntaxHighlighting } from '@codemirror/language'
import { EditorState } from '@codemirror/state'
import { drawSelection, EditorView, highlightActiveLine, highlightActiveLineGutter, keymap, lineNumbers } from '@codemirror/view'
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'

const props = defineProps<{
  modelValue: string
  editorLabel: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()

const editorHost = ref<HTMLDivElement>()
let editorView: EditorView | undefined
let isApplyingExternalValue = false

const editorTheme = EditorView.theme({
  '&': {
    height: '100%',
    color: 'var(--ink)',
    backgroundColor: 'var(--surface)',
    fontSize: '13px',
  },
  '&.cm-focused': {
    outline: '2px solid var(--accent)',
    outlineOffset: '-2px',
  },
  '.cm-scroller': {
    overflow: 'auto',
    fontFamily: 'var(--mono)',
    lineHeight: '1.65',
  },
  '.cm-content': {
    minHeight: '100%',
    padding: '14px 0 34px',
  },
  '.cm-line': {
    padding: '0 18px',
  },
  '.cm-gutters': {
    minHeight: '100%',
    borderRight: '1px solid var(--line)',
    backgroundColor: 'var(--canvas)',
    color: 'var(--subtle)',
  },
  '.cm-activeLine': {
    backgroundColor: '#f8fbfe',
  },
  '.cm-activeLineGutter': {
    backgroundColor: 'var(--accent-soft)',
    color: 'var(--accent-strong)',
  },
  '.cm-selectionBackground, &.cm-focused .cm-selectionBackground': {
    backgroundColor: '#cce9f8',
  },
  '&.cm-focused .cm-cursor': {
    borderLeftColor: 'var(--ink)',
  },
})

function replaceDocument(value: string): void {
  if (!editorView || editorView.state.doc.toString() === value) {
    return
  }

  isApplyingExternalValue = true
  editorView.dispatch({
    changes: {
      from: 0,
      to: editorView.state.doc.length,
      insert: value,
    },
  })
  isApplyingExternalValue = false
}

onMounted(() => {
  if (!editorHost.value) {
    return
  }

  const state = EditorState.create({
    doc: props.modelValue,
    extensions: [
      lineNumbers(),
      highlightActiveLineGutter(),
      history(),
      foldGutter(),
      drawSelection(),
      indentOnInput(),
      bracketMatching(),
      syntaxHighlighting(defaultHighlightStyle, { fallback: true }),
      highlightActiveLine(),
      javascript(),
      EditorState.tabSize.of(2),
      EditorView.lineWrapping,
      EditorView.contentAttributes.of({
        'aria-label': props.editorLabel,
        'aria-multiline': 'true',
      }),
      keymap.of([...defaultKeymap, ...historyKeymap, indentWithTab]),
      editorTheme,
      EditorView.updateListener.of((update) => {
        if (update.docChanged && !isApplyingExternalValue) {
          emit('update:modelValue', update.state.doc.toString())
        }
      }),
    ],
  })

  editorView = new EditorView({ state, parent: editorHost.value })
  requestAnimationFrame(() => editorView?.focus())
})

watch(() => props.modelValue, replaceDocument)

onBeforeUnmount(() => {
  editorView?.destroy()
  editorView = undefined
})
</script>

<template>
  <div ref="editorHost" class="serein-script-code-editor"></div>
</template>
