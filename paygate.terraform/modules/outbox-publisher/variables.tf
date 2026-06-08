variable "name" {
  type = string
}

variable "vpc_id" {
  type = string
}

variable "subnet_ids" {
  description = "Private subnets for the ECS tasks"
  type        = list(string)
}

variable "ecs_cluster_id" {
  type = string
}

variable "image_tag" {
  description = "Docker image tag to deploy"
  type        = string
  default     = "latest"
}

variable "db_secret_arn" {
  description = "ARN of the Secrets Manager secret containing DB credentials"
  type        = string
}

variable "sns_topic_arn" {
  description = "ARN of the SNS topic to publish to"
  type        = string
}

variable "desired_count" {
  description = "Number of ECS tasks to run"
  type        = number
  default     = 1
}

variable "cpu" {
  type    = number
  default = 256
}

variable "memory" {
  type    = number
  default = 512
}

variable "db_security_group_id" {
  description = "Security group ID of the RDS instance (to allow egress)"
  type        = string
}

variable "log_retention_days" {
  type    = number
  default = 7
}

variable "tags" {
  type    = map(string)
  default = {}
}
