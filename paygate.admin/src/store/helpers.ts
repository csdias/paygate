import { fetchBaseQuery } from '@reduxjs/toolkit/query'
import { getAccessToken } from '../auth/userManager'

// Factory for a fetchBaseQuery that attaches the OIDC access token. Each API keeps its own
// relative baseUrl (routed to :5000 by the Vite dev proxy); only the auth header is shared.
export const makeBaseQuery = (baseUrl: string) =>
  fetchBaseQuery({
    baseUrl,
    prepareHeaders: async (headers) => {
      const token = await getAccessToken()
      if (token) headers.set('Authorization', `Bearer ${token}`)
      return headers
    },
  })
