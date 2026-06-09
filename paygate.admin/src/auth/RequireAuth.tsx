import type { ReactNode } from 'react'
import { useAuth } from 'react-oidc-context'

const center: React.CSSProperties = {
  display: 'grid', placeItems: 'center', minHeight: '100vh', textAlign: 'center',
}

// App-wide gate with an explicit Sign-in button (no auto-redirect).
export default function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth()

  if (auth.isLoading) return <div style={center}>Loading…</div>
  if (auth.error) return <div style={center}>Authentication error: {auth.error.message}</div>

  if (!auth.isAuthenticated) {
    return (
      <div style={center}>
        <div>
          <h1>Paygate Admin</h1>
          <p>Please sign in to continue.</p>
          <button className="btn-approve" onClick={() => void auth.signinRedirect()}>
            Sign in
          </button>
        </div>
      </div>
    )
  }

  return <>{children}</>
}
