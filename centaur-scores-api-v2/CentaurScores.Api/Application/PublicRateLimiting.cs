using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CentaurScores.Api.Application;

/// <summary>
/// Per-caller rate limiting for anonymous endpoints that are expensive to answer (published results - a competition
/// recomputes every round's ranking). Each client IP gets a token bucket: a few quick refreshes are served immediately,
/// then requests in a burst are queued and released one per replenishment period, so every further request waits
/// progressively longer (5s, 10s, 15s, ...). Once the queue is full the caller gets 429 with a Retry-After header.
/// </summary>
public static class PublicRateLimiting
{
    public const string PublishedResultsPolicy = "published-results";

    // Burst allowance: this many requests are answered without any delay.
    public const int TokenLimit = 6;
    // Afterwards one request is let through per period.
    public static readonly TimeSpan ReplenishmentPeriod = TimeSpan.FromSeconds(5);
    // Requests beyond the bucket wait in line (each one a period later than the previous); beyond this many, 429.
    public const int QueueLimit = 6;

    public static IServiceCollection AddPublicRateLimiting(this IServiceCollection services) => services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = (context, _) =>
        {
            var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value) ? value : ReplenishmentPeriod * QueueLimit;
            context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            return ValueTask.CompletedTask;
        };
        options.AddPolicy(PublishedResultsPolicy, httpContext => RateLimitPartition.GetTokenBucketLimiter(
            ClientAddress(httpContext.Connection.RemoteIpAddress, httpContext.Request.Headers["X-Forwarded-For"].ToString()),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = TokenLimit,
                TokensPerPeriod = 1,
                ReplenishmentPeriod = ReplenishmentPeriod,
                QueueLimit = QueueLimit,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
    });

    /// <summary>
    /// The caller to rate-limit. Behind the nginx gateway every request comes from the proxy, so the proxy's appended
    /// X-Forwarded-For entry (the rightmost one - the address nginx itself saw) identifies the real client. The header
    /// is only trusted when the direct peer is a loopback/private address, i.e. our own proxy; anything a client puts
    /// earlier in the header is ignored, so it cannot be used to dodge the limit.
    /// </summary>
    public static string ClientAddress(IPAddress? remoteAddress, string? forwardedFor)
    {
        if (remoteAddress is not null && IsInternal(remoteAddress) && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            var last = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (last is not null && IPAddress.TryParse(last, out var forwarded)) return Normalize(forwarded);
        }
        return remoteAddress is null ? "unknown" : Normalize(remoteAddress);
    }

    private static string Normalize(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        // IPv6 clients can trivially rotate within their /64, so they share one bucket per /64.
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return new IPAddress(bytes) + "/64";
        }
        return address.ToString();
    }

    private static bool IsInternal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168);
        }
        // fc00::/7 unique local addresses
        return address.AddressFamily == AddressFamily.InterNetworkV6 && (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
    }
}
