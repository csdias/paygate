import { UserManager, WebStorageStateStore, type User } from 'oidc-client-ts'

// Shared UserManager so the RTK Query base query can read the access token outside React.
export const userManager = new UserManager({
  authority: import.meta.env.VITE_OIDC_AUTHORITY,
  client_id: import.meta.env.VITE_OIDC_CLIENT_ID,
  redirect_uri: `${window.location.origin}/callback`,
  post_logout_redirect_uri: window.location.origin,
  response_type: 'code', // Authorization Code + PKCE
  scope: 'openid profile role payments.read payments.approve',
  userStore: new WebStorageStateStore({ store: window.localStorage }),
})

export const onSigninCallback = () => {
  window.history.replaceState({}, document.title, window.location.pathname)
}

export const hasRole = (user: User | null | undefined, role: string): boolean => {
  const claim = user?.profile?.role as string | string[] | undefined
  return Array.isArray(claim) ? claim.includes(role) : claim === role
}

export const getAccessToken = async (): Promise<string | null> => {
  const user = await userManager.getUser()
  return user?.access_token ?? null
}
