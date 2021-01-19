FROM mcr.microsoft.com/dotnet/core/sdk:5.0-buster-slim-arm64v8 AS build
WORKDIR /app

# publish app
COPY src .
WORKDIR /app/RockchipGpioDriver.Samples
RUN dotnet restore
RUN dotnet publish -c release -r linux-arm64 -o out

## run app
FROM mcr.microsoft.com/dotnet/core/runtime:5.0-buster-slim-arm64v8 AS runtime
WORKDIR /app
COPY --from=build /app/RockchipGpioDriver.Samples/out ./

ENTRYPOINT ["dotnet", "RockchipGpioDriver.Samples.dll"]