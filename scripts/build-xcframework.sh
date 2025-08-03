#!/bin/bash

# Build script for XCFramework supporting iOS, macOS, Mac Catalyst, and tvOS
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BINDINGS_DIR="$PROJECT_ROOT/bindings"
RUNTIME_DIR="$PROJECT_ROOT/runtime"
FRAMEWORKS_DIR="$RUNTIME_DIR/Frameworks"
XCFRAMEWORK_NAME="libturso_csharp.xcframework"

echo "Building libturso_csharp XCFramework for Apple platforms..."
echo "Project root: $PROJECT_ROOT"

# Ensure we're in the bindings directory
cd "$BINDINGS_DIR"

# Clean previous builds
echo "Cleaning previous builds..."
cargo clean

# Define all required targets (including tier 3 tvOS support)
TARGETS=(
    # tvOS Device (tier 3 support)
    #"aarch64-apple-tvos"
    #"aarch64-apple-tvos-sim"
    
    # tvOS Simulator (tier 3 support)
    #"x86_64-apple-tvos"

    # macOS
    "x86_64-apple-darwin"
    "aarch64-apple-darwin"
    
    # iOS Device
    "aarch64-apple-ios"
    
    # iOS Simulator
    "aarch64-apple-ios-sim"
    "x86_64-apple-ios"
    
    # Mac Catalyst
    "x86_64-apple-ios-macabi"
    "aarch64-apple-ios-macabi"

)

# Install nightly toolchain for tier 3 targets
echo "Installing nightly toolchain for tier 3 targets..."
rustup toolchain install nightly

# Install all required targets
echo "Installing Rust targets..."
for target in "${TARGETS[@]}"; do
    echo "Installing target: $target"
    if [[ "$target" == *"tvos"* ]]; then
        # Use nightly toolchain for tvOS targets (tier 3 support)
        rustup component add rust-src --toolchain nightly-aarch64-apple-darwin
    else
        # Use stable toolchain for other targets
        rustup target add "$target"
    fi
done

# Build for all targets
echo "Building for all Apple targets..."
for target in "${TARGETS[@]}"; do
    echo "Building for target: $target"
    if [[ "$target" == *"tvos"* ]]; then
        # Use nightly toolchain with -Zbuild-std for tvOS targets (tier 3 support)
        cargo +nightly build --release --target "$target" -Zbuild-std
    else
        # Use stable toolchain for other targets
        cargo build --release --target "$target"
    fi
done

# Create individual frameworks for each platform
echo "Creating individual frameworks..."

# Create temporary build directory
BUILD_DIR="$BINDINGS_DIR/build"
mkdir -p "$BUILD_DIR"

# Function to create a framework
create_framework() {
    local platform="$1"
    local framework_name="$2"
    shift 2
    local targets=("$@")
    
    local platform_dir="$BUILD_DIR/$platform"
    local framework_dir="$platform_dir/$framework_name.framework"
    mkdir -p "$framework_dir"
    
    echo "Creating $framework_name.framework for $platform..."
    
    # Create fat binary if multiple targets
    if [ ${#targets[@]} -gt 1 ]; then
        local lipo_args=()
        for target in "${targets[@]}"; do
            local lib_path="$BINDINGS_DIR/target/$target/release/libturso_csharp.dylib"
            if [ -f "$lib_path" ]; then
                lipo_args+=("$lib_path")
            else
                echo "Warning: Library not found for target $target at $lib_path"
            fi
        done
        
        if [ ${#lipo_args[@]} -gt 0 ]; then
            lipo -create "${lipo_args[@]}" -output "$framework_dir/$framework_name"
        else
            echo "Error: No valid libraries found for $platform"
            return 1
        fi
    else
        # Single target
        local lib_path="$BINDINGS_DIR/target/${targets[0]}/release/libturso_csharp.dylib"
        if [ -f "$lib_path" ]; then
            cp "$lib_path" "$framework_dir/$framework_name"
        else
            echo "Error: Library not found for target ${targets[0]} at $lib_path"
            return 1
        fi
    fi
    
    # Create Info.plist
    cat > "$framework_dir/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>$framework_name</string>
    <key>CFBundleIdentifier</key>
    <string>com.drasticactions.turso-csharp</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$framework_name</string>
    <key>CFBundlePackageType</key>
    <string>FMWK</string>
    <key>CFBundleShortVersionString</key>
    <string>0.1.3</string>
    <key>CFBundleVersion</key>
    <string>0.1.3</string>
    <key>MinimumOSVersion</key>
    <string>12.0</string>
</dict>
</plist>
EOF
    
    echo "Created $framework_name.framework"
}

# Create frameworks for each platform
create_framework "macos" "libturso_csharp" "x86_64-apple-darwin" "aarch64-apple-darwin"
create_framework "ios" "libturso_csharp" "aarch64-apple-ios"
create_framework "ios-simulator" "libturso_csharp" "aarch64-apple-ios-sim" "x86_64-apple-ios"
create_framework "maccatalyst" "libturso_csharp" "x86_64-apple-ios-macabi" "aarch64-apple-ios-macabi"
#create_framework "tvos" "libturso_csharp" "aarch64-apple-tvos"
#create_framework "tvos-simulator" "libturso_csharp" "aarch64-apple-tvos-sim" "x86_64-apple-tvos"

# Create the XCFramework
echo "Creating XCFramework..."
mkdir -p "$FRAMEWORKS_DIR"

# Remove existing XCFramework if it exists
if [ -d "$FRAMEWORKS_DIR/$XCFRAMEWORK_NAME" ]; then
    rm -rf "$FRAMEWORKS_DIR/$XCFRAMEWORK_NAME"
fi

# Build xcodebuild command
XCODEBUILD_ARGS=(
    "xcodebuild" "-create-xcframework"
)

# Add all frameworks
for platform_dir in "$BUILD_DIR"/*; do
    if [ -d "$platform_dir" ]; then
        for framework in "$platform_dir"/*.framework; do
            if [ -d "$framework" ]; then
                XCODEBUILD_ARGS+=("-framework" "$framework")
            fi
        done
    fi
done

# Set output path
XCODEBUILD_ARGS+=("-output" "$FRAMEWORKS_DIR/$XCFRAMEWORK_NAME")

# Execute xcodebuild
echo "Running: ${XCODEBUILD_ARGS[*]}"
"${XCODEBUILD_ARGS[@]}"

# Clean up temporary build directory
echo "Cleaning up temporary files..."
rm -rf "$BUILD_DIR"

# Verify the XCFramework
echo "Verifying XCFramework..."
if [ -d "$FRAMEWORKS_DIR/$XCFRAMEWORK_NAME" ]; then
    echo "✅ XCFramework created successfully!"
    echo "Location: $FRAMEWORKS_DIR/$XCFRAMEWORK_NAME"
    
    # Show framework info
    echo ""
    echo "XCFramework contents:"
    find "$FRAMEWORKS_DIR/$XCFRAMEWORK_NAME" -name "*.framework" -type d | while read -r framework; do
        echo "  $(basename "$framework")"
        if [ -f "$framework/libturso_csharp" ]; then
            file "$framework/libturso_csharp" | sed 's/^/    /'
        fi
    done
else
    echo "❌ Failed to create XCFramework"
    exit 1
fi

echo ""
echo "🎉 XCFramework build completed successfully!"
echo "The XCFramework can now be used with:"
echo "  - net9.0-macos"
echo "  - net9.0-ios" 
echo "  - net9.0-maccatalyst"
# echo "  - net9.0-tvos"