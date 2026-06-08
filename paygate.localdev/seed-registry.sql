-- Local-only seed: register PaymentInitiatedEvent so the API can resolve a topic
-- when writing to the outbox, and the OutboxPublisher knows where to publish.
--
-- The topic ARN must match the topic the LocalStack bootstrap creates:
--   paygate-local-payment-events  in region us-east-1, account 000000000000.
--   (us-east-1 locally — see the note in localstack-init/01-bootstrap.sh.)
--
-- Runs after 01-outbox.sql (which creates outbox_message_type/registry and seeds
-- the Event/Command types), so message_type_id = 1 ('Event') already exists.

INSERT INTO outbox_message_registry
    (message_registry_id, message_name, message_version, message_type_id, topic, retry_limit, retry_backoff_in_seconds)
VALUES
    (gen_random_uuid(), 'PaymentInitiatedEvent', '1.0', 1,
     'arn:aws:sns:us-east-1:000000000000:paygate-local-payment-events', 3, 60),
    -- The admin's acquirer decision (approve/reject) publishes this through the same
    -- topic; consumers branch on the message name to handle the decision.
    (gen_random_uuid(), 'PaymentDecidedEvent', '1.0', 1,
     'arn:aws:sns:us-east-1:000000000000:paygate-local-payment-events', 3, 60)
ON CONFLICT (message_name, message_version) DO UPDATE
    SET topic = EXCLUDED.topic;
