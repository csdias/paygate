import type { ReactNode } from 'react'
import { useAuth } from 'react-oidc-context'
import { Box, Button, CircularProgress, Stack, Typography } from '@mui/material'

// Gate for the whole app. While the OIDC state resolves we show a spinner; if the user
// isn't signed in we show an explicit Sign-in screen (a deliberate button press triggers
// the redirect, so you can watch the OAuth flow happen). Once authenticated, render the app.
export default function RequireAuth({ children }: { children: ReactNode }) {
  const auth = useAuth()

  if (auth.isLoading) {
    return (
      <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}>
        <CircularProgress />
      </Box>
    )
  }

  if (auth.error) {
    return (
      <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}>
        <Typography color="error">Authentication error: {auth.error.message}</Typography>
      </Box>
    )
  }

  if (!auth.isAuthenticated) {
    return (
      <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}>
        <Stack spacing={3} alignItems="center">
          <Typography variant="h4" sx={{ fontWeight: 700 }}>Paygate</Typography>
          <Typography color="text.secondary">Please sign in to continue.</Typography>
          <Button variant="contained" size="large" onClick={() => void auth.signinRedirect()}>
            Sign in
          </Button>
        </Stack>
      </Box>
    )
  }

  return <>{children}</>
}
