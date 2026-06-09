import { fetchBaseQuery } from '@reduxjs/toolkit/query'
import { getAccessToken } from '../auth/userManager'

// Attaches the OIDC access token to every API request as a Bearer token.
export const appBaseQuery = fetchBaseQuery({
  baseUrl: import.meta.env.VITE_API_URL ?? '',
  fetchFn: (input, init) => fetch(input, { ...init, cache: 'no-store' }),
  prepareHeaders: async (headers) => {
    const token = await getAccessToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)
    return headers
  },
})
