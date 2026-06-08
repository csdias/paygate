CREATE TABLE IF NOT EXISTS payment_audit (
    audit_id    UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id  UUID         NOT NULL,
    event_type  VARCHAR(100) NOT NULL,
    payload     JSONB        NOT NULL,
    occurred_at TIMESTAMPTZ  NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_payment_audit_payment_id  ON payment_audit (payment_id);
CREATE INDEX IF NOT EXISTS ix_payment_audit_occurred_at ON payment_audit (occurred_at);
