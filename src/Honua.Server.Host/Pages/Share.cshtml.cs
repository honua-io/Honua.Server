// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Honua.Server.Core.Services.Sharing;
using Honua.Server.Core.Models;

namespace Honua.Server.Host.Pages;

public class ShareModel : PageModel
{
    private readonly ShareService _shareService;
    private readonly ILogger<ShareModel> _logger;

    public ShareModel(ShareService shareService, ILogger<ShareModel> logger)
    {
        _shareService = shareService;
        _logger = logger;
    }

    public string Token { get; set; } = string.Empty;
    public ShareToken? ShareToken { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RequiresPassword { get; set; }
    public bool CanComment { get; set; }

    public async Task<IActionResult> OnGetAsync(string token, string? password = null)
    {
        Token = token;

        var (isValid, shareToken, error) = await _shareService.ValidateShareAsync(token, password);

        if (!isValid)
        {
            if (error == "Password required")
            {
                RequiresPassword = true;
                ShareToken = shareToken;
                return Page();
            }

            ErrorMessage = error ?? "This share link is invalid or has expired.";
            _logger.LogWarning("Invalid share token access attempt: {Token}, Error: {Error}", token, error);
            return Page();
        }

        ShareToken = shareToken;
        CanComment = shareToken?.Permission == SharePermission.Comment ||
                     shareToken?.Permission == SharePermission.Edit;

        _logger.LogInformation("Share token {Token} accessed for map {MapId}", token, shareToken?.MapId);

        return Page();
    }
}
