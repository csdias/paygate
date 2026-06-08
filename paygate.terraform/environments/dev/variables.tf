variable "aws_region" {
  type    = string
  default = "eu-west-1"
}

variable "vpc_id" {
  description = "Existing VPC ID"
  type        = string
}

variable "private_subnet_ids" {
  description = "Private subnet IDs (for ECS tasks and RDS)"
  type        = list(string)
}

variable "public_subnet_ids" {
  description = "Public subnet IDs (for the ALB)"
  type        = list(string)
}

variable "outbox_publisher_image_tag" {
  type    = string
  default = "latest"
}

variable "payment_api_image_tag" {
  type    = string
  default = "latest"
}

variable "codestar_connection_arn" {
  description = "ARN of the CodeStar GitHub connection — create once via AWS Console > CodePipeline > Connections"
  type        = string
}

variable "github_owner" {
  description = "GitHub username or organization that owns the paygate.* repos"
  type        = string
}
