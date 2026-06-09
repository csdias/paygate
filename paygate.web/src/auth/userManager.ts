import { UserManager, WebStorageStateStore, type User } from 'oidc-client-ts'

// Single shared UserManager so non-React code (the RTK Query base query) can read the
// access token without going through React context. AuthProvider in main.tsx uses this
// same instance.
export const userManager = new UserManager({
  authority: import.meta.env.VITE_OIDC_AUTHORITY,
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID,
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: window.location.origin,
  response_type: 'code', // Authorization Code + PKCE (PKCE is automatic for code flow)
  scope: 'openid profile role payments.read payments.write',
  userStore: new WebStorageStateStore({ store: window.localStorage }),
})

// After the redirect callback completes, strip ?code/&state from the URL so a refresh
// doesn't try to reprocess a one-time authorization code.
export const onSigninCallback = () => {
  window.history.replaceState({}, document.title, window.location.pathname)
}

// Role claim arrives as a string or string[] (depending on count); normalise the check.
export const hasRole = (user: User | null | undefined, role: string): boolean => {
  const claim = user?.profile?.role as string | string[] | undefined
  return Array.isArray(claim) ? claim.includes(role) : claim === role
}

// Access-token getter for the RTK Query base query (prepareHeaders).
export const getAccessToken = async (): Promise<string | null> => {
  const user = await userManager.getUser()
  return user?.access_token ?? null
}
