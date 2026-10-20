export function isArchivedProjectStatus(status: string | undefined): boolean {
  const trimmedStatus = status?.trim();
  const normalizedStatus = trimmedStatus?.toLowerCase();
  const evaluatedValue: boolean = normalizedStatus === 'archived';
  return evaluatedValue;
}
