import { createApi } from '@reduxjs/toolkit/query/react'
import { appBaseQuery } from '../helpers'
import type { CreatePaymentRequest, PagedPayments, Payment } from './payments.types'

export const paymentsApi = createApi({
  reducerPath: 'paymentsApi',
  baseQuery: appBaseQuery,
  tagTypes: ['Payment'],
  endpoints: (builder) => ({
    listPayments: builder.query<PagedPayments, { afterId?: string; limit?: number }>({
      query: ({ afterId, limit = 10 } = {}) => ({
        url: '/payments',
        params: { ...(afterId ? { afterId } : {}), limit },
      }),
      providesTags: ['Payment'],
    }),

    getPayment: builder.query<Payment, string>({
      query: (id) => `/payments/${id}`,
      providesTags: (_, __, id) => [{ type: 'Payment' as const, id }],
    }),

    createPayment: builder.mutation<Payment, CreatePaymentRequest>({
      query: (body) => ({ url: '/payments', method: 'POST', body }),
      invalidatesTags: ['Payment'],
    }),
  }),
})

export const { useListPaymentsQuery, useGetPaymentQuery, useCreatePaymentMutation } = paymentsApi
