import { combineReducers, configureStore } from '@reduxjs/toolkit'
import { setupListeners } from '@reduxjs/toolkit/query'
import { paymentsApi } from './payments/payments.api'
import { directoryApi } from './directory/directory.api'
import paymentsReducer from './payments/payments.slice'

const rootReducer = combineReducers({
  payments: paymentsReducer,
  [paymentsApi.reducerPath]: paymentsApi.reducer,
  [directoryApi.reducerPath]: directoryApi.reducer,
})

export const store = configureStore({
  reducer: rootReducer,
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware().concat(paymentsApi.middleware).concat(directoryApi.middleware),
})

setupListeners(store.dispatch)

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
