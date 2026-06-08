variable "name" {
  type = string
}

variable "vpc_id" {
  type = string
}

variable "private_subnet_ids" {
  description = "Private subnets for the ECS tasks"
  type        = list(string)
}

variable "public_subnet_ids" {
  description = "Public subnets for the ALB"
  type        = list(string)
}

variable "ecs_cluster_id" {
  type = string
}

variable "image_tag" {
  type    = string
  default = "latest"
}

variable "db_secret_arn" {
  type = string
}

variable "desired_count" {
  type    = number
  default = 1
}

variable "cpu" {
  type    = number
  default = 512
}

variable "memory" {
  type    = number
  default = 1024
}

variable "container_port" {
  type    = number
  default = 8080
}

variable "db_security_group_id" {
  type = string
}

variable "health_check_path" {
  type    = string
  default = "/health"
}

variable "log_retention_days" {
  type    = number
  default = 7
}

variable "tags" {
  type    = map(string)
  default = {}
}
