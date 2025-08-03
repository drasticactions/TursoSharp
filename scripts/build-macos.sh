#!/bin/bash

# Build script for macOS universal binary
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BINDINGS_DIR="$PROJECT_ROOT/bindings"
RUNTIME_DIR="$PROJECT_ROOT/runtime"
MACOS_DIR="$RUNTIME_DIR/macos"

echo "Building TursoSharp native library for macOS..."
echo "Project root: $PROJECT_ROOT"

# Ensure we're in the bindings directory
cd "$BINDINGS_DIR"

# Clean previous builds
echo "Cleaning previous builds..."
cargo clean

# Add targets if they're not already installed
echo "Adding Rust targets..."
rustup target add x86_64-apple-darwin
rustup target add aarch64-apple-darwin

# Build for x86_64
echo "Building for x86_64-apple-darwin..."
cargo build --release --target x86_64-apple-darwin

# Build for aarch64
echo "Building for aarch64-apple-darwin..."
cargo build --release --target aarch64-apple-darwin

# Create runtime/macos directory if it doesn't exist
echo "Creating runtime directory..."
mkdir -p "$MACOS_DIR"

# Create universal binary using lipo
echo "Creating universal binary..."
lipo -create \
    "$BINDINGS_DIR/target/x86_64-apple-darwin/release/libturso_csharp.dylib" \
    "$BINDINGS_DIR/target/aarch64-apple-darwin/release/libturso_csharp.dylib" \
    -output "$MACOS_DIR/libturso_csharp.dylib"

# Verify the universal binary
echo "Verifying universal binary..."
file "$MACOS_DIR/libturso_csharp.dylib"
lipo -info "$MACOS_DIR/libturso_csharp.dylib"

echo "✅ macOS universal binary built successfully!"
echo "Location: $MACOS_DIR/libturso_csharp.dylib"