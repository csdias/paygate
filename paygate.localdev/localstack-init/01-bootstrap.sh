#!/bin/bash
# Creates the SNS topic, SQS queues + DLQs, and subscriptions — the LocalStack
# equivalent of paygate.terraform/modules/messaging/main.tf.
#
# Runs automatically when LocalStack reports ready (mounted into ready.d).
set -euo pipefail

# us-east-1 locally: when the AWS SDK is pointed at a ServiceURL (LocalStack) it
# signs as us-east-1 regardless of region settings, so the emulated topology must
# live there too. Region is just a label here — the topology mirrors the Terraform.
REGION=us-east-1
ACCOUNT=000000000000
NAME=paygate-local
QUEUES=(notification audit)

echo "[bootstrap] creating SNS topic ${NAME}-payment-events"
awslocal sns create-topic --name "${NAME}-payment-events" --region "$REGION" >/dev/null
TOPIC_ARN="arn:aws:sns:${REGION}:${ACCOUNT}:${NAME}-payment-events"

for q in "${QUEUES[@]}"; do
  echo "[bootstrap] creating queue ${NAME}-${q} (+ DLQ)"

  awslocal sqs create-queue --queue-name "${NAME}-${q}-dlq" --region "$REGION" >/dev/null
  DLQ_URL=$(awslocal sqs get-queue-url --queue-name "${NAME}-${q}-dlq" --region "$REGION" --output text)
  DLQ_ARN=$(awslocal sqs get-queue-attributes --queue-url "$DLQ_URL" \
      --attribute-names QueueArn --region "$REGION" \
      --query 'Attributes.QueueArn' --output text)

  # maxReceiveCount 3 → after 3 failed receives, the message moves to the DLQ.
  # VisibilityTimeout 30 must be >= the consumer's processing time.
  REDRIVE="{\"deadLetterTargetArn\":\"${DLQ_ARN}\",\"maxReceiveCount\":\"3\"}"
  awslocal sqs create-queue --queue-name "${NAME}-${q}" --region "$REGION" \
      --attributes "{\"RedrivePolicy\":\"$(echo "$REDRIVE" | sed 's/"/\\"/g')\",\"VisibilityTimeout\":\"30\"}" >/dev/null

  Q_URL=$(awslocal sqs get-queue-url --queue-name "${NAME}-${q}" --region "$REGION" --output text)
  Q_ARN=$(awslocal sqs get-queue-attributes --queue-url "$Q_URL" \
      --attribute-names QueueArn --region "$REGION" \
      --query 'Attributes.QueueArn' --output text)

  # raw_message_delivery = false → SQS receives the full SNS envelope (so the
  # consumers can read traceparent from the envelope's MessageAttributes).
  echo "[bootstrap] subscribing ${NAME}-${q} to topic"
  awslocal sns subscribe \
      --topic-arn "$TOPIC_ARN" \
      --protocol sqs \
      --notification-endpoint "$Q_ARN" \
      --attributes '{"RawMessageDelivery":"false"}' \
      --region "$REGION" >/dev/null
done

echo "[bootstrap] done. topic + ${#QUEUES[@]} queues ready."
