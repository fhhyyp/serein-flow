import { computed, ref, type Ref } from 'vue'
import { listLibraries, type LibraryDto } from '../api/libraryApi'
import { t } from '../i18n'

interface UseLibraryCatalogOptions {
  notice: Ref<string>
}

export function useLibraryCatalog(options: UseLibraryCatalogOptions) {
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

  async function refreshLibraryCatalog(): Promise<void> {
    isLibraryCatalogLoading.value = true
    libraryCatalogError.value = ''
    try {
      libraries.value = await listLibraries()
    } catch {
      libraryCatalogError.value = t('library.loadFailed')
    } finally {
      isLibraryCatalogLoading.value = false
    }
  }

  function handleLibraryUploaded(library: LibraryDto): void {
    const existingIndex = libraries.value.findIndex((item) => item.id === library.id)
    if (existingIndex >= 0) {
      libraries.value = libraries.value.map((item, index) => index === existingIndex ? library : item)
    } else {
      libraries.value = [...libraries.value, library]
    }
    librarySearch.value = ''
    options.notice.value = t('libraryUpload.success', { name: library.name, count: library.nodes.length })
  }

  return {
    librarySearch,
    libraries,
    isLibraryCatalogLoading,
    libraryCatalogError,
    visibleLibraries,
    catalogNodeCount,
    refreshLibraryCatalog,
    handleLibraryUploaded,
  }
}
