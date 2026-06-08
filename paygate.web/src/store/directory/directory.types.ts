export interface Card {
  id: string
  brand: string
  last4: string
  isMain: boolean
}

// A cardholder — the party paying. Owns one or more cards.
export interface Customer {
  id: string
  name: string
  cards: Card[]
}

// A merchant — the business being paid. Holds no card; settles into an account.
export interface Merchant {
  id: string
  name: string
  category: string
  settlementCurrency: string
}
