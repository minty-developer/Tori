public class APIKeyMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // /api 로 시작하는 요청에 대해서만 API 키 검증
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            // 1. Header에서 x-api-key 추출
            if (!context.Request.Headers.TryGetValue("x-api-key", out var extractedApiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "API 키가 누락되었습니다." });
                return;
            }

            var validApiKey = Environment.GetEnvironmentVariable("TORI_API_KEY");

            Console.WriteLine(
                $"TORI_API_KEY loaded: {!string.IsNullOrEmpty(validApiKey)}, length: {validApiKey?.Length ?? 0}"
            );

            // 3. 키 일치 여부 검증
            if (string.IsNullOrEmpty(validApiKey) || !validApiKey.Equals(extractedApiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "유효하지 않은 API 키입니다." });
                return;
            }
        }

        await _next(context);
    }
}

// 편하게 등록하기 위한 확장 메서드
public static class APIKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseAPIKeyMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<APIKeyMiddleware>();
    }
}