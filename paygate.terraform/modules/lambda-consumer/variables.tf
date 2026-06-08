variable "name" {
  description = "Unique name for this consumer (e.g. paygate-dev-notification)"
  type        = string
}

variable "source_queue_arn" {
  description = "ARN of the SQS queue that triggers this Lambda"
  type        = string
}

variable "source_queue_url" {
  description = "URL of the SQS queue (needed for the execution role policy)"
  type        = string
}

variable "dead_letter_queue_arn" {
  description = "ARN of the DLQ for this consumer's async invocations"
  type        = string
}

variable "s3_bucket" {
  description = "S3 bucket holding the Lambda deployment ZIP"
  type        = string
}

variable "s3_key" {
  description = "S3 object key for the Lambda deployment ZIP"
  type        = string
}

variable "handler" {
  description = "Lambda handler — for custom runtime bootstrap this is always 'bootstrap'"
  type        = string
  default     = "bootstrap"
}

variable "runtime" {
  description = "Lambda runtime identifier"
  type        = string
  default     = "provided.al2023"
}

variable "timeout_seconds" {
  type    = number
  default = 30
}

variable "memory_mb" {
  type    = number
  default = 256
}

variable "batch_size" {
  description = "Maximum number of SQS messages per Lambda invocation"
  type        = number
  default     = 10
}

variable "maximum_batching_window_seconds" {
  description = "Seconds Lambda waits to fill a batch before invoking"
  type        = number
  default     = 0
}

variable "environment_variables" {
  description = "Environment variables injected into the Lambda function"
  type        = map(string)
  default     = {}
}

variable "db_secret_arn" {
  description = "Secrets Manager ARN the function needs read access to (optional)"
  type        = string
  default     = ""
}

variable "log_retention_days" {
  type    = number
  default = 7
}

variable "tags" {
  type    = map(string)
  default = {}
}
