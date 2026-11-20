using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    
    // 설정 값: 10초 동안 최대 10번 요청 허용
    private readonly int _maxRequests = 10;
    private readonly TimeSpan _timeWindow = TimeSpan.FromSeconds(10);

    public RateLimitMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 클라이언트 IP 가져오기 (프록시 환경인 경우 X-Forwarded-For 고려 가능)
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        string cacheKey = $"rate_limit_{ipAddress}";

        // 캐시에서 현재 요청 횟수 가져오기 (없으면 0으로 초기화 후 _timeWindow 동안 유지)
        var requestCount = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _timeWindow;
            return 0;
        });

        if (requestCount >= _maxRequests)
        {
            // 429 상태 코드 및 JSON 응답 반환
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                status = 429,
                message = "요청 횟수가 너무 많습니다. 잠시 후 다시 시도해 주세요."
            };

            await context.Response.WriteAsJsonAsync(errorResponse);
            return;
        }

        // 요청 횟수 1 증가 후 저장
        _cache.Set(cacheKey, requestCount + 1, _timeWindow);

        await _next(context);
    }
}

// 편하게 등록하기 위한 확장 메서드
public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiterMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitMiddleware>();
    }
}