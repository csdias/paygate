import { combineReducers, configureStore } from '@reduxjs/toolkit'
import { setupListeners } from '@reduxjs/toolkit/query'
import { signalsApi } from './signals/signals.api'
import { backofficeApi } from './backoffice/backoffice.api'
import signalsReducer from './signals/signals.slice'

const rootReducer = combineReducers({
  signals: signalsReducer,
  [signalsApi.reducerPath]: signalsApi.reducer,
  [backofficeApi.reducerPath]: backofficeApi.reducer,
})

export const store = configureStore({
  reducer: rootReducer,
  middleware: (getDefaultMiddleware) =>
    getDefaultMiddleware().concat(signalsApi.middleware).concat(backofficeApi.middleware),
})

setupListeners(store.dispatch)

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch
