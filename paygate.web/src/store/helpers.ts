import { fetchBaseQuery } from '@reduxjs/toolkit/query'

// Single place to add auth headers, error extraction, or 401 handling
// when those requirements arrive.
export const appBaseQuery = fetchBaseQuery({
  baseUrl: import.meta.env.VITE_API_URL ?? '',
  fetchFn: (input, init) => fetch(input, { ...init, cache: 'no-store' }),
})
