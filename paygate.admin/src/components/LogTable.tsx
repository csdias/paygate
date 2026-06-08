import { useState } from 'react'
import type { LogEntry } from '../store/signals/signals.types'

// Stable hue per service name so each service gets a distinct colour
function serviceStyle(service: string): React.CSSProperties {
  const palette = [
    { bg: 'rgba(99,102,241,.15)',  color: '#818cf8' },
    { bg: 'rgba(139,92,246,.15)',  color: '#a78bfa' },
    { bg: 'rgba(6,182,212,.15)',   color: '#22d3ee' },
    { bg: 'rgba(16,185,129,.15)',  color: '#34d399' },
    { bg: 'rgba(245,158,11,.15)',  color: '#fbbf24' },
  ]
  const idx = [...service].reduce((acc, c) => acc + c.charCodeAt(0), 0) % palette.length
  return { background: palette[idx].bg, color: palette[idx].color }
}

function formatTime(ts: string) {
  const d = new Date(ts)
  return d.toLocaleTimeString('en-GB', { hour12: false }) +
    '.' + String(d.getMilliseconds()).padStart(3, '0')
}

interface Props {
  entries: LogEntry[]
  onTraceSelect: (traceId: string) => void
}

export default function LogTable({ entries, onTraceSelect }: Props) {
  const [expanded, setExpanded] = useState<Set<number>>(new Set())

  const toggle = (i: number) =>
    setExpanded(prev => {
      const next = new Set(prev)
      next.has(i) ? next.delete(i) : next.add(i)
      return next
    })

  if (entries.length === 0) {
    return (
      <div className="log-table-wrap">
        <div className="empty-state">
          <div className="empty-state-icon">◎</div>
          <div className="empty-state-text">No log entries yet — start the API to see signals.</div>
        </div>
      </div>
    )
  }

  return (
    <div className="log-table-wrap">
      <table className="log-table">
        <colgroup>
          <col className="col-time" />
          <col className="col-level" />
          <col className="col-service" />
          <col className="col-message" />
          <col className="col-trace" />
        </colgroup>
        <thead>
          <tr>
            <th>Time</th>
            <th>Level</th>
            <th>Service</th>
            <th>Message</th>
            <th>Trace</th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry, i) => {
            const isExpanded = expanded.has(i)
            const level = entry.level.toLowerCase()
            const hasProps = Object.keys(entry.properties).length > 0

            return [
              <tr
                key={`row-${i}`}
                className={`log-row ${isExpanded ? 'expanded' : ''}`}
                onClick={() => hasProps && toggle(i)}
              >
                <td className="log-time">{formatTime(entry.timestamp)}</td>
                <td>
                  <span className="level-badge" data-level={level}>
                    {entry.level.slice(0, 4).toUpperCase()}
                  </span>
                </td>
                <td>
                  <span className="service-chip" style={serviceStyle(entry.service)}>
                    {entry.service}
                  </span>
                </td>
                <td>
                  <div className="log-message">{entry.message}</div>
                </td>
                <td>
                  {entry.traceId && (
                    <span
                      className="trace-link"
                      title={entry.traceId}
                      onClick={(e) => { e.stopPropagation(); onTraceSelect(entry.traceId!) }}
                    >
                      {entry.traceId.slice(0, 8)}…
                    </span>
                  )}
                </td>
              </tr>,

              isExpanded && hasProps && (
                <tr key={`props-${i}`} className="log-props-row">
                  <td colSpan={5}>
                    <div className="log-props">
                      {Object.entries(entry.properties).map(([k, v]) => (
                        <span key={k} className="log-prop">
                          <span className="log-prop-key">{k}</span>
                          <span className="log-prop-value">{v}</span>
                        </span>
                      ))}
                    </div>
                  </td>
                </tr>
              ),
            ]
          })}
        </tbody>
      </table>
    </div>
  )
}
