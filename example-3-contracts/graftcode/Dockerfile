# Documented host path for the method-signature sample. There is no
# Gateway image to docker pull. Base image and gg install match the
# .NET MCP quick start.
# https://docs.graftcode.com/quick-start/expose-mcp/dotnet
#
# The REST and gRPC folders are not copied into this image.
# --types and --methods select the callable surface. They are not authorization.
FROM mcr.microsoft.com/dotnet/sdk:9.0

WORKDIR /usr/app

COPY . /usr/app/

RUN dotnet publish ./Billing.Graftcode.csproj -c Release -o /usr/app/publish

RUN curl -fsSL grft.dev/get/gg | sh && dpkg -i gg.deb && rm gg.deb

EXPOSE 80
EXPOSE 81

CMD ["gg", "/usr/app/publish/Billing.Graftcode.dll", "--types", "Billing.Graftcode.BillingService", "--methods", "CalculateMonthlyBill", "--mcpBaseClass", "Billing.Graftcode.BillingService"]
