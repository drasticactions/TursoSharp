#!/bin/bash

# Build script for WebAssembly target
# This script builds the Rust bindings for wasm32-wasip1 target and places the output in the runtime folder

set -e  # Exit on any error

echo "🦀 Building TursoSharp WebAssembly bindings..."

# Check if we're in the right directory
if [ ! -f "bindings/Cargo.toml" ]; then
    echo "❌ Error: This script must be run from the project root directory"
    exit 1
fi

# Install wasm32-wasip1 target if not already installed
echo "📦 Ensuring wasm32-wasip1 target is installed..."
rustup target add wasm32-wasip1

# Create runtime/browser-wasm directory if it doesn't exist
echo "📁 Creating runtime directory structure..."
mkdir -p runtime/browser-wasm

# Build the Rust library for WebAssembly
echo "🔨 Building Rust library for wasm32-wasip1..."
cd bindings

# We need to build as staticlib for WASM instead of cdylib
# Temporarily modify Cargo.toml to build staticlib
cp Cargo.toml Cargo.toml.backup

# Update crate-type to staticlib and tokio features for WASM build
sed -e 's/crate-type = \["cdylib"\]/crate-type = ["staticlib"]/' \
    -e 's/tokio = { version = "1.0", features = \["rt", "rt-multi-thread"\] }/tokio = { version = "1.0", features = ["rt", "sync", "macros"] }/' \
    Cargo.toml.backup > Cargo.toml

# Build for WASM target with compatible features
cargo build --target wasm32-wasip1 --release --no-default-features

# Restore original Cargo.toml
mv Cargo.toml.backup Cargo.toml

# Copy the built library to the runtime directory
echo "📋 Copying WebAssembly library to runtime directory..."
cp target/wasm32-wasip1/release/libturso_csharp.a ../runtime/browser-wasm/turso_csharp.a

echo "✅ WebAssembly build completed successfully!"
echo "📍 Output: runtime/browser-wasm/turso_csharp.a"

# Verify the file was created
if [ -f "../runtime/browser-wasm/turso_csharp.a" ]; then
    echo "🎉 Build verification: turso_csharp.a created successfully"
    # Show file size
    ls -lh ../runtime/browser-wasm/turso_csharp.a
else
    echo "❌ Error: Expected output file not found"
    exit 1
fi