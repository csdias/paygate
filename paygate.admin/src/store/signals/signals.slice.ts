import { createSlice, type PayloadAction } from '@reduxjs/toolkit'
import type { Filters, LogEntry } from './signals.types'

const MAX_ENTRIES = 2_000

export const EMPTY_FILTERS: Filters = { service: '', level: '', traceId: '', search: '' }

interface SignalsState {
  entries: LogEntry[]
  filters: Filters
  selectedTrace: string | null
  paused: boolean
}

const initialState: SignalsState = {
  entries: [],
  filters: EMPTY_FILTERS,
  selectedTrace: null,
  paused: false,
}

export const signalsSlice = createSlice({
  name: 'signals',
  initialState,
  reducers: {
    // Called once on mount to seed historical entries (newest-first from caller)
    setEntries: (state, action: PayloadAction<LogEntry[]>) => {
      state.entries = action.payload
    },
    // Called per SSE frame — dropped silently when paused
    prependEntry: (state, action: PayloadAction<LogEntry>) => {
      if (state.paused) return
      state.entries.unshift(action.payload)
      if (state.entries.length > MAX_ENTRIES) state.entries.splice(MAX_ENTRIES)
    },
    setFilters: (state, action: PayloadAction<Filters>) => {
      state.filters = action.payload
    },
    clearFilters: (state) => {
      state.filters = EMPTY_FILTERS
    },
    setSelectedTrace: (state, action: PayloadAction<string | null>) => {
      state.selectedTrace = action.payload
    },
    togglePaused: (state) => {
      state.paused = !state.paused
    },
  },
})

export const {
  setEntries,
  prependEntry,
  setFilters,
  clearFilters,
  setSelectedTrace,
  togglePaused,
} = signalsSlice.actions

export default signalsSlice.reducer
