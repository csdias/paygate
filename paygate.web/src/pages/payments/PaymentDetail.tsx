import { useNavigate, useParams } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Divider,
  Paper,
  Stack,
  Typography,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import { useGetPaymentQuery } from '../../store/payments/payments.api'
import { useCustomerNames } from '../../hooks/useCustomerNames'
import { useMerchantNames } from '../../hooks/useMerchantNames'
import { useCardNames } from '../../hooks/useCardNames'
import { ROUTES } from '../../config/routes/paths'

const STATUS_COLOR = {
  Pending:    'warning',
  Authorized: 'success',
  Declined:   'error',
} as const

type StatusKey = keyof typeof STATUS_COLOR

function Field({ label, value, mono = false }: { label: string; value: React.ReactNode; mono?: boolean }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" display="block">{label}</Typography>
      <Typography variant="body2" sx={{ fontFamily: mono ? 'monospace' : 'inherit', wordBreak: 'break-all' }}>
        {value}
      </Typography>
    </Box>
  )
}

export default function PaymentDetail() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const customerName = useCustomerNames()
  const merchantName = useMerchantNames()
  const cardName = useCardNames()
  const { data, isFetching, isError } = useGetPaymentQuery(id!)

  if (isFetching) return <CircularProgress />
  if (isError || !data) return <Alert severity="error">Payment not found.</Alert>

  return (
    <Box sx={{ maxWidth: 600 }}>
      <Button startIcon={<ArrowBackIcon />} onClick={() => navigate(ROUTES.payments.root.full())} sx={{ mb: 2 }}>
        Back to payments
      </Button>

      <Typography variant="h5" sx={{ mb: 3 }}>Payment detail</Typography>

      <Paper variant="outlined" sx={{ p: 3 }}>
        <Stack spacing={2} divider={<Divider />}>
          <Field label="Payment ID" value={data.paymentId} mono />
          <Field label="Status" value={
            <Chip label={data.status} color={STATUS_COLOR[data.status as StatusKey] ?? 'default'} size="small" sx={{ mt: 0.5 }} />
          } />
          <Field label="Amount" value={`${data.amount.toFixed(2)} ${data.currency}`} />
          <Field label="Customer (cardholder)" value={customerName(data.customerId)} />
          <Field label="Merchant" value={merchantName(data.merchantId)} />
          <Field label="Card" value={`${cardName(data.cardId)} · ${data.processor}`} />
          {data.declineReason && <Field label="Decline reason" value={data.declineReason} />}
          {data.reference && <Field label="Reference" value={data.reference} />}
          <Field label="Created" value={new Date(data.createdAt).toLocaleString()} />
          <Field label="Updated" value={new Date(data.updatedAt).toLocaleString()} />
        </Stack>
      </Paper>
    </Box>
  )
}
