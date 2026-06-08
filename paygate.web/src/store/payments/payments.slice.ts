import { createSlice, type PayloadAction } from '@reduxjs/toolkit'

// Pagination cursor lives in Redux so navigating to a detail page and back
// returns you to the same page rather than resetting to the first page.
interface PaymentsState {
  afterId: string | undefined
}

const initialState: PaymentsState = {
  afterId: undefined,
}

export const paymentsSlice = createSlice({
  name: 'payments',
  initialState,
  reducers: {
    setAfterPage: (state, action: PayloadAction<string | undefined>) => {
      state.afterId = action.payload
    },
  },
})

export const { setAfterPage } = paymentsSlice.actions
export default paymentsSlice.reducer
