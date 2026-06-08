import type { Filters } from '../store/signals/signals.types'

const LEVELS = ['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal']

interface Props {
  filters: Filters
  services: string[]
  count: number
  total: number
  onChange: (f: Filters) => void
  onClear: () => void
}

export default function FilterBar({ filters, services, count, total, onChange, onClear }: Props) {
  const set = (key: keyof Filters) => (e: React.ChangeEvent<HTMLSelectElement | HTMLInputElement>) =>
    onChange({ ...filters, [key]: e.target.value })

  const hasFilters = Object.values(filters).some(Boolean)

  return (
    <div className="filter-bar">
      <select className="filter-select" value={filters.service} onChange={set('service')}>
        <option value="">All services</option>
        {services.map(s => <option key={s} value={s}>{s}</option>)}
      </select>

      <select className="filter-select" value={filters.level} onChange={set('level')}>
        <option value="">All levels</option>
        {LEVELS.map(l => <option key={l} value={l}>{l}</option>)}
      </select>

      <div className="filter-divider" />

      <input
        className="filter-input"
        placeholder="Trace ID…"
        value={filters.traceId}
        onChange={set('traceId')}
        spellCheck={false}
      />
      <input
        className="filter-input"
        placeholder="Search messages…"
        value={filters.search}
        onChange={set('search')}
      />

      {hasFilters && (
        <button className="btn-clear" onClick={onClear}>Clear</button>
      )}

      <span className="filter-count">
        {count === total ? `${total} entries` : `${count} / ${total}`}
      </span>
    </div>
  )
}
