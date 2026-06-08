export interface Payment {
  paymentId: string
  amount: number
  currency: string
  customerId: string
  merchantId: string
  cardId?: string
  processor: string
  status: string
  declineReason?: string
  reference?: string
  createdAt: string
  updatedAt: string
}

export interface PagedPayments {
  items: Payment[]
  nextCursor?: string
}

export interface CreatePaymentRequest {
  amount: number
  currency: string
  customerId: string
  merchantId: string
  cardId: string
  reference?: string
}
