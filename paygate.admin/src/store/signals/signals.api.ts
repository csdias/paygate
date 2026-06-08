import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'
import type { LogEntry } from './signals.types'

export const signalsApi = createApi({
  reducerPath: 'signalsApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/telemetry' }),
  endpoints: (builder) => ({
    getLogs: builder.query<LogEntry[], { limit?: number }>({
      query: ({ limit = 500 } = {}) => `logs?limit=${limit}`,
    }),
  }),
})

export const { useGetLogsQuery } = signalsApi
