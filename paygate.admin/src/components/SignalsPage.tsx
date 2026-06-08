import { useCallback, useEffect, useMemo, useState } from 'react'
import type { Filters, LogEntry } from '../types'
import { useSSE } from '../hooks/useSSE'
import FilterBar from './FilterBar'
import LogTable from './LogTable'
import TracePanel from './TracePanel'

const EMPTY_FILTERS: Filters = { service: '', level: '', traceId: '', search: '' }
const MAX_ENTRIES = 2_000

export default function SignalsPage() {
  const [entries, setEntries]         = useState<LogEntry[]>([])
  const [filters, setFilters]         = useState<Filters>(EMPTY_FILTERS)
  const [selectedTrace, setTrace]     = useState<string | null>(null)
  const [paused, setPaused]           = useState(false)

  // Load historical entries on mount
  useEffect(() => {
    fetch('/telemetry/logs?limit=500')
      .then(r => r.ok ? r.json() : [])
      .then((data: LogEntry[]) => {
        // API returns oldest-first; we display newest-first
        setEntries([...data].reverse())
      })
      .catch(() => { /* API not running yet */ })
  }, [])

  // SSE — prepend new entries at the top
  const onEntry = useCallback((entry: LogEntry) => {
    if (paused) return
    setEntries(prev => [entry, ...prev].slice(0, MAX_ENTRIES))
  }, [paused])

  useSSE(onEntry)

  // Client-side filtering
  const filtered = useMemo(() => {
    const { service, level, traceId, search } = filters
    return entries.filter(e => {
      if (service  && e.service !== service) return false
      if (level    && e.level   !== level)   return false
      if (traceId  && e.traceId !== traceId) return false
      if (search   && !e.message.toLowerCase().includes(search.toLowerCase())) return false
      return true
    })
  }, [entries, filters])

  const services = useMemo(() =>
    [...new Set(entries.map(e => e.service))].sort(),
  [entries])

  const traceEntries = useMemo(() =>
    selectedTrace ? entries.filter(e => e.traceId === selectedTrace) : [],
  [entries, selectedTrace])

  return (
    <>
      <div className="page-header">
        <span className="page-title">Signals</span>
        <div className="live-indicator">
          <div className={`live-dot ${paused ? 'paused' : ''}`} />
          <span
            style={{ cursor: 'pointer', userSelect: 'none' }}
            onClick={() => setPaused(p => !p)}
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
        onChange={setFilters}
        onClear={() => setFilters(EMPTY_FILTERS)}
      />

      <div className="signals-body">
        <LogTable
          entries={filtered}
          onTraceSelect={(id) => setTrace(prev => prev === id ? null : id)}
        />
        {selectedTrace && (
          <TracePanel
            traceId={selectedTrace}
            entries={traceEntries}
            onClose={() => setTrace(null)}
          />
        )}
      </div>
    </>
  )
}
