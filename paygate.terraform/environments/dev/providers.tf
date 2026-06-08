provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "paygate"
      Environment = "dev"
      ManagedBy   = "terraform"
    }
  }
}
