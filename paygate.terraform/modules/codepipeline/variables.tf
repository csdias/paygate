variable "name" {
  description = "Pipeline name — used as a prefix for all resources"
  type        = string
}

variable "pipeline_type" {
  description = "Type of pipeline: ecs, lambda, or frontend"
  type        = string
  validation {
    condition     = contains(["ecs", "lambda", "frontend"], var.pipeline_type)
    error_message = "pipeline_type must be one of: ecs, lambda, frontend."
  }
}

variable "codestar_connection_arn" {
  description = "ARN of the CodeStar GitHub connection (created once per account via AWS Console)"
  type        = string
}

variable "repository_id" {
  description = "GitHub repository in owner/name format, e.g. acme/paygate.api"
  type        = string
}

variable "branch" {
  description = "Branch that triggers the pipeline"
  type        = string
  default     = "main"
}

variable "artifact_bucket" {
  description = "S3 bucket name for CodePipeline inter-stage artifacts"
  type        = string
}

variable "tags" {
  type    = map(string)
  default = {}
}

# ── ECS-specific (required when pipeline_type = "ecs") ───────────────────────

variable "ecs_config" {
  description = "Required for pipeline_type = ecs"
  type = object({
    ecr_repository_url = string
    ecs_cluster_name   = string
    ecs_service_name   = string
    # Path to the build context directory, relative to the repo root
    build_context    = string
    # Path to the Dockerfile, relative to the repo root
    dockerfile_path  = string
  })
  default = null
}

# ── Lambda-specific (required when pipeline_type = "lambda") ─────────────────

variable "lambda_config" {
  description = "Required for pipeline_type = lambda"
  type = object({
    function_name = string
    # S3 bucket where the ZIP is uploaded and where Lambda reads it
    s3_bucket    = string
    s3_key       = string
    # Path to the .NET project directory, relative to the repo root
    project_path = string
  })
  default = null
}

# ── Frontend-specific (required when pipeline_type = "frontend") ─────────────

variable "frontend_config" {
  description = "Required for pipeline_type = frontend"
  type = object({
    s3_bucket                  = string
    cloudfront_distribution_id = string
    # Directory that npm run build writes to, e.g. "dist" or "build"
    build_output_dir = string
  })
  default = null
}
