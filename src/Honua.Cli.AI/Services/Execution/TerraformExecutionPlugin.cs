// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.Services.Execution;

public class TerraformExecutionPlugin
{
    private readonly IPluginExecutionContext _context;

    public TerraformExecutionPlugin(IPluginExecutionContext context)
    {
        _context = context;
    }

    [KernelFunction, Description("Generate Terraform configuration for cloud infrastructure")]
    public async Task<string> GenerateTerraformConfig(
        [Description("Cloud provider: aws, azure, gcp")] string provider,
        [Description("Infrastructure specification as JSON")] string specification,
        [Description("Output directory relative to workspace")] string outputDir = "terraform")
    {
        // Validate path FIRST to prevent path traversal attacks
        try
        {
            CommandArgumentValidator.ValidatePath(outputDir, nameof(outputDir));
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Terraform", "GenerateConfig", $"Path validation failed: {ex.Message}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }

        var fullPath = Path.Combine(_context.WorkspacePath, outputDir);

        // Additional security check: Ensure the resolved path is within the workspace
        var normalizedFullPath = Path.GetFullPath(fullPath);
        var normalizedWorkspace = Path.GetFullPath(_context.WorkspacePath);

        if (!normalizedFullPath.StartsWith(normalizedWorkspace, StringComparison.OrdinalIgnoreCase))
        {
            var errorMsg = $"Path traversal detected: outputDir '{outputDir}' resolves outside workspace boundary";
            _context.RecordAction("Terraform", "GenerateConfig", errorMsg, false, errorMsg);
            return JsonSerializer.Serialize(new { success = false, error = errorMsg });
        }

        var spec = JsonSerializer.Deserialize<JsonElement>(specification);

        var tfConfig = provider.ToLower() switch
        {
            "aws" => GenerateAWSTerraform(spec),
            "azure" => GenerateAzureTerraform(spec),
            "gcp" => GenerateGCPTerraform(spec),
            _ => throw new ArgumentException($"Unsupported provider: {provider}")
        };

        if (_context.DryRun)
        {
            _context.RecordAction("Terraform", "GenerateConfig", $"[DRY-RUN] Would generate {provider} config in {fullPath}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, provider, outputDir, config = tfConfig });
        }

        if (_context.RequireApproval)
        {
            var approved = await _context.RequestApprovalAsync(
                "Generate Terraform Config",
                $"Generate {provider} infrastructure config in {outputDir}",
                new[] { fullPath });

            if (!approved)
            {
                _context.RecordAction("Terraform", "GenerateConfig", "User rejected Terraform config generation", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            Directory.CreateDirectory(fullPath);
            var mainPath = Path.Combine(fullPath, "main.tf");
            await File.WriteAllTextAsync(mainPath, tfConfig);

            // Generate variables.tf file
            var variablesPath = Path.Combine(fullPath, "variables.tf");
            var variablesDef = GenerateVariablesDefinition(provider);
            await File.WriteAllTextAsync(variablesPath, variablesDef);

            // Generate terraform.tfvars file with placeholder values (should be replaced with real values)
            var tfvarsPath = Path.Combine(fullPath, "terraform.tfvars");
            var tfvars = GenerateTfvarsTemplate(provider);
            await File.WriteAllTextAsync(tfvarsPath, tfvars);

            // Add .tfvars to .gitignore to prevent secrets from being committed
            var gitignorePath = Path.Combine(fullPath, ".gitignore");
            if (!File.Exists(gitignorePath))
            {
                await File.WriteAllTextAsync(gitignorePath, "*.tfvars\n.terraform/\n*.tfstate\n*.tfstate.backup\n");
            }

            _context.RecordAction("Terraform", "GenerateConfig", $"Generated {provider} config with variables.tf and terraform.tfvars in {fullPath}", true);

            return JsonSerializer.Serialize(new { success = true, provider, outputDir, path = mainPath, variablesPath, tfvarsPath });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Terraform", "GenerateConfig", $"Failed to generate config", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [KernelFunction, Description("Execute terraform init")]
    public async Task<string> TerraformInit(
        [Description("Path to Terraform directory relative to workspace")] string terraformDir = "terraform")
    {
        // Validate path FIRST to prevent path traversal and command injection
        try
        {
            CommandArgumentValidator.ValidatePath(terraformDir, nameof(terraformDir));
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Terraform", "Init", $"Validation failed: {ex.Message}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }

        var fullPath = Path.Combine(_context.WorkspacePath, terraformDir);

        if (_context.DryRun)
        {
            _context.RecordAction("Terraform", "Init", $"[DRY-RUN] Would run terraform init in {fullPath}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, terraformDir, fullPath });
        }

        if (_context.RequireApproval)
        {
            var approved = await _context.RequestApprovalAsync(
                "Terraform Init",
                $"Initialize Terraform in {terraformDir}",
                new[] { fullPath });

            if (!approved)
            {
                _context.RecordAction("Terraform", "Init", "User rejected terraform init", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            var output = await ExecuteTerraformCommandAsync(fullPath, "init");
            _context.RecordAction("Terraform", "Init", $"Initialized Terraform in {terraformDir}", true);

            return JsonSerializer.Serialize(new { success = true, output, terraformDir });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Terraform", "Init", $"Failed terraform init", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [KernelFunction, Description("Execute terraform plan")]
    public async Task<string> TerraformPlan(
        [Description("Path to Terraform directory relative to workspace")] string terraformDir = "terraform")
    {
        // Validate path FIRST to prevent path traversal and command injection
        try
        {
            CommandArgumentValidator.ValidatePath(terraformDir, nameof(terraformDir));
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Terraform", "Plan", $"Validation failed: {ex.Message}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }

        var fullPath = Path.Combine(_context.WorkspacePath, terraformDir);

        if (_context.DryRun)
        {
            _context.RecordAction("Terraform", "Plan", $"[DRY-RUN] Would run terraform plan in {fullPath}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, terraformDir, fullPath });
        }

        try
        {
            var output = await ExecuteTerraformCommandAsync(fullPath, "plan", "-out=tfplan");
            _context.RecordAction("Terraform", "Plan", $"Generated Terraform plan in {terraformDir}", true);

            return JsonSerializer.Serialize(new { success = true, output, terraformDir, planFile = "tfplan" });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Terraform", "Plan", $"Failed terraform plan", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [KernelFunction, Description("Execute terraform apply")]
    public async Task<string> TerraformApply(
        [Description("Path to Terraform directory relative to workspace")] string terraformDir = "terraform",
        [Description("Auto-approve the apply (dangerous)")] bool autoApprove = false)
    {
        // Validate path FIRST to prevent path traversal and command injection
        try
        {
            CommandArgumentValidator.ValidatePath(terraformDir, nameof(terraformDir));
        }
        catch (ArgumentException ex)
        {
            _context.RecordAction("Terraform", "Apply", $"Validation failed: {ex.Message}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }

        var fullPath = Path.Combine(_context.WorkspacePath, terraformDir);

        if (_context.DryRun)
        {
            _context.RecordAction("Terraform", "Apply", $"[DRY-RUN] Would run terraform apply in {fullPath}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, terraformDir, fullPath, autoApprove });
        }

        if (_context.RequireApproval || !autoApprove)
        {
            var approved = await _context.RequestApprovalAsync(
                "Terraform Apply",
                $"Apply Terraform changes in {terraformDir}\n⚠️ This will create/modify/destroy cloud resources!",
                new[] { fullPath });

            if (!approved)
            {
                _context.RecordAction("Terraform", "Apply", "User rejected terraform apply", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            var args = autoApprove ? new[] { "apply", "-auto-approve", "tfplan" } : new[] { "apply", "tfplan" };
            var output = await ExecuteTerraformCommandAsync(fullPath, args);
            _context.RecordAction("Terraform", "Apply", $"Applied Terraform changes in {terraformDir}", true);

            return JsonSerializer.Serialize(new { success = true, output, terraformDir });
        }
        catch (Exception ex)
        {
            _context.RecordAction("Terraform", "Apply", $"Failed terraform apply", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private string GenerateAWSTerraform(JsonElement spec)
    {
        return @"terraform {
  required_providers {
    aws = {
      source  = ""hashicorp/aws""
      version = ""~> 5.0""
    }
  }
}

provider ""aws"" {
  region = var.aws_region
}

resource ""aws_instance"" ""honua_server"" {
  ami           = ""ami-0c55b159cbfafe1f0""
  instance_type = ""t3.medium""

  tags = {
    Name = ""honua-gis-server""
  }
}

resource ""aws_db_instance"" ""postgis"" {
  identifier           = ""honua-postgis""
  engine              = ""postgres""
  engine_version      = ""15.4""
  instance_class      = ""db.t3.micro""
  allocated_storage   = 20
  db_name             = ""honua""
  username            = var.db_username
  password            = var.db_password
  skip_final_snapshot = true
}";
    }

    private string GenerateAzureTerraform(JsonElement spec)
    {
        return @"terraform {
  required_providers {
    azurerm = {
      source  = ""hashicorp/azurerm""
      version = ""~> 3.0""
    }
  }
}

provider ""azurerm"" {
  features {}
}

resource ""azurerm_resource_group"" ""honua"" {
  name     = ""honua-resources""
  location = ""East US""
}

resource ""azurerm_postgresql_server"" ""postgis"" {
  name                = ""honua-postgis""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  sku_name           = ""B_Gen5_1""
  version            = ""11""

  administrator_login          = var.db_username
  administrator_login_password = var.db_password
}";
    }

    private string GenerateGCPTerraform(JsonElement spec)
    {
        return @"terraform {
  required_providers {
    google = {
      source  = ""hashicorp/google""
      version = ""~> 5.0""
    }
  }
}

provider ""google"" {
  project = ""your-project-id""
  region  = ""us-central1""
}

resource ""google_compute_instance"" ""honua_server"" {
  name         = ""honua-gis-server""
  machine_type = ""e2-medium""
  zone         = ""us-central1-a""

  boot_disk {
    initialize_params {
      image = ""debian-cloud/debian-11""
    }
  }

  network_interface {
    network = ""default""
    access_config {}
  }
}

resource ""google_sql_database_instance"" ""postgis"" {
  name             = ""honua-postgis""
  database_version = ""POSTGRES_15""
  region           = ""us-central1""

  settings {
    tier = ""db-f1-micro""
  }
}";
    }

    private string GenerateVariablesDefinition(string provider)
    {
        return provider.ToLower() switch
        {
            "aws" => @"variable ""aws_region"" {
  description = ""AWS region for resources""
  type        = string
  default     = ""us-east-1""
}

variable ""db_username"" {
  description = ""Database administrator username""
  type        = string
  sensitive   = true
}

variable ""db_password"" {
  description = ""Database administrator password""
  type        = string
  sensitive   = true
}",
            "azure" => @"variable ""db_username"" {
  description = ""Database administrator username""
  type        = string
  sensitive   = true
}

variable ""db_password"" {
  description = ""Database administrator password""
  type        = string
  sensitive   = true
}",
            _ => @"# Define your variables here
variable ""db_username"" {
  description = ""Database administrator username""
  type        = string
  sensitive   = true
}

variable ""db_password"" {
  description = ""Database administrator password""
  type        = string
  sensitive   = true
}"
        };
    }

    private string GenerateTfvarsTemplate(string provider)
    {
        return provider.ToLower() switch
        {
            "aws" => @"# AWS Configuration
# WARNING: This file contains sensitive values and should NOT be committed to version control
# It is automatically added to .gitignore

aws_region  = ""us-east-1""
db_username = ""postgres""
db_password = ""CHANGEME_REPLACE_WITH_SECURE_PASSWORD""
",
            "azure" => @"# Azure Configuration
# WARNING: This file contains sensitive values and should NOT be committed to version control
# It is automatically added to .gitignore

db_username = ""postgres""
db_password = ""CHANGEME_REPLACE_WITH_SECURE_PASSWORD""
",
            _ => @"# Terraform Configuration
# WARNING: This file contains sensitive values and should NOT be committed to version control
# It is automatically added to .gitignore

db_username = ""postgres""
db_password = ""CHANGEME_REPLACE_WITH_SECURE_PASSWORD""
"
        };
    }

    /// <summary>
    /// Execute terraform command safely (preventing command injection)
    /// </summary>
    private async Task<string> ExecuteTerraformCommandAsync(string workingDirectory, params string[] arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "terraform",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Use ArgumentList to prevent command injection
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start terraform process");

        // Read stdout and stderr concurrently to prevent deadlock
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await Task.WhenAll(stdoutTask, stderrTask);

        var output = await stdoutTask;
        var error = await stderrTask;

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Terraform command failed: {error}");

        return output;
    }
}
