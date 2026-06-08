-- Canonical outbox schema.
--
-- Table names match IOutboxTableNames with base name "outbox":
--   outbox, outbox_message_registry, outbox_message_type, outbox_message_filter
-- This is what the live code queries — both the OutboxClient (API write side, via
-- PostgresMessageRegistryRepository / PostgresOutboxMessageRepository) and the
-- OutboxPublisher read side (PostgresOutbox). The message payload column is `message`.

DROP TABLE IF EXISTS outbox_message_filter CASCADE;
DROP TABLE IF EXISTS outbox_message_registry CASCADE;
DROP TABLE IF EXISTS outbox_message_type CASCADE;
DROP TABLE IF EXISTS outbox CASCADE;

CREATE TABLE outbox_message_type
(
    message_type_id   integer NOT NULL,
    message_type_desc text    NOT NULL
);

CREATE TABLE outbox_message_registry
(
    message_registry_id      uuid    NOT NULL,
    message_name             text    NOT NULL,
    message_version          text    NOT NULL,
    message_type_id          integer NOT NULL,
    topic                    text    NOT NULL,
    retry_limit              integer NOT NULL,
    retry_backoff_in_seconds integer NOT NULL DEFAULT 60
);

CREATE TABLE outbox
(
    message_id          uuid                     NOT NULL,
    message_registry_id uuid                     NOT NULL,
    context_id          text                     NOT NULL,
    occurred_at         timestamp with time zone NOT NULL,
    message             json                     NOT NULL,
    published_at        timestamp with time zone NULL,
    last_updated        timestamp with time zone NOT NULL,
    retry_count         integer                  NOT NULL DEFAULT 0,
    processed_by        uuid                     NULL,
    traceparent         text                     NULL,
    tracestate          text                     NULL
);

CREATE TABLE outbox_message_filter
(
    message_id   uuid NOT NULL,
    filter_key   text NOT NULL,
    filter_value text NULL
);

ALTER TABLE outbox_message_type ADD CONSTRAINT outbox_message_type_pk PRIMARY KEY (message_type_id);
ALTER TABLE outbox_message_type ADD CONSTRAINT outbox_message_type_desc_unique UNIQUE (message_type_desc);

ALTER TABLE outbox_message_registry ADD CONSTRAINT outbox_message_registry_pk PRIMARY KEY (message_registry_id);
ALTER TABLE outbox_message_registry ADD CONSTRAINT outbox_message_registry_name_version_unique UNIQUE (message_name, message_version);
CREATE INDEX outbox_message_registry_idx1 ON outbox_message_registry (message_type_id ASC);
ALTER TABLE outbox_message_registry ADD CONSTRAINT outbox_message_registry_fk1 FOREIGN KEY (message_type_id) REFERENCES outbox_message_type (message_type_id);

ALTER TABLE outbox ADD CONSTRAINT outbox_pk PRIMARY KEY (message_id);
CREATE INDEX outbox_idx1 ON outbox (message_registry_id ASC);
CREATE INDEX outbox_idx2 ON outbox (message_registry_id ASC, context_id ASC, occurred_at ASC);
CREATE INDEX outbox_idx3 ON outbox (published_at ASC, retry_count ASC, processed_by ASC, last_updated ASC);
ALTER TABLE outbox ADD CONSTRAINT outbox_fk1 FOREIGN KEY (message_registry_id) REFERENCES outbox_message_registry (message_registry_id);

ALTER TABLE outbox_message_filter ADD CONSTRAINT outbox_message_filter_pk PRIMARY KEY (message_id, filter_key);
CREATE INDEX outbox_message_filter_idx1 ON outbox_message_filter (message_id ASC);
ALTER TABLE outbox_message_filter ADD CONSTRAINT outbox_message_filter_fk1 FOREIGN KEY (message_id) REFERENCES outbox (message_id) ON DELETE CASCADE;

INSERT INTO outbox_message_type (message_type_id, message_type_desc) VALUES (1, 'Event');
INSERT INTO outbox_message_type (message_type_id, message_type_desc) VALUES (2, 'Command');
