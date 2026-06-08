output "ecr_repository_url" {
  value = aws_ecr_repository.this.repository_url
}

output "security_group_id" {
  value = aws_security_group.this.id
}

output "task_role_arn" {
  value = aws_iam_role.task.arn
}

output "ecs_service_name" {
  description = "ECS service name — used by CI/CD to trigger redeployment"
  value       = aws_ecs_service.this.name
}
