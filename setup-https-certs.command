#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CERT_DIR="$SCRIPT_DIR/certs"
CERT_FILE="$CERT_DIR/ninjalogs-dev.pfx"
CERT_PASSWORD="NinjaLogsDev123!"

mkdir -p "$CERT_DIR"

echo "Generating/exporting ASP.NET dev certificate..."
DOTNET_OK=true
if ! dotnet dev-certs https --trust; then
  echo "Warning: could not trust certificate automatically. Continuing with export."
fi
if ! dotnet dev-certs https -ep "$CERT_FILE" -p "$CERT_PASSWORD"; then
  DOTNET_OK=false
fi

if [ "$DOTNET_OK" = false ]; then
  echo "Falling back to OpenSSL self-signed certificate..."
  TMP_DIR="$(mktemp -d)"
  KEY_FILE="$TMP_DIR/ninjalogs-dev.key"
  CRT_FILE="$TMP_DIR/ninjalogs-dev.crt"

  openssl req -x509 -newkey rsa:2048 -sha256 -days 365 \
    -nodes \
    -keyout "$KEY_FILE" \
    -out "$CRT_FILE" \
    -subj "/CN=localhost" \
    -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

  openssl pkcs12 -export \
    -out "$CERT_FILE" \
    -inkey "$KEY_FILE" \
    -in "$CRT_FILE" \
    -password "pass:$CERT_PASSWORD"

  rm -rf "$TMP_DIR"
fi

echo "Certificate exported: $CERT_FILE"
echo "Password: $CERT_PASSWORD"
echo ""
echo "Now restart docker services:"
echo "  docker compose down"
echo "  docker compose up --build -d"
