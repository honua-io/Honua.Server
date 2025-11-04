# Placeholder GCP Regional Stack Module
variable "region" { type = string }
variable "environment" { type = string }
variable "project_name" { type = string }
variable "unique_suffix" { type = string }
variable "is_primary" { type = bool }
variable "instance_count" { type = number }
variable "machine_type" { type = string }
variable "container_image" { type = string }
variable "container_port" { type = number }
variable "cpu_limit" { type = string }
variable "memory_limit" { type = string }
variable "db_tier" { type = string }
variable "db_version" { type = string }
variable "db_storage_size_gb" {
  type = number
  default = 100
}
variable "enable_ha" {
  type = bool
  default = false
}
variable "enable_backup" {
  type = bool
  default = true
}
variable "backup_retention_days" {
  type = number
  default = 30
}
variable "enable_read_replica" {
  type = bool
  default = false
}
variable "master_instance_name" {
  type = string
  default = null
}
variable "storage_class" {
  type = string
  default = "STANDARD"
}
variable "enable_versioning" {
  type = bool
  default = true
}
variable "source_bucket" {
  type = string
  default = null
}
variable "enable_redis" {
  type = bool
  default = true
}
variable "redis_tier" {
  type = string
  default = "STANDARD_HA"
}
variable "redis_memory_size_gb" {
  type = number
  default = 5
}
variable "enable_monitoring" {
  type = bool
  default = true
}
variable "enable_logging" {
  type = bool
  default = true
}
variable "tags" {
  type = map(string)
  default = {}
}

output "service_url" { value = "https://honua-${var.environment}-${var.region}-${var.unique_suffix}.a.run.app" }
output "backend_service" { value = "honua-${var.environment}-${var.region}-backend" }
output "db_instance_name" { value = "honua-${var.environment}-${var.region}-db" }
output "db_connection_name" { value = "${var.project_name}:${var.region}:honua-${var.environment}-${var.region}-db" }
output "storage_bucket_name" { value = "honua-${var.environment}-${var.region}-${var.unique_suffix}" }
output "storage_bucket_url" { value = "gs://honua-${var.environment}-${var.region}-${var.unique_suffix}" }
output "redis_host" { value = "10.0.0.3" }
