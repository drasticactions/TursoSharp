#!/bin/bash

# Build script for Android native libraries using cargo-ndk
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BINDINGS_DIR="$PROJECT_ROOT/bindings"
RUNTIME_DIR="$PROJECT_ROOT/runtime"

echo "Building libturso_csharp.so for Android platforms using cargo-ndk..."
echo "Project root: $PROJECT_ROOT"

# Check if cargo-ndk is installed
if ! command -v cargo-ndk >/dev/null 2>&1; then
    echo "Installing cargo-ndk..."
    cargo install cargo-ndk
fi

# Ensure we're in the bindings directory
cd "$BINDINGS_DIR"

# Clean previous builds
echo "Cleaning previous builds..."
cargo clean

# Define Android targets and their corresponding runtime directories
ANDROID_TARGETS=(
    "x86_64" 
    "arm64-v8a"
)

# Build for all Android targets using cargo-ndk
echo "Building for all Android targets using cargo-ndk..."
cargo ndk -t x86_64 -t arm64-v8a build --release

# Function to get runtime directory for Android ABI
get_runtime_dir() {
    case "$1" in
        "x86_64") echo "android-x86_64" ;;
        "arm64-v8a") echo "android-arm64-v8a" ;;
        *) echo "unknown" ;;
    esac
}

# Function to get Rust target for Android ABI
get_rust_target() {
    case "$1" in
        "x86_64") echo "x86_64-linux-android" ;;
        "arm64-v8a") echo "aarch64-linux-android" ;;
        *) echo "unknown" ;;
    esac
}

# Create Android runtime directories and copy libraries
echo "Creating Android runtime directories and copying libraries..."
for abi in "${ANDROID_TARGETS[@]}"; do
    runtime_dir=$(get_runtime_dir "$abi")
    rust_target=$(get_rust_target "$abi")
    android_dir="$RUNTIME_DIR/$runtime_dir"
    
    echo "Processing $abi -> $runtime_dir"
    
    # Create directory
    mkdir -p "$android_dir"
    
    # Copy shared library (cargo-ndk builds to target/<rust-target>/release/)
    source_lib="$BINDINGS_DIR/target/$rust_target/release/libturso_csharp.so"
    dest_lib="$android_dir/libturso_csharp.so"
    
    if [ -f "$source_lib" ]; then
        echo "  Copying $source_lib to $dest_lib"
        cp "$source_lib" "$dest_lib"
    else
        echo "  ❌ Library not found for ABI $abi at $source_lib"
        exit 1
    fi
done

# Verify all libraries
echo ""
echo "Verifying Android libraries..."
all_success=true

for abi in "${ANDROID_TARGETS[@]}"; do
    runtime_dir=$(get_runtime_dir "$abi")
    lib_path="$RUNTIME_DIR/$runtime_dir/libturso_csharp.so"
    
    if [ -f "$lib_path" ]; then
        echo "✅ $runtime_dir/libturso_csharp.so created successfully!"
        echo "  Location: $lib_path"
        
        # Show file info
        file_info=$(file "$lib_path")
        echo "  Info: $file_info"
        echo "  Size: $(du -h "$lib_path" | cut -f1)"
        
        # Show library dependencies if available
        if command -v readelf >/dev/null 2>&1; then
            echo "  Dependencies:"
            readelf -d "$lib_path" 2>/dev/null | grep "NEEDED" | sed 's/^/    /' || echo "    (no dynamic dependencies or readelf failed)"
        fi
        echo ""
    else
        echo "❌ Failed to create $runtime_dir/libturso_csharp.so"
        all_success=false
    fi
done

if [ "$all_success" = true ]; then
    echo "🎉 Android libraries build completed successfully!"
    echo "The libraries can now be used with:"
    echo "  - net9.0-android"
    echo "  - Any .NET target framework on Android"
    echo ""
    echo "Supported Android ABIs:"
    echo "  - x86_64 (android-x86_64)"  
    echo "  - arm64-v8a (android-arm64-v8a)"
else
    echo "❌ Some Android libraries failed to build"
    exit 1
fi