import { computed, ref } from 'vue'
import { listProjectLibraries, type LibraryDto } from '../api/libraryApi'
import { t } from '../i18n'

export function useLibraryCatalog() {
  const librarySearch = ref('')
  const libraries = ref<LibraryDto[]>([])
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

  async function refreshLibraryCatalog(projectId?: string): Promise<void> {
    isLibraryCatalogLoading.value = true
    libraryCatalogError.value = ''
    if (!projectId) {
      libraries.value = []
      isLibraryCatalogLoading.value = false
      return
    }
    try {
      libraries.value = (await listProjectLibraries(projectId)).map((reference) => reference.library)
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
    isLibraryCatalogLoading,
    libraryCatalogError,
    visibleLibraries,
    catalogNodeCount,
    refreshLibraryCatalog,
    replaceProjectLibraries,
  }
}
