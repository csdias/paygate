import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'

// redirect_uri landing. react-oidc-context processes the code on mount; then we go home.
export default function Callback() {
  const auth = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (auth.isAuthenticated) navigate('/', { replace: true })
  }, [auth.isAuthenticated, navigate])

  return (
    <div style={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}>
      Completing sign-in…
    </div>
  )
}
