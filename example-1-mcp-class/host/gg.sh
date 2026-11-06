#!/usr/bin/env bash
# Filter the callable surface:
# https://docs.graftcode.com/how-to-guides/filter-the-callable-surface
#
#   dotnet build ./Pricing/Pricing.csproj
#   gg ./Pricing/bin/Debug/net9.0/Pricing.dll --types Pricing.PriceService --methods Calculate
#
# The command below uses those flags with this sample's assembly and type.
# --types and --methods select what Gateway publishes.
# They do not authenticate or authorize a caller.
# Invocation auth is a separate Receiver concern:
# https://docs.graftcode.com/operations/authentication-and-authorization-operations
#
# MCP endpoint from the .NET quick start (Vision HTTP port 81):
#   http://localhost:81/mcp
# https://docs.graftcode.com/quick-start/expose-mcp/dotnet
#
# --mcpBaseClass is the declaring type used when MCP tools/call sends a
# bare method name and params.class is empty:
# https://docs.graftcode.com/how-to-guides/expose-receiver-methods-for-mcp
#
# Named here: CalculateMonthlyBill, ApplyTax (public static).
# Left off this command: LateFee (public static) and FormatInvoice
# (public instance). Matcher details differ by runtime; confirm the
# hosted surface in Vision before anyone installs a Graft.
#
# This file is the sample command. The tests read it. They do not execute
# it, and they do not start Graftcode Gateway.

set -euo pipefail

cd "$(dirname "$0")/.."

dotnet build ./InvoiceCalculator/InvoiceCalculator.csproj

gg ./InvoiceCalculator/bin/Debug/net9.0/InvoiceCalculator.dll \
  --types Billing.InvoiceCalculator \
  --methods CalculateMonthlyBill,ApplyTax \
  --mcpBaseClass Billing.InvoiceCalculator
