# Shape from the .NET MCP quick start, with the callable-surface flags
# from host/gg.sh. Unit tests do not build this image. The e2e project does.
# There is no Gateway image to docker pull. This image starts from the
# documented SDK base and installs gg with the quick start command.
# Port 80 is the default WebSocket port. Port 81 is the default Vision
# HTTP port, which the quick start also uses for /mcp.
FROM mcr.microsoft.com/dotnet/sdk:9.0

WORKDIR /usr/app

COPY . /usr/app/

RUN dotnet build ./InvoiceCalculator/InvoiceCalculator.csproj
RUN dotnet publish ./InvoiceCalculator/InvoiceCalculator.csproj -c Release -o /usr/app/publish

RUN curl -fsSL grft.dev/get/gg | sh && dpkg -i gg.deb && rm gg.deb

EXPOSE 80
EXPOSE 81

# --types and --methods select the callable surface. They are not authorization.
CMD ["gg", "/usr/app/publish/InvoiceCalculator.dll", "--types", "Billing.InvoiceCalculator", "--methods", "CalculateMonthlyBill,ApplyTax", "--mcpBaseClass", "Billing.InvoiceCalculator"]
