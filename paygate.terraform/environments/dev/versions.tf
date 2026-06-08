terraform {
  required_version = ">= 1.9"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Uncomment when you have an S3 remote state bucket
  # backend "s3" {
  #   bucket = "paygate-terraform-state"
  #   key    = "dev/terraform.tfstate"
  #   region = "eu-west-1"
  # }
}
