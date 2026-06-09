import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from 'react-oidc-context'
import { Box, CircularProgress, Stack, Typography } from '@mui/material'

// Landing for redirect_uri (/callback). react-oidc-context auto-processes the authorization
// code on mount; once authenticated we bounce to the app root.
export default function Callback() {
  const auth = useAuth()
  const navigate = useNavigate()

  useEffect(() => {
    if (auth.isAuthenticated) navigate('/', { replace: true })
  }, [auth.isAuthenticated, navigate])

  return (
    <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}>
      <Stack spacing={2} alignItems="center">
        <CircularProgress />
        <Typography color="text.secondary">Completing sign-in…</Typography>
      </Stack>
    </Box>
  )
}
