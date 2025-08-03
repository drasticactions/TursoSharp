#!/bin/bash

# Build script for Linux shared libraries
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BINDINGS_DIR="$PROJECT_ROOT/bindings"
RUNTIME_DIR="$PROJECT_ROOT/runtime"
LINUX_DIR="$RUNTIME_DIR/linux-x64"

echo "Building libturso_csharp.so for Linux platforms..."
echo "Project root: $PROJECT_ROOT"

# Ensure we're in the bindings directory
cd "$BINDINGS_DIR"

# Clean previous builds
echo "Cleaning previous builds..."
cargo clean

# Define Linux targets
TARGETS=(
    "x86_64-unknown-linux-gnu"
    # "x86_64-unknown-linux-musl"
    # "aarch64-unknown-linux-gnu"
    # "aarch64-unknown-linux-musl"
)

# Install all required targets
echo "Installing Rust targets..."
for target in "${TARGETS[@]}"; do
    echo "Installing target: $target"
    rustup target add "$target"
done

# Build for all targets
echo "Building for all Linux targets..."
for target in "${TARGETS[@]}"; do
    echo "Building for target: $target"
    cargo build --release --target "$target"
done

# Create Linux runtime directory
echo "Creating Linux runtime directory..."S
mkdir -p "$LINUX_DIR"

# Copy the primary shared library (prefer GNU x64 if available, fallback to musl)
echo "Copying libturso_csharp.so to runtime directory..."
if [ -f "$BINDINGS_DIR/target/x86_64-unknown-linux-gnu/release/libturso_csharp.so" ]; then
    echo "Using GNU x64 build"
    cp "$BINDINGS_DIR/target/x86_64-unknown-linux-gnu/release/libturso_csharp.so" "$LINUX_DIR/libturso_csharp.so"
# elif [ -f "$BINDINGS_DIR/target/x86_64-unknown-linux-musl/release/libturso_csharp.so" ]; then
#     echo "Using musl x64 build"
#     cp "$BINDINGS_DIR/target/x86_64-unknown-linux-musl/release/libturso_csharp.so" "$LINUX_DIR/libturso_csharp.so"
else
    echo "❌ No Linux shared library found"
    exit 1
fi

# Verify the shared library
echo "Verifying Linux shared library..."
if [ -f "$LINUX_DIR/libturso_csharp.so" ]; then
    echo "✅ Linux shared library created successfully!"
    echo "Location: $LINUX_DIR/libturso_csharp.so"
    
    # Show file info
    echo ""
    echo "Shared library info:"
    file "$LINUX_DIR/libturso_csharp.so" | sed 's/^/  /'
    echo "Size: $(du -h "$LINUX_DIR/libturso_csharp.so" | cut -f1)"
    
    # Show library dependencies if ldd is available
    if command -v ldd >/dev/null 2>&1; then
        echo ""
        echo "Library dependencies:"
        ldd "$LINUX_DIR/libturso_csharp.so" 2>/dev/null | sed 's/^/  /' || echo "  (ldd not available or library not compatible)"
    fi
else
    echo "❌ Failed to create Linux shared library"
    exit 1
fi

echo ""
echo "🎉 Linux shared library build completed successfully!"
echo "The shared library can now be used with:"
echo "  - net9.0 on Linux"
echo "  - Any .NET target framework on Linux x64"