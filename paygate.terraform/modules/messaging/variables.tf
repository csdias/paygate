variable "name" {
  type = string
}

variable "message_retention_seconds" {
  description = "How long SQS retains unconsumed messages"
  type        = number
  default     = 1209600 # 14 days
}

variable "dlq_max_receive_count" {
  description = "Number of failed receive attempts before a message moves to the DLQ"
  type        = number
  default     = 3
}

variable "visibility_timeout_seconds" {
  description = "SQS visibility timeout — set >= Lambda timeout"
  type        = number
  default     = 30
}

variable "tags" {
  type    = map(string)
  default = {}
}
