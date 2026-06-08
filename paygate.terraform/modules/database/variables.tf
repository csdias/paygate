variable "name" {
  description = "Resource name prefix (e.g. paygate-dev)"
  type        = string
}

variable "vpc_id" {
  description = "VPC to place the RDS instance in"
  type        = string
}

variable "subnet_ids" {
  description = "Private subnet IDs for the DB subnet group"
  type        = list(string)
}

variable "allowed_security_group_ids" {
  description = "Security groups allowed to connect on port 5432"
  type        = list(string)
  default     = []
}

variable "db_name" {
  description = "Name of the initial Postgres database"
  type        = string
  default     = "paygate"
}

variable "db_username" {
  description = "Master DB username"
  type        = string
  default     = "paygate"
}

variable "instance_class" {
  description = "RDS instance class"
  type        = string
  default     = "db.t3.micro"
}

variable "allocated_storage_gb" {
  description = "Allocated storage in GiB"
  type        = number
  default     = 20
}

variable "deletion_protection" {
  description = "Enable deletion protection on the RDS instance"
  type        = bool
  default     = false
}

variable "tags" {
  type    = map(string)
  default = {}
}
