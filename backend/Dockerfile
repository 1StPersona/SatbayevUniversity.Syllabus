# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY SatbayevUniversity.Syllabus.sln ./
COPY src/SatbayevUniversity.Syllabus.Domain/*.csproj src/SatbayevUniversity.Syllabus.Domain/
COPY src/SatbayevUniversity.Syllabus.Infrastructure/*.csproj src/SatbayevUniversity.Syllabus.Infrastructure/
COPY src/SatbayevUniversity.Syllabus.Api/*.csproj src/SatbayevUniversity.Syllabus.Api/
# Необязательный секрет corp_ca — PEM-бандл корпоративного прокси с TLS-инспекцией:
#   docker build --secret id=corp_ca,src=/path/ca-bundle.crt .
RUN --mount=type=secret,id=corp_ca,required=false \
    if [ -f /run/secrets/corp_ca ]; then export SSL_CERT_FILE=/run/secrets/corp_ca; fi; \
    dotnet restore src/SatbayevUniversity.Syllabus.Api/SatbayevUniversity.Syllabus.Api.csproj
COPY src/ src/
RUN dotnet publish src/SatbayevUniversity.Syllabus.Api/SatbayevUniversity.Syllabus.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=Asia/Almaty
COPY --from=build /app .
RUN mkdir -p /app/App_Data/umkd /app/logs && chown -R app:app /app/App_Data /app/logs
USER app
EXPOSE 8080
# Проверка /health без curl (в образе только bash): ответ должен содержать "status":"ok"
HEALTHCHECK --interval=15s --timeout=5s --start-period=40s --retries=5 \
  CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && grep -q "\"status\":\"ok\"" <&3' || exit 1
ENTRYPOINT ["dotnet", "SatbayevUniversity.Syllabus.Api.dll"]
