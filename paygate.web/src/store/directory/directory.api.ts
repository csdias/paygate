import { createApi } from '@reduxjs/toolkit/query/react'
import { appBaseQuery } from '../helpers'
import type { Customer, Merchant } from './directory.types'

// Reference data the payment form chooses from: cardholders and merchants.
export const directoryApi = createApi({
  reducerPath: 'directoryApi',
  baseQuery: appBaseQuery,
  endpoints: (builder) => ({
    getCustomers: builder.query<Customer[], void>({
      query: () => '/customers',
    }),
    getMerchants: builder.query<Merchant[], void>({
      query: () => '/merchants',
    }),
  }),
})

export const { useGetCustomersQuery, useGetMerchantsQuery } = directoryApi
