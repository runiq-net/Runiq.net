using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Runiq.AI.Core.Dashboard;
using Runiq.AI.Core.Studio;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Runiq.AI.Core;

/// <summary>
/// Runiq Dashboard'u host uygulama içinde yayınlayan extension metodlarını içerir.
/// </summary>
public static class RuniqDashboardApplicationBuilderExtensions
{
    /// <summary>
    /// Runiq Dashboard'u varsayılan ayarlarla yayınlar.
    /// </summary>
    public static IApplicationBuilder UseRuniqDashboard(this IApplicationBuilder app)
    {
        return app.UseRuniqDashboard(_ => { });
    }

    /// <summary>
    /// Runiq Dashboard'u verilen ayarlarla yayınlar.
    /// </summary>
    public static IApplicationBuilder UseRuniqDashboard(
        this IApplicationBuilder app,
        Action<RuniqDashboardOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RuniqDashboardOptions();
        configure(options);
        options.ValidateAuthentication();

        var basePath = NormalizePath(options.Path);
        var authenticationOptions = options.AuthenticationOptions;

        app.UseRouting();

        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(basePath))
            {
                await next();
                return;
            }

            if (HasDashboardAccess(context.User, authenticationOptions))
            {
                await next();
                return;
            }

            await RejectDashboardAccessAsync(context);
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGroup(basePath).MapControllers();
        });

        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals(basePath, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect(basePath + "/");
                return;
            }

            await next();
        });

        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(basePath, out var remainingPath))
            {
                await next();
                return;
            }

            var relativePath = remainingPath.Value ?? string.Empty;

            if (relativePath.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("/metadata/", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync(
                    "Runiq endpoint was not found.",
                    context.RequestAborted);

                return;
            }

            var requestPath = context.Request.Path.Value ?? string.Empty;

            var isStaticAsset =
                Path.HasExtension(requestPath) ||
                requestPath.Contains("/assets/", StringComparison.OrdinalIgnoreCase);

            if (isStaticAsset)
            {
                var assetRelativePath = relativePath.TrimStart('/');

                var assetStream = RuniqDashboardAssets.OpenRead(assetRelativePath);

                if (assetStream is null)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    await context.Response.WriteAsync(
                        "Runiq Dashboard asset was not found.",
                        context.RequestAborted);

                    return;
                }

                await using (assetStream)
                {
                    context.Response.ContentType = GetContentType(assetRelativePath);

                    await assetStream.CopyToAsync(
                        context.Response.Body,
                        context.RequestAborted);
                }

                return;
            }

            var html = await RuniqDashboardAssets.ReadTextAsync(
                "index.html",
                context.RequestAborted);

            if (html is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync(
                    "Runiq Dashboard index.html not found.",
                    context.RequestAborted);

                return;
            }

            var encodedTitle = HtmlEncoder.Default.Encode(options.Title);

            var dashboardConfigJson = JsonSerializer.Serialize(new
            {
                basePath,
                title = options.Title,
                authentication = new
                {
                    accessMode = options.AuthenticationOptions.AccessMode.ToString(),
                    logoutPath = "/logout"
                }
            });

            html = html
                .Replace("__RUNIQ_TITLE_HTML__", encodedTitle, StringComparison.Ordinal)
                .Replace("__RUNIQ_DASHBOARD_CONFIG__", dashboardConfigJson, StringComparison.Ordinal)
                .Replace("src=\"./assets/", $"src=\"{basePath}/assets/", StringComparison.Ordinal)
                .Replace("href=\"./assets/", $"href=\"{basePath}/assets/", StringComparison.Ordinal);

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html, context.RequestAborted);
        });

        return app;
    }

    private static bool HasDashboardAccess(
        ClaimsPrincipal user,
        RuniqDashboardAuthenticationOptions authenticationOptions)
    {
        return authenticationOptions.AccessMode switch
        {
            RuniqDashboardAccessMode.Anonymous => true,
            RuniqDashboardAccessMode.AuthenticatedUser => IsAuthenticated(user),
            RuniqDashboardAccessMode.Role =>
                IsAuthenticated(user) &&
                authenticationOptions.Roles.Any(user.IsInRole),
            _ => false
        };
    }

    private static bool IsAuthenticated(ClaimsPrincipal user)
    {
        return user.Identity?.IsAuthenticated == true;
    }

    private static async Task RejectDashboardAccessAsync(HttpContext context)
    {
        if (IsAuthenticated(context.User))
        {
            if (await HasDefaultAuthenticationSchemeAsync(context))
            {
                await context.ForbidAsync();
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (await HasDefaultAuthenticationSchemeAsync(context))
        {
            await context.ChallengeAsync();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    }

    private static async Task<bool> HasDefaultAuthenticationSchemeAsync(HttpContext context)
    {
        var schemeProvider = context.RequestServices
            .GetService<IAuthenticationSchemeProvider>();

        if (schemeProvider is null)
        {
            return false;
        }

        return await schemeProvider.GetDefaultChallengeSchemeAsync() is not null ||
            await schemeProvider.GetDefaultAuthenticateSchemeAsync() is not null;
    }

    /// <summary>
    /// Dashboard asset dosyası için content type değerini belirler.
    /// </summary>
    /// <param name="relativePath">Asset göreli yoludur.</param>
    /// <returns>HTTP content type değeridir.</returns>
    private static string GetContentType(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);

        return extension.ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".mjs" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" => "application/json; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Dashboard path değerini canonical forma dönüştürür.
    /// </summary>
    /// <param name="path">Kullanıcı tarafından verilen dashboard path değeridir.</param>
    /// <returns>Normalize edilmiş dashboard path değeridir.</returns>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "Dashboard path cannot be empty.",
                nameof(path));
        }

        var normalized = path.Trim();

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        normalized = normalized.TrimEnd('/');

        if (normalized == "/")
        {
            throw new ArgumentException(
                "Dashboard root path '/' is not supported.",
                nameof(path));
        }

        return normalized;
    }
}

