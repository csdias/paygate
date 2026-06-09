CREATE TABLE IF NOT EXISTS payment (
    payment_id     UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    amount         NUMERIC(18,2) NOT NULL,
    currency       CHAR(3)       NOT NULL,
    customer_id    UUID          NOT NULL,             -- cardholder paying
    merchant_id    UUID          NOT NULL,             -- business being paid
    card_id        UUID          NULL,                 -- card used (from the customer's cards)
    processor      VARCHAR(30)   NOT NULL DEFAULT 'Omni Card',
    status         VARCHAR(20)   NOT NULL DEFAULT 'Pending',
    decline_reason VARCHAR(200)  NULL,
    reference      VARCHAR(100)  NULL,
    created_by     VARCHAR(200)  NULL,                  -- actor id (token sub) for maker-checker
    created_at     TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_payment_customer_id ON payment (customer_id);
CREATE INDEX IF NOT EXISTS ix_payment_merchant_id ON payment (merchant_id);
CREATE INDEX IF NOT EXISTS ix_payment_status      ON payment (status);
