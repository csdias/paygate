variable "name" {
  type = string
}

variable "price_class" {
  description = "CloudFront price class (PriceClass_100 = US/EU only, cheapest)"
  type        = string
  default     = "PriceClass_100"
}

variable "tags" {
  type    = map(string)
  default = {}
}
