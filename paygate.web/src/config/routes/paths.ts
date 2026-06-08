type Id = string

export const ROUTES = {
  payments: {
    root:   { full: () => '/payments',       path: 'payments' },
    new:    { full: () => '/payments/new',   path: 'payments/new' },
    detail: { full: (id: Id) => `/payments/${id}`, path: 'payments/:id' },
  },
} as const
