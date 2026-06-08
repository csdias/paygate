import { useNavigate } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { useListPaymentsQuery } from '../../store/payments/payments.api'
import { setAfterPage } from '../../store/payments/payments.slice'
import { useAppDispatch, useAppSelector } from '../../hooks/redux.hooks'
import { useCustomerNames } from '../../hooks/useCustomerNames'
import { useMerchantNames } from '../../hooks/useMerchantNames'
import { ROUTES } from '../../config/routes/paths'

const STATUS_COLOR = {
  Pending:    'warning',
  Authorized: 'success',
  Declined:   'error',
} as const

type StatusKey = keyof typeof STATUS_COLOR

export default function PaymentsList() {
  const navigate = useNavigate()
  const dispatch = useAppDispatch()
  const afterId = useAppSelector((s) => s.payments.afterId)
  const customerName = useCustomerNames()
  const merchantName = useMerchantNames()

  const { data, isFetching, isError } = useListPaymentsQuery({ afterId, limit: 10 })

  if (isError) {
    return <Alert severity="error">Failed to load payments. Is the API running?</Alert>
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
        <Typography variant="h5">Payments</Typography>
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => navigate(ROUTES.payments.new.full())}
        >
          New Payment
        </Button>
      </Box>

      <TableContainer component={Paper} variant="outlined">
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>ID</TableCell>
              <TableCell align="right">Amount</TableCell>
              <TableCell>Currency</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Customer</TableCell>
              <TableCell>Merchant</TableCell>
              <TableCell>Created</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {isFetching && !data && (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 4 }}>
                  <CircularProgress size={24} />
                </TableCell>
              </TableRow>
            )}
            {data?.items.map((p) => (
              <TableRow
                key={p.paymentId}
                hover
                sx={{ cursor: 'pointer' }}
                onClick={() => navigate(ROUTES.payments.detail.full(p.paymentId))}
              >
                <TableCell sx={{ fontFamily: 'monospace', fontSize: '0.75rem' }}>
                  {p.paymentId.slice(0, 8)}…
                </TableCell>
                <TableCell align="right">{p.amount.toFixed(2)}</TableCell>
                <TableCell>{p.currency}</TableCell>
                <TableCell>
                  <Chip
                    label={p.status}
                    color={STATUS_COLOR[p.status as StatusKey] ?? 'default'}
                    size="small"
                  />
                </TableCell>
                <TableCell>{customerName(p.customerId)}</TableCell>
                <TableCell>{merchantName(p.merchantId)}</TableCell>
                <TableCell>{new Date(p.createdAt).toLocaleString()}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      <Box sx={{ mt: 2, display: 'flex', justifyContent: 'flex-end', gap: 1, alignItems: 'center' }}>
        {isFetching && data && <CircularProgress size={20} />}
        {!isFetching && data?.nextCursor && (
          <Button size="small" onClick={() => dispatch(setAfterPage(data.nextCursor))}>
            Load more
          </Button>
        )}
      </Box>
    </Box>
  )
}
