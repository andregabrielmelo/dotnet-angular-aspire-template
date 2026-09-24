// A third-party sign-in option (Google, GitHub, ...) brokered by Keycloak. Returned by
// GET /backend-for-frontend/providers - only providers configured for this environment.
export interface ExternalIdentityProvider {
  alias: string;
  displayName: string;
}
