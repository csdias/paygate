import { useMemo } from 'react'
import { useGetCustomersQuery } from '../store/directory/directory.api'

/**
 * Returns a resolver that maps a customer id to its display name. Unknown ids
 * (e.g. payments created before the fixed customer set) fall back to a short id.
 */
export function useCustomerNames() {
  const { data: customers } = useGetCustomersQuery()
  return useMemo(() => {
    const byId = new Map((customers ?? []).map((c) => [c.id, c.name]))
    return (id: string) => byId.get(id) ?? `${id.slice(0, 8)}…`
  }, [customers])
}
