# 1단계: 빌드 환경 (Microsoft 공식 .NET SDK 이미지 사용)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 프로젝트 파일 복사 및 복원
COPY ["Bot/Bot.csproj", "Bot/"]
RUN dotnet restore "Bot/Bot.csproj"

# 전체 소스코드 복사 및 빌드 (Release 모드로 out 폴더에 퍼블리시)
COPY . .
WORKDIR "/src/Bot"
RUN dotnet publish -c Release -o /app/publish

# 2단계: 실행 환경 (가벼운 .NET 런타임 이미지 사용)
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# 봇 실행 명령어 (.dll 파일명은 본인 프로젝트에 맞게 수정 가능)
ENTRYPOINT ["dotnet", "Bot.dll"]