export interface PendingPayment {
  paymentId: string
  amount: number
  currency: string
  customerId: string
  merchantId: string
  cardId?: string
  processor: string
  status: string
  reference?: string
  createdAt: string
}

export interface PagedPending {
  items: PendingPayment[]
  nextCursor?: string
}
