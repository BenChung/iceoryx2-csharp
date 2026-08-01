// Custom iceoryx2 platform configuration, injected into
// `iceoryx2-pal-configuration` via `--cfg configuration_override` and
// `IOX2_CUSTOM_PLATFORM_CONFIGURATION_PATH` (see .cargo/config.toml).
// Identical to upstream except the Windows paths: upstream hardcodes
// `C:\Temp\`, which is no real temp directory. `C:\ProgramData` is the
// machine-wide mutable data location that exists on every install. It is
// not reboot-cleared either, but the heavy shm segments are pagefile-backed
// (never on disk) and iceoryx2 reaps the small marker files itself.
//
// These paths are the IPC rendezvous namespace: binaries built with
// different values cannot see each other's services (this includes stock
// iceoryx2 tooling such as the iox2 CLI). Keep byte-identical with
// deno-script-runtime/seapower-runtime/platform/iceoryx2_settings.rs.

#[cfg(all(not(target_os = "windows"), not(target_os = "nto")))]
pub mod settings {
    pub const GLOBAL_CONFIG_PATH: &[u8] = b"/etc";
    pub const USER_CONFIG_PATH: &[u8] = b".config";
    pub const TEMP_DIRECTORY: &[u8] = b"/tmp/";
    pub const TEST_DIRECTORY: &[u8] = b"/tmp/iceoryx2/tests/";
    pub const SHARED_MEMORY_DIRECTORY: &[u8] = b"/dev/shm/";
    pub const PATH_SEPARATOR: u8 = b'/';
    pub const ROOT: &[u8] = b"/";
    pub const REQUIRED_SOCKET_DIRECTORY: Option<&[u8]> = None;
    pub const ICEORYX2_ROOT_PATH: &[u8] = b"/tmp/iceoryx2/";
    pub const FILENAME_LENGTH: usize = 255;
    pub const PATH_LENGTH: usize = 255;
    #[cfg(not(target_os = "macos"))]
    pub const AT_LEAST_TIMING_VARIANCE: f32 = 0.25;
    #[cfg(target_os = "macos")]
    pub const AT_LEAST_TIMING_VARIANCE: f32 = 1.0;
}

#[cfg(target_os = "nto")]
pub mod settings {
    pub const GLOBAL_CONFIG_PATH: &[u8] = b"/etc";
    pub const USER_CONFIG_PATH: &[u8] = b".config";
    pub const TEMP_DIRECTORY: &[u8] = b"/data/iceoryx2/tmp/";
    pub const TEST_DIRECTORY: &[u8] = b"/data/iceoryx2/tests/";
    pub const SHARED_MEMORY_DIRECTORY: &[u8] = b"/dev/shmem/";
    pub const PATH_SEPARATOR: u8 = b'/';
    pub const ROOT: &[u8] = b"/";
    pub const REQUIRED_SOCKET_DIRECTORY: Option<&[u8]> = None;
    pub const ICEORYX2_ROOT_PATH: &[u8] = b"/data/iceoryx2/";
    pub const FILENAME_LENGTH: usize = 255;
    pub const PATH_LENGTH: usize = 255;
    pub const AT_LEAST_TIMING_VARIANCE: f32 = 0.25;
}

#[cfg(target_os = "windows")]
pub mod settings {
    pub const GLOBAL_CONFIG_PATH: &[u8] = b"C:\\ProgramData";
    pub const USER_CONFIG_PATH: &[u8] = b".config";
    pub const TEMP_DIRECTORY: &[u8] = b"C:\\ProgramData\\iceoryx2\\tmp\\";
    pub const TEST_DIRECTORY: &[u8] = b"C:\\ProgramData\\iceoryx2\\tests\\";
    pub const SHARED_MEMORY_DIRECTORY: &[u8] = b"C:\\ProgramData\\iceoryx2\\shm\\";
    pub const PATH_SEPARATOR: u8 = b'\\';
    pub const ROOT: &[u8] = b"C:\\";
    pub const REQUIRED_SOCKET_DIRECTORY: Option<&[u8]> = None;
    pub const ICEORYX2_ROOT_PATH: &[u8] = b"C:\\ProgramData\\iceoryx2\\";
    pub const FILENAME_LENGTH: usize = 255;
    pub const PATH_LENGTH: usize = 255;
    pub const AT_LEAST_TIMING_VARIANCE: f32 = 1.0;
}
