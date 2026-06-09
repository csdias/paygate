import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { RouterProvider } from 'react-router-dom'
import { AuthProvider } from 'react-oidc-context'
import { CssBaseline, ThemeProvider, createTheme } from '@mui/material'
import { store } from './store/store'
import { router } from './config/routes/routes'
import { userManager, onSigninCallback } from './auth/userManager'

const theme = createTheme({
  palette: { mode: 'light' },
  typography: { fontFamily: '"Inter", "Roboto", "Helvetica", "Arial", sans-serif' },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provider store={store}>
      <AuthProvider userManager={userManager} onSigninCallback={onSigninCallback}>
        <ThemeProvider theme={theme}>
          <CssBaseline />
          <RouterProvider router={router} />
        </ThemeProvider>
      </AuthProvider>
    </Provider>
  </StrictMode>,
)
