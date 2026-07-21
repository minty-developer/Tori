# 1단계: 빌드 환경 (.NET 10 SDK 사용)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 프로젝트 파일 복사 및 패키지 복원
COPY ["Bot/Bot.csproj", "Bot/"]
RUN dotnet restore "Bot/Bot.csproj"

# 나머지 소스코드 복사 후 Release 모드로 빌드
COPY . .
WORKDIR "/src/Bot"
RUN dotnet publish -c Release -o /app/publish

# 2단계: 실행 환경 (ASP.NET 10 런타임 사용 - 웹/앱 공통 프레임워크 포함)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# 봇 실행
ENTRYPOINT ["dotnet", "Bot.dll"]