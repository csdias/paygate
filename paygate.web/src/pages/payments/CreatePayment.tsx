import { type ChangeEvent, type FormEvent, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from '@mui/material'
import { useCreatePaymentMutation } from '../../store/payments/payments.api'
import { useGetCustomersQuery, useGetMerchantsQuery } from '../../store/directory/directory.api'
import { ROUTES } from '../../config/routes/paths'

interface FormState {
  amount: string
  currency: string
  customerId: string
  merchantId: string
  cardId: string
  reference: string
}

const INITIAL: FormState = {
  amount: '',
  currency: 'GBP',
  customerId: '',
  merchantId: '',
  cardId: '',
  reference: '',
}

export default function CreatePayment() {
  const navigate = useNavigate()
  const [createPayment, { isLoading, isError }] = useCreatePaymentMutation()
  const { data: customers = [], isLoading: customersLoading } = useGetCustomersQuery()
  const { data: merchants = [], isLoading: merchantsLoading } = useGetMerchantsQuery()
  const [form, setForm] = useState<FormState>(INITIAL)

  const handleChange = (e: ChangeEvent<HTMLInputElement>) =>
    setForm((prev) => ({ ...prev, [e.target.name]: e.target.value }))

  // Picking a customer (cardholder) resets the card to that customer's main card.
  const handleCustomerChange = (e: ChangeEvent<HTMLInputElement>) => {
    const customerId = e.target.value
    const cards = customers.find((c) => c.id === customerId)?.cards ?? []
    const mainCard = cards.find((c) => c.isMain) ?? cards[0]
    setForm((prev) => ({ ...prev, customerId, cardId: mainCard?.id ?? '' }))
  }

  const customerCards = customers.find((c) => c.id === form.customerId)?.cards ?? []

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    const result = await createPayment({
      amount: parseFloat(form.amount),
      currency: form.currency.toUpperCase(),
      customerId: form.customerId,
      merchantId: form.merchantId,
      cardId: form.cardId,
      reference: form.reference.trim() || undefined,
    })
    if (!('error' in result)) navigate(ROUTES.payments.root.full())
  }

  return (
    <Box component="form" onSubmit={handleSubmit} sx={{ maxWidth: 480 }}>
      <Typography variant="h5" sx={{ mb: 3 }}>New Payment</Typography>

      {isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Failed to create payment. Check all fields and try again.
        </Alert>
      )}

      <Stack spacing={2.5}>
        <TextField
          label="Amount" name="amount" type="number"
          value={form.amount} onChange={handleChange} required
          inputProps={{ min: '0.01', step: '0.01' }}
        />
        <TextField
          label="Currency" name="currency"
          value={form.currency} onChange={handleChange} required
          inputProps={{ maxLength: 3 }}
          helperText="3-letter ISO 4217 code, e.g. GBP, EUR, USD"
        />
        <TextField
          select label="Customer (cardholder)" name="customerId"
          value={form.customerId} onChange={handleCustomerChange} required
          disabled={customersLoading}
          helperText="The paying cardholder"
        >
          {customers.map((c) => (
            <MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>
          ))}
        </TextField>
        <TextField
          select label="Card" name="cardId"
          value={form.cardId} onChange={handleChange} required
          disabled={!form.customerId}
          helperText={form.customerId ? 'Processed by Omni Card' : 'Choose a customer first'}
        >
          {customerCards.map((c) => (
            <MenuItem key={c.id} value={c.id}>
              {c.brand} ••{c.last4}{c.isMain ? ' (main)' : ''}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select label="Merchant" name="merchantId"
          value={form.merchantId} onChange={handleChange} required
          disabled={merchantsLoading}
          helperText="The business being paid"
        >
          {merchants.map((m) => (
            <MenuItem key={m.id} value={m.id}>{m.name} · {m.category}</MenuItem>
          ))}
        </TextField>
        <TextField
          label="Reference" name="reference"
          value={form.reference} onChange={handleChange}
          helperText="Optional payment reference"
        />

        <Box sx={{ display: 'flex', gap: 1, pt: 1 }}>
          <Button variant="outlined" onClick={() => navigate(ROUTES.payments.root.full())} disabled={isLoading}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={isLoading} sx={{ minWidth: 140 }}>
            {isLoading ? <CircularProgress size={20} color="inherit" /> : 'Create Payment'}
          </Button>
        </Box>
      </Stack>
    </Box>
  )
}
