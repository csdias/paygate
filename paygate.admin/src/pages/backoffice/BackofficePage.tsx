import { useAuth } from 'react-oidc-context'
import { hasRole } from '../../auth/userManager'
import {
  useGetPendingQuery,
  useApprovePaymentMutation,
  useRejectPaymentMutation,
} from '../../store/backoffice/backoffice.api'

function formatTime(ts: string) {
  return new Date(ts).toLocaleString('en-GB', { hour12: false })
}

export default function BackofficePage() {
  const auth = useAuth()
  const canDecide = hasRole(auth.user, 'PaymentApprover')
  const { data, isLoading, isError } = useGetPendingQuery(undefined, { pollingInterval: 4000 })
  const [approve, approveState] = useApprovePaymentMutation()
  const [reject, rejectState] = useRejectPaymentMutation()

  const busy = approveState.isLoading || rejectState.isLoading
  const pending = data?.items ?? []

  const onReject = (id: string) => {
    const reason = window.prompt('Reason for declining this payment?', 'Insufficient funds')
    if (reason === null) return // cancelled
    void reject({ id, reason: reason || undefined })
  }

  return (
    <>
      <div className="page-header">
        <span className="page-title">Backoffice</span>
        <span className="filter-count">
          {pending.length} pending{!canDecide ? ' · read-only (PaymentApprover role required)' : ''}
        </span>
      </div>

      <div className="log-table-wrap">
        {isError ? (
          <div className="empty-state">
            <div className="empty-state-text">Couldn't load payments — is the API running?</div>
          </div>
        ) : isLoading ? (
          <div className="empty-state"><div className="empty-state-text">Loading…</div></div>
        ) : pending.length === 0 ? (
          <div className="empty-state">
            <div className="empty-state-icon">✓</div>
            <div className="empty-state-text">No payments awaiting a decision.</div>
          </div>
        ) : (
          <table className="log-table">
            <colgroup>
              <col style={{ width: '90px' }} />
              <col style={{ width: '110px' }} />
              <col />
              <col style={{ width: '110px' }} />
              <col style={{ width: '170px' }} />
            </colgroup>
            <thead>
              <tr>
                <th>ID</th>
                <th>Amount</th>
                <th>Processor</th>
                <th>Created</th>
                <th>Decision</th>
              </tr>
            </thead>
            <tbody>
              {pending.map((p) => (
                <tr key={p.paymentId} className="log-row">
                  <td className="log-time">{p.paymentId.slice(0, 8)}…</td>
                  <td className="amount-cell">{p.amount.toFixed(2)} {p.currency}</td>
                  <td>{p.processor}</td>
                  <td className="log-time">{formatTime(p.createdAt)}</td>
                  <td>
                    {canDecide ? (
                      <div className="bo-actions">
                        <button className="btn-approve" disabled={busy} onClick={() => approve(p.paymentId)}>
                          Approve
                        </button>
                        <button className="btn-reject" disabled={busy} onClick={() => onReject(p.paymentId)}>
                          Reject
                        </button>
                      </div>
                    ) : (
                      <span className="log-time">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  )
}
