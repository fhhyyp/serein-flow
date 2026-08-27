import { computed, ref } from 'vue'
import { getBuiltinNodeCatalog, type NodeCreationDescriptorDto } from '../api/flowApi'
import { listProjectLibraries, type LibraryDto } from '../api/libraryApi'
import { t } from '../i18n'

export function useLibraryCatalog() {
  const librarySearch = ref('')
  const libraries = ref<LibraryDto[]>([])
  const builtinNodes = ref<NodeCreationDescriptorDto[]>([])
  const isLibraryCatalogLoading = ref(true)
  const libraryCatalogError = ref('')

  const visibleLibraries = computed(() => {
    const query = librarySearch.value.trim().toLocaleLowerCase()
    return libraries.value
      .map((library) => ({
        library,
        nodes: query
          ? library.nodes.filter((node) => [node.displayName, node.className, node.methodName, node.description ?? ''].some((value) => value.toLocaleLowerCase().includes(query)))
          : library.nodes,
      }))
      .filter((item) => item.nodes.length > 0)
  })
  const catalogNodeCount = computed(() => libraries.value.reduce((count, library) => count + library.nodes.length, 0))
  const visibleBuiltinNodes = computed(() => {
    const query = librarySearch.value.trim().toLocaleLowerCase()
    if (!query) {
      return builtinNodes.value
    }

    return builtinNodes.value.filter((node) =>
      [node.displayName, node.description ?? '', node.type, t(node.ui.titleKey), t(node.ui.subtitleKey)]
        .some((value) => value.toLocaleLowerCase().includes(query)))
  })

  async function refreshLibraryCatalog(projectId?: string): Promise<void> {
    isLibraryCatalogLoading.value = true
    libraryCatalogError.value = ''
    try {
      const [builtins, references] = await Promise.all([
        getBuiltinNodeCatalog(),
        projectId ? listProjectLibraries(projectId) : Promise.resolve([]),
      ])
      builtinNodes.value = builtins.nodes
      libraries.value = references.map((reference) => reference.library)
    } catch {
      libraryCatalogError.value = t('library.loadFailed')
    } finally {
      isLibraryCatalogLoading.value = false
    }
  }

  function replaceProjectLibraries(nextLibraries: LibraryDto[]): void {
    libraries.value = nextLibraries
    librarySearch.value = ''
  }

  return {
    librarySearch,
    libraries,
    builtinNodes,
    isLibraryCatalogLoading,
    libraryCatalogError,
    visibleLibraries,
    visibleBuiltinNodes,
    catalogNodeCount,
    refreshLibraryCatalog,
    replaceProjectLibraries,
  }
}
