FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src
COPY . .
RUN dotnet publish TeamBuilder/TeamBuilder.csproj --configuration Release --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
COPY --from=build /app/publish .
RUN .playwright/node/linux-x64/node .playwright/package/cli.js install --with-deps --only-shell chromium \
    && chmod -R a+rX /ms-playwright \
    && rm -rf /var/lib/apt/lists/*
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TeamBuilder.dll"]