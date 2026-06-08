import type { LogEntry } from '../store/signals/signals.types'

function formatTime(ts: string) {
  const d = new Date(ts)
  return d.toLocaleTimeString('en-GB', { hour12: false }) +
    '.' + String(d.getMilliseconds()).padStart(3, '0')
}

interface Props {
  traceId: string
  entries: LogEntry[]   // all entries, filtered by caller to this trace
  onClose: () => void
}

export default function TracePanel({ traceId, entries, onClose }: Props) {
  // Oldest first for chronological timeline
  const sorted = [...entries].sort(
    (a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
  )

  return (
    <aside className="trace-panel">
      <div className="trace-panel-header">
        <span className="trace-panel-title">Trace</span>
        <button className="btn-close" onClick={onClose} title="Close">✕</button>
      </div>

      <div className="trace-id-display" title={traceId}>{traceId}</div>

      <div className="trace-timeline">
        {sorted.length === 0 ? (
          <div className="empty-state" style={{ height: '120px' }}>
            <div className="empty-state-text">No entries for this trace.</div>
          </div>
        ) : (
          sorted.map((entry, i) => {
            const level = entry.level.toLowerCase()
            return (
              <div key={i} className="trace-event">
                <div className="trace-spine">
                  <div className="trace-dot" data-level={level} />
                  <div className="trace-line" />
                </div>
                <div className="trace-event-content">
                  <div className="trace-event-time">{formatTime(entry.timestamp)}</div>
                  <div className="trace-event-message">{entry.message}</div>
                  <div className="trace-event-meta">
                    <span
                      className="level-badge"
                      data-level={level}
                      style={{ fontSize: '10px' }}
                    >
                      {entry.level.slice(0, 4).toUpperCase()}
                    </span>
                    <span
                      className="service-chip"
                      style={{ fontSize: '11px', padding: '1px 6px' }}
                    >
                      {entry.service}
                    </span>
                  </div>
                </div>
              </div>
            )
          })
        )}
      </div>
    </aside>
  )
}
