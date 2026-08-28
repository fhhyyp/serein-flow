export function isArchivedProjectStatus(status: string | undefined): boolean {
  return status?.trim().toLowerCase() === 'archived'
}
