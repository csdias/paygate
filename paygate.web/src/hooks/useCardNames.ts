import { useMemo } from 'react'
import { useGetCustomersQuery } from '../store/directory/directory.api'

/**
 * Returns a resolver that maps a card id to a display label like "Visa ••4242".
 * Cards belong to customers. Unknown ids fall back to a short id; undefined returns a dash.
 */
export function useCardNames() {
  const { data: customers } = useGetCustomersQuery()
  return useMemo(() => {
    const byId = new Map<string, string>()
    for (const c of customers ?? [])
      for (const card of c.cards) byId.set(card.id, `${card.brand} ••${card.last4}`)
    return (id?: string) => (id ? byId.get(id) ?? `${id.slice(0, 8)}…` : '—')
  }, [customers])
}
