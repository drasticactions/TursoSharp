use csbindgen::Builder;

fn main() {
    // Generate C# bindings from Rust extern "C" functions
    Builder::default()
        .input_extern_file("src/lib.rs")
        .csharp_dll_name("turso_csharp")
        .csharp_namespace("Turso.Native")
        .csharp_class_name("TursoFFI")
        .csharp_use_nint_types(true)
        .csharp_dll_name_if("IOS || MACOS || TVOS || MACCATALYST", "__Internal")
        .generate_csharp_file("../src/TursoSharp/Generated/TursoNative.g.cs")
        .unwrap();

    println!("cargo:rerun-if-changed=src/lib.rs");
    println!("cargo:rerun-if-changed=build.rs");
}