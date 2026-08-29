export function authorizationLabel(authorization: string, labels: Record<string, string>): string {
  if (authorization === 'Administrator') return labels.authAdministrator
  if (authorization === 'Manager') return labels.authManager
  return labels.authViewer
}
