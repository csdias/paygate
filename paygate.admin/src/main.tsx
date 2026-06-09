import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Provider } from 'react-redux'
import { RouterProvider } from 'react-router-dom'
import { AuthProvider } from 'react-oidc-context'
import { store } from './store/store'
import { router } from './config/routes/routes'
import { userManager, onSigninCallback } from './auth/userManager'
import './styles/main.scss'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provider store={store}>
      <AuthProvider userManager={userManager} onSigninCallback={onSigninCallback}>
        <RouterProvider router={router} />
      </AuthProvider>
    </Provider>
  </StrictMode>,
)
