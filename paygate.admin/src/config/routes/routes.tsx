import { createBrowserRouter } from 'react-router-dom'
import { lazy, Suspense } from 'react'
import App from '../../App'
import { ROUTES } from './paths'

const SignalsPage = lazy(() => import('../../pages/signals/SignalsPage'))
const BackofficePage = lazy(() => import('../../pages/backoffice/BackofficePage'))

export const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
    children: [
      { index: true, element: <Suspense fallback={null}><SignalsPage /></Suspense> },
      { path: ROUTES.signals.path, element: <Suspense fallback={null}><SignalsPage /></Suspense> },
      { path: ROUTES.backoffice.path, element: <Suspense fallback={null}><BackofficePage /></Suspense> },
    ],
  },
])
