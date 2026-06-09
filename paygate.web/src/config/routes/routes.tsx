import { createBrowserRouter, Navigate } from 'react-router-dom'
import { lazy, Suspense } from 'react'
import App from '../../App'
import RequireAuth from '../../auth/RequireAuth'
import Callback from '../../auth/Callback'
import { ROUTES } from './paths'

const PaymentsList   = lazy(() => import('../../pages/payments/PaymentsList'))
const CreatePayment  = lazy(() => import('../../pages/payments/CreatePayment'))
const PaymentDetail  = lazy(() => import('../../pages/payments/PaymentDetail'))

export const router = createBrowserRouter([
  { path: '/callback', element: <Callback /> },
  {
    path: '/',
    element: <RequireAuth><App /></RequireAuth>,
    children: [
      { index: true, element: <Navigate to={ROUTES.payments.root.full()} replace /> },
      { path: ROUTES.payments.root.path,   element: <Suspense fallback={null}><PaymentsList /></Suspense> },
      { path: ROUTES.payments.new.path,    element: <Suspense fallback={null}><CreatePayment /></Suspense> },
      { path: ROUTES.payments.detail.path, element: <Suspense fallback={null}><PaymentDetail /></Suspense> },
    ],
  },
])
