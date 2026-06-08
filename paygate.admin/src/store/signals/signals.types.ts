export type LogLevel = 'Verbose' | 'Debug' | 'Information' | 'Warning' | 'Error' | 'Fatal'

export interface LogEntry {
  timestamp: string
  level: string
  message: string
  service: string
  traceId: string | null
  spanId: string | null
  properties: Record<string, string>
}

export interface Filters {
  service: string
  level: string
  traceId: string
  search: string
}
