import { useMemo } from 'react'
import { useGetMerchantsQuery } from '../store/directory/directory.api'

/**
 * Returns a resolver that maps a merchant id to its display name. Unknown ids
 * (e.g. payments created before the fixed merchant set) fall back to a short id.
 */
export function useMerchantNames() {
  const { data: merchants } = useGetMerchantsQuery()
  return useMemo(() => {
    const byId = new Map((merchants ?? []).map((m) => [m.id, m.name]))
    return (id: string) => byId.get(id) ?? `${id.slice(0, 8)}…`
  }, [merchants])
}
