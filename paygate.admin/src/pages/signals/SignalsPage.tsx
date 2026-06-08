import { useCallback, useEffect, useMemo } from 'react'
import { useAppDispatch, useAppSelector } from '../../hooks/redux.hooks'
import {
  prependEntry,
  setEntries,
  setFilters,
  clearFilters,
  setSelectedTrace,
  togglePaused,
} from '../../store/signals/signals.slice'
import { useGetLogsQuery } from '../../store/signals/signals.api'
import type { LogEntry } from '../../store/signals/signals.types'
import { useSSE } from '../../hooks/useSSE'
import FilterBar from '../../components/FilterBar'
import LogTable from '../../components/LogTable'
import TracePanel from '../../components/TracePanel'

export default function SignalsPage() {
  const dispatch = useAppDispatch()
  const { entries, filters, selectedTrace, paused } = useAppSelector((s) => s.signals)

  // RTK Query handles the fetch lifecycle — seed the slice on success
  const { data } = useGetLogsQuery({ limit: 500 })
  useEffect(() => {
    if (data) dispatch(setEntries([...data].reverse()))
  }, [data, dispatch])

  // SSE — each frame dispatches directly to the slice
  const onEntry = useCallback((entry: LogEntry) => {
    dispatch(prependEntry(entry))
  }, [dispatch])
  useSSE(onEntry)

  const filtered = useMemo(() => {
    const { service, level, traceId, search } = filters
    return entries.filter((e) => {
      if (service && e.service !== service) return false
      if (level   && e.level   !== level)   return false
      if (traceId && e.traceId !== traceId) return false
      if (search  && !e.message.toLowerCase().includes(search.toLowerCase())) return false
      return true
    })
  }, [entries, filters])

  const services = useMemo(
    () => [...new Set(entries.map((e) => e.service))].sort(),
    [entries],
  )

  const traceEntries = useMemo(
    () => (selectedTrace ? entries.filter((e) => e.traceId === selectedTrace) : []),
    [entries, selectedTrace],
  )

  return (
    <>
      <div className="page-header">
        <span className="page-title">Signals</span>
        <div className="live-indicator">
          <div className={`live-dot ${paused ? 'paused' : ''}`} />
          <span
            style={{ cursor: 'pointer', userSelect: 'none' }}
            onClick={() => dispatch(togglePaused())}
          >
            {paused ? 'Paused' : 'Live'}
          </span>
        </div>
      </div>

      <FilterBar
        filters={filters}
        services={services}
        count={filtered.length}
        total={entries.length}
        onChange={(f) => dispatch(setFilters(f))}
        onClear={() => dispatch(clearFilters())}
      />

      <div className="signals-body">
        <LogTable
          entries={filtered}
          onTraceSelect={(id) =>
            dispatch(setSelectedTrace(selectedTrace === id ? null : id))
          }
        />
        {selectedTrace && (
          <TracePanel
            traceId={selectedTrace}
            entries={traceEntries}
            onClose={() => dispatch(setSelectedTrace(null))}
          />
        )}
      </div>
    </>
  )
}
