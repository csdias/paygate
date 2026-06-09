import { createApi } from '@reduxjs/toolkit/query/react'
import type { LogEntry } from './signals.types'
import { makeBaseQuery } from '../helpers'

export const signalsApi = createApi({
  reducerPath: 'signalsApi',
  baseQuery: makeBaseQuery('/telemetry'),
  endpoints: (builder) => ({
    getLogs: builder.query<LogEntry[], { limit?: number }>({
      query: ({ limit = 500 } = {}) => `logs?limit=${limit}`,
    }),
  }),
})

export const { useGetLogsQuery } = signalsApi
