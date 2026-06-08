FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["OutboxPublisher/Pay.Message.Exchange.OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.csproj", "OutboxPublisher/Pay.Message.Exchange.OutboxPublisher/"]
COPY ["OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Db/Pay.Message.Exchange.OutboxPublisher.Db.csproj", "OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Db/"]
COPY ["OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Db.Postgres/Pay.Message.Exchange.OutboxPublisher.Db.Postgres.csproj", "OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Db.Postgres/"]
COPY ["OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Entities/Pay.Message.Exchange.OutboxPublisher.Entities.csproj", "OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Entities/"]
COPY ["OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Services/Pay.Message.Exchange.OutboxPublisher.Services.csproj", "OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Services/"]
COPY ["OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Sns/Pay.Message.Exchange.OutboxPublisher.Sns.csproj", "OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.Sns/"]
COPY ["OutboxClient/Pay.Message.Exchange.OutboxClient/Pay.Message.Exchange.OutboxClient.csproj", "OutboxClient/Pay.Message.Exchange.OutboxClient/"]
COPY ["OutboxClient/Pay.Message.Exchange.OutboxClient.Postgres/Pay.Message.Exchange.OutboxClient.Postgres.csproj", "OutboxClient/Pay.Message.Exchange.OutboxClient.Postgres/"]

RUN dotnet restore "OutboxPublisher/Pay.Message.Exchange.OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.csproj"

COPY . .

RUN dotnet publish "OutboxPublisher/Pay.Message.Exchange.OutboxPublisher/Pay.Message.Exchange.OutboxPublisher.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --no-create-home appuser
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Pay.Message.Exchange.OutboxPublisher.dll"]
