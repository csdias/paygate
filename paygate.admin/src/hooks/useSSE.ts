import { useEffect, useRef } from 'react'
import type { LogEntry } from '../store/signals/signals.types'

export function useSSE(onEntry: (entry: LogEntry) => void) {
  // Keep a ref so the effect never needs to restart when onEntry changes
  const callbackRef = useRef(onEntry)
  callbackRef.current = onEntry

  useEffect(() => {
    const source = new EventSource('/telemetry/stream')

    source.onmessage = (e) => {
      try {
        callbackRef.current(JSON.parse(e.data) as LogEntry)
      } catch {
        // ignore malformed frames
      }
    }

    // EventSource auto-reconnects on error — no manual handling needed
    return () => source.close()
  }, []) // intentionally empty: the ref keeps the callback current
}
