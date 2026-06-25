# Estágio 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Desabilita download de docs XML do NuGet (mais rápido)
ENV NUGET_XMLDOC_MODE=skip

# Copia os arquivos de projeto para restaurar dependências antes do código (cache de layers)
COPY ["Versatus.ForcaVendas.Api/Versatus.ForcaVendas.Api.csproj", "Versatus.ForcaVendas.Api/"]
COPY ["Versatus.ForcaVendas.Application/Versatus.ForcaVendas.Application.csproj", "Versatus.ForcaVendas.Application/"]
COPY ["Versatus.ForcaVendas.Domain/Versatus.ForcaVendas.Domain.csproj", "Versatus.ForcaVendas.Domain/"]
COPY ["Versatus.ForcaVendas.Infrastructure/Versatus.ForcaVendas.Infrastructure.csproj", "Versatus.ForcaVendas.Infrastructure/"]

RUN dotnet restore "Versatus.ForcaVendas.Api/Versatus.ForcaVendas.Api.csproj" --runtime linux-musl-x64

# Copia o código-fonte e publica
COPY . .
WORKDIR "/src/Versatus.ForcaVendas.Api"
RUN dotnet publish "Versatus.ForcaVendas.Api.csproj" -c Release -r linux-musl-x64 --self-contained false -o /app/publish

# Estágio 2: Runtime (imagem menor, sem o SDK)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Instala dependências necessárias para BCrypt e cultura pt-BR
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

COPY --from=build /app/publish .

EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000

ENTRYPOINT ["dotnet", "Versatus.ForcaVendas.Api.dll"]
