import type { InjectionKey } from 'vue'
import type { NodeRuntimeMetadata } from './types'

export type LibraryNameResolver = (runtime?: NodeRuntimeMetadata) => string | undefined

export const libraryNameResolverKey: InjectionKey<LibraryNameResolver> = Symbol('libraryNameResolver')
