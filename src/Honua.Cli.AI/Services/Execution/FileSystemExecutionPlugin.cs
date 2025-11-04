// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Services.Security;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Execution;

public class FileSystemExecutionPlugin
{
    private readonly IPluginExecutionContext _context;

    public FileSystemExecutionPlugin(IPluginExecutionContext context)
    {
        _context = context;
    }

    [KernelFunction, Description("Write content to a file")]
    public async Task<string> WriteFile(
        [Description("Relative path to file from workspace")] string path,
        [Description("Content to write to file")] string content,
        [Description("Description of what this file is for")] string description)
    {
        // Validate path to prevent path traversal attacks
        string fullPath;
        try
        {
            fullPath = PathTraversalValidator.ValidateAndResolvePath(_context.WorkspacePath, path);
        }
        catch (SecurityException ex)
        {
            _context.RecordAction("FileSystem", "WriteFile", $"Security violation: {ex.Message}", false);
            return JsonSerializer.Serialize(new { success = false, error = $"Path validation failed: {ex.Message}" });
        }
        
        if (_context.DryRun)
        {
            _context.RecordAction("FileSystem", "WriteFile", $"[DRY-RUN] Would write to {fullPath}: {description}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, path = fullPath, description });
        }

        if (_context.RequireApproval)
        {
            var approved = await _context.RequestApprovalAsync(
                "Write File",
                $"Write to {path}: {description}",
                new[] { fullPath });

            if (!approved)
            {
                _context.RecordAction("FileSystem", "WriteFile", $"User rejected write to {fullPath}", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (!directory.IsNullOrEmpty())
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(fullPath, content);
            _context.RecordAction("FileSystem", "WriteFile", $"Wrote to {fullPath}: {description}", true);
            
            return JsonSerializer.Serialize(new { success = true, path = fullPath, description, size = content.Length });
        }
        catch (Exception ex)
        {
            _context.RecordAction("FileSystem", "WriteFile", $"Failed to write {fullPath}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [KernelFunction, Description("Create a directory")]
    public async Task<string> CreateDirectory(
        [Description("Relative path to directory from workspace")] string path,
        [Description("Description of what this directory is for")] string description)
    {
        // Validate path to prevent path traversal attacks
        string fullPath;
        try
        {
            fullPath = PathTraversalValidator.ValidateAndResolvePath(_context.WorkspacePath, path);
        }
        catch (SecurityException ex)
        {
            _context.RecordAction("FileSystem", "CreateDirectory", $"Security violation: {ex.Message}", false);
            return JsonSerializer.Serialize(new { success = false, error = $"Path validation failed: {ex.Message}" });
        }

        if (_context.DryRun)
        {
            _context.RecordAction("FileSystem", "CreateDirectory", $"[DRY-RUN] Would create {fullPath}: {description}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, path = fullPath, description });
        }

        if (_context.RequireApproval)
        {
            var approved = await _context.RequestApprovalAsync(
                "Create Directory",
                $"Create directory {path}: {description}",
                new[] { fullPath });

            if (!approved)
            {
                _context.RecordAction("FileSystem", "CreateDirectory", $"User rejected directory creation {fullPath}", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            Directory.CreateDirectory(fullPath);
            _context.RecordAction("FileSystem", "CreateDirectory", $"Created {fullPath}: {description}", true);
            
            return JsonSerializer.Serialize(new { success = true, path = fullPath, description });
        }
        catch (Exception ex)
        {
            _context.RecordAction("FileSystem", "CreateDirectory", $"Failed to create {fullPath}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    [KernelFunction, Description("Delete a file")]
    public async Task<string> DeleteFile(
        [Description("Relative path to file from workspace")] string path,
        [Description("Reason for deletion")] string reason)
    {
        // Validate path to prevent path traversal attacks
        string fullPath;
        try
        {
            fullPath = PathTraversalValidator.ValidateAndResolvePath(_context.WorkspacePath, path);
        }
        catch (SecurityException ex)
        {
            _context.RecordAction("FileSystem", "DeleteFile", $"Security violation: {ex.Message}", false);
            return JsonSerializer.Serialize(new { success = false, error = $"Path validation failed: {ex.Message}" });
        }

        if (_context.DryRun)
        {
            _context.RecordAction("FileSystem", "DeleteFile", $"[DRY-RUN] Would delete {fullPath}: {reason}", true);
            return JsonSerializer.Serialize(new { success = true, dryRun = true, path = fullPath, reason });
        }

        if (_context.RequireApproval)
        {
            var approved = await _context.RequestApprovalAsync(
                "Delete File",
                $"Delete {path}: {reason}",
                new[] { fullPath });

            if (!approved)
            {
                _context.RecordAction("FileSystem", "DeleteFile", $"User rejected deletion of {fullPath}", false);
                return JsonSerializer.Serialize(new { success = false, reason = "User rejected approval" });
            }
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _context.RecordAction("FileSystem", "DeleteFile", $"Deleted {fullPath}: {reason}", true);
                return JsonSerializer.Serialize(new { success = true, path = fullPath, reason });
            }
            else
            {
                _context.RecordAction("FileSystem", "DeleteFile", $"File not found {fullPath}", false);
                return JsonSerializer.Serialize(new { success = false, error = "File not found" });
            }
        }
        catch (Exception ex)
        {
            _context.RecordAction("FileSystem", "DeleteFile", $"Failed to delete {fullPath}", false, ex.Message);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }
}
