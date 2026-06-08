resource "random_password" "db" {
  length  = 32
  special = false
}

resource "aws_secretsmanager_secret" "db_credentials" {
  name                    = "${var.name}/rds/credentials"
  recovery_window_in_days = 0
  tags                    = var.tags
}

resource "aws_secretsmanager_secret_version" "db_credentials" {
  secret_id = aws_secretsmanager_secret.db_credentials.id
  secret_string = jsonencode({
    username = var.db_username
    password = random_password.db.result
    host     = aws_db_instance.this.address
    port     = aws_db_instance.this.port
    dbname   = var.db_name
  })
}

resource "aws_security_group" "db" {
  name        = "${var.name}-rds"
  description = "Allow Postgres ingress from application layer"
  vpc_id      = var.vpc_id
  tags        = merge(var.tags, { Name = "${var.name}-rds" })

  ingress {
    description     = "Postgres from app"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = var.allowed_security_group_ids
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_db_subnet_group" "this" {
  name       = "${var.name}-rds"
  subnet_ids = var.subnet_ids
  tags       = var.tags
}

resource "aws_db_parameter_group" "this" {
  name   = "${var.name}-pg15"
  family = "postgres15"
  tags   = var.tags

  parameter {
    name  = "log_connections"
    value = "1"
  }
}

resource "aws_db_instance" "this" {
  identifier        = var.name
  engine            = "postgres"
  engine_version    = "15"
  instance_class    = var.instance_class
  allocated_storage = var.allocated_storage_gb
  storage_encrypted = true

  db_name  = var.db_name
  username = var.db_username
  password = random_password.db.result

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [aws_security_group.db.id]
  parameter_group_name   = aws_db_parameter_group.this.name

  backup_retention_period = 7
  skip_final_snapshot     = true
  deletion_protection     = var.deletion_protection

  tags = merge(var.tags, { Name = var.name })
}
