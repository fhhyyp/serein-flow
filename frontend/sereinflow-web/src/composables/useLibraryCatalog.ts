import { computed, ref } from 'vue';
import { getBuiltinNodeCatalog, type NodeCreationDescriptorDto } from '../api/flowApi';
import { listProjectLibraries, type LibraryDto } from '../api/libraryApi';
import { t } from '../i18n';
export function useLibraryCatalog() {
  const librarySearch = ref('');
  const libraries = ref<LibraryDto[]>([]);
  const builtinNodes = ref<NodeCreationDescriptorDto[]>([]);
  const isLibraryCatalogLoading = ref(true);
  const libraryCatalogError = ref('');
  const visibleLibraries = computed(() => {
    const trimResult = librarySearch.value.trim();
    const query = trimResult.toLocaleLowerCase();
    const mapResult = libraries.value
      .map((library) => {
      const evaluatedValue: {
        library: {
          id: string;
          name: string;
          version: string;
          fileName: string;
          sizeBytes: number;
          sha256: string;
          uploadedAt: string;
          nodes: {
            id: string;
            type: import("../api/flowApi").ApiNodeType;
            displayName: string;
            description?: string | null | undefined;
            libraryId: string;
            flowLibraryName?: string | null | undefined;
            className: string;
            methodName: string;
            dllName: string;
            dllVersion: string;
            returnType: string;
            isAwaitable?: boolean | undefined;
            contractId?: string | null | undefined;
            overloadSignature?: string | null | undefined;
            identityConfidence?: "explicit" | "legacy" | undefined;
            parameters: {
              id: string;
              name: string;
              type: string;
              description?: string | null | undefined;
              required: boolean;
              isVariadic?: boolean | undefined;
              variadicGroupId?: string | null | undefined;
              elementType?: string | null | undefined;
              enumMetadata?: {
                typeName: string;
                isFlags: boolean;
                underlyingType: string;
                options: {
                  name: string;
                  numericValue: string;
                }[];
              } | null | undefined;
              defaultValue?: string | null | undefined;
              aliases?: string[] | null | undefined;
              identityConfidence?: "explicit" | "legacy" | undefined;
            }[];
          }[];
          lifecycle: "available" | "archived";
          familyId?: string | null | undefined;
          familyName?: string | null | undefined;
          semanticVersion?: string | null | undefined;
        };
        nodes: {
          id: string;
          type: import("../api/flowApi").ApiNodeType;
          displayName: string;
          description?: string | null | undefined;
          libraryId: string;
          flowLibraryName?: string | null | undefined;
          className: string;
          methodName: string;
          dllName: string;
          dllVersion: string;
          returnType: string;
          isAwaitable?: boolean | undefined;
          contractId?: string | null | undefined;
          overloadSignature?: string | null | undefined;
          identityConfidence?: "explicit" | "legacy" | undefined;
          parameters: {
            id: string;
            name: string;
            type: string;
            description?: string | null | undefined;
            required: boolean;
            isVariadic?: boolean | undefined;
            variadicGroupId?: string | null | undefined;
            elementType?: string | null | undefined;
            enumMetadata?: {
              typeName: string;
              isFlags: boolean;
              underlyingType: string;
              options: {
                name: string;
                numericValue: string;
              }[];
            } | null | undefined;
            defaultValue?: string | null | undefined;
            aliases?: string[] | null | undefined;
            identityConfidence?: "explicit" | "legacy" | undefined;
          }[];
        }[];
      } = ({
        library,
        nodes: query
          ? library.nodes.filter((node) => {
            const someResult: boolean = [node.displayName, node.className, node.methodName, node.description ?? ''].some((value) => {
              const toLocaleLowerCaseResult = value.toLocaleLowerCase();
              const includesResult: boolean = toLocaleLowerCaseResult.includes(query);
              return includesResult;
            });
            return someResult;
          })
          : library.nodes,
      });
      return evaluatedValue;
    });
    const filterResult: {
      library: {
        id: string;
        name: string;
        version: string;
        fileName: string;
        sizeBytes: number;
        sha256: string;
        uploadedAt: string;
        nodes: {
          id: string;
          type: import("../api/flowApi").ApiNodeType;
          displayName: string;
          description?: string | null | undefined;
          libraryId: string;
          flowLibraryName?: string | null | undefined;
          className: string;
          methodName: string;
          dllName: string;
          dllVersion: string;
          returnType: string;
          isAwaitable?: boolean | undefined;
          contractId?: string | null | undefined;
          overloadSignature?: string | null | undefined;
          identityConfidence?: "explicit" | "legacy" | undefined;
          parameters: {
            id: string;
            name: string;
            type: string;
            description?: string | null | undefined;
            required: boolean;
            isVariadic?: boolean | undefined;
            variadicGroupId?: string | null | undefined;
            elementType?: string | null | undefined;
            enumMetadata?: {
              typeName: string;
              isFlags: boolean;
              underlyingType: string;
              options: {
                name: string;
                numericValue: string;
              }[];
            } | null | undefined;
            defaultValue?: string | null | undefined;
            aliases?: string[] | null | undefined;
            identityConfidence?: "explicit" | "legacy" | undefined;
          }[];
        }[];
        lifecycle: "available" | "archived";
        familyId?: string | null | undefined;
        familyName?: string | null | undefined;
        semanticVersion?: string | null | undefined;
      };
      nodes: {
        id: string;
        type: import("../api/flowApi").ApiNodeType;
        displayName: string;
        description?: string | null | undefined;
        libraryId: string;
        flowLibraryName?: string | null | undefined;
        className: string;
        methodName: string;
        dllName: string;
        dllVersion: string;
        returnType: string;
        isAwaitable?: boolean | undefined;
        contractId?: string | null | undefined;
        overloadSignature?: string | null | undefined;
        identityConfidence?: "explicit" | "legacy" | undefined;
        parameters: {
          id: string;
          name: string;
          type: string;
          description?: string | null | undefined;
          required: boolean;
          isVariadic?: boolean | undefined;
          variadicGroupId?: string | null | undefined;
          elementType?: string | null | undefined;
          enumMetadata?: {
            typeName: string;
            isFlags: boolean;
            underlyingType: string;
            options: {
              name: string;
              numericValue: string;
            }[];
          } | null | undefined;
          defaultValue?: string | null | undefined;
          aliases?: string[] | null | undefined;
          identityConfidence?: "explicit" | "legacy" | undefined;
        }[];
      }[];
    }[] = mapResult.filter((item) => item.nodes.length > 0);
    return filterResult;
  });
  const catalogNodeCount = computed(() => {
    const reduceResult: number = libraries.value.reduce((count, library) => count + library.nodes.length, 0);
    return reduceResult;
  });
  const visibleBuiltinNodes = computed(() => {
    const trimResult1 = librarySearch.value.trim();
    const query = trimResult1.toLocaleLowerCase();
    if (!query) {
      return builtinNodes.value;
    }
    const filterResult1: {
      id: string;
      type: import("../api/flowApi").ApiNodeType;
      displayName: string;
      description: string | null;
      ui: {
        kind: string;
        titleKey: string;
        subtitleKey: string;
        description?: string | undefined;
        status: string;
        hasDataOutput: boolean;
        width?: number | undefined;
        category?: "method" | "basic" | undefined;
        libraryId?: string | undefined;
        flowLibraryName?: string | undefined;
        className?: string | undefined;
        methodName?: string | undefined;
        dllName?: string | undefined;
        dllVersion?: string | undefined;
        returnType?: string | undefined;
        targetNodeId?: string | undefined;
        targetFlowId?: string | undefined;
        isAwaitable?: boolean | undefined;
        staticReturnType?: string | undefined;
        isDynamicReturnType?: boolean | undefined;
        targetCanvasId?: string | undefined;
        isPublic?: boolean | undefined;
        flowCallParameterBindings?: {
          callParameterId: string;
          targetParameterId: string;
        }[] | undefined;
        libraryNodeContractId?: string | undefined;
      };
      script?: {
        nodeId: string;
        source: string;
        languageVersion: string;
        sourceHash: string;
        inputs: {
          name: string;
          valueKind: string;
          required: boolean;
          id?: string | undefined;
          description?: string | undefined;
        }[];
        outputs: {
          name: string;
          valueKind: string;
          required: boolean;
          id?: string | undefined;
          description?: string | undefined;
        }[];
      } | null | undefined;
      parameters?: {
        name: string;
        valueJson?: string | undefined;
        source: import("../api/flowApi").ApiDataSource;
        required: boolean;
        ui?: {
          id: string;
          nameKey: string;
          valueKind: string;
          type?: string | undefined;
          description?: string | undefined;
          inputMode?: "connection" | "manual" | "select" | undefined;
          literalValue?: string | undefined;
          projectInputKey?: string | undefined;
          expression?: string | undefined;
          sourceNodeId?: string | undefined;
          sourcePortId?: string | undefined;
          isVariadic?: boolean | undefined;
          variadicGroupId?: string | undefined;
          elementType?: string | undefined;
          variadicMode?: "expanded" | "collection" | undefined;
          enumMetadata?: {
            typeName: string;
            isFlags: boolean;
            underlyingType: string;
            options: {
              name: string;
              numericValue: string;
            }[];
          } | undefined;
        } | undefined;
      }[] | null | undefined;
    }[] = builtinNodes.value.filter((node) => {
      const evaluatedValue1 = [node.displayName, node.description ?? '', node.type, t(node.ui.titleKey), t(node.ui.subtitleKey)];
      const someResult1: boolean = evaluatedValue1.some((value) => {
        const toLocaleLowerCaseResult1 = value.toLocaleLowerCase();
        const includesResult1: boolean = toLocaleLowerCaseResult1.includes(query);
        return includesResult1;
      });
      return someResult1;
    });
    return filterResult1;
  });
  async function refreshLibraryCatalog(projectId?: string): Promise<void> {
    isLibraryCatalogLoading.value = true;
    libraryCatalogError.value = '';
    try {
      const evaluatedValue2: [
        Promise<import("../api/flowApi").BuiltinNodeCatalogDto>,
        Promise<import("../api/libraryApi").ProjectLibraryReferenceDto[]>
      ] = [
        getBuiltinNodeCatalog(),
        projectId ? listProjectLibraries(projectId) : Promise.resolve([]),
      ];
      const bindingSource = await Promise.all(evaluatedValue2);
      const builtins = bindingSource[0];
      const references = bindingSource[1];
      builtinNodes.value = builtins.nodes;
      libraries.value = references.map((reference) => reference.library);
    }
    catch {
      libraryCatalogError.value = t('library.loadFailed');
    }
    finally {
      isLibraryCatalogLoading.value = false;
    }
  }
  function replaceProjectLibraries(nextLibraries: LibraryDto[]): void {
    libraries.value = nextLibraries;
    librarySearch.value = '';
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
  };
}
