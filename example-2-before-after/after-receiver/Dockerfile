# Documented host path for the Receiver. There is no Gateway image to
# docker pull. Base image and gg install match the .NET MCP quick start.
# https://docs.graftcode.com/quick-start/expose-mcp/dotnet
# https://docs.graftcode.com/how-to-guides/deploy-gateway-with-docker
#
# --types and --methods select the callable surface. They are not authorization.
# The illustrative REST project is not copied into this image.
FROM mcr.microsoft.com/dotnet/sdk:9.0

WORKDIR /usr/app

COPY . /usr/app/

RUN dotnet publish ./Billing.Receiver.csproj -c Release -o /usr/app/publish

RUN curl -fsSL grft.dev/get/gg | sh && dpkg -i gg.deb && rm gg.deb

EXPOSE 80
EXPOSE 81

CMD ["gg", "/usr/app/publish/Billing.Receiver.dll", "--types", "Billing.BillingService", "--methods", "CalculateMonthlyBill", "--mcpBaseClass", "Billing.BillingService"]
