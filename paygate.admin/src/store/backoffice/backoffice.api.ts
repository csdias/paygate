import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react'
import type { PagedPending } from './backoffice.types'

export const backofficeApi = createApi({
  reducerPath: 'backofficeApi',
  baseQuery: fetchBaseQuery({ baseUrl: '/payments' }),
  tagTypes: ['Pending'],
  endpoints: (builder) => ({
    getPending: builder.query<PagedPending, void>({
      query: () => '?status=Pending&limit=50',
      providesTags: ['Pending'],
    }),
    approvePayment: builder.mutation<unknown, string>({
      query: (id) => ({ url: `/${id}/approve`, method: 'POST' }),
      invalidatesTags: ['Pending'],
    }),
    rejectPayment: builder.mutation<unknown, { id: string; reason?: string }>({
      query: ({ id, reason }) => ({ url: `/${id}/reject`, method: 'POST', body: { reason } }),
      invalidatesTags: ['Pending'],
    }),
  }),
})

export const { useGetPendingQuery, useApprovePaymentMutation, useRejectPaymentMutation } = backofficeApi
