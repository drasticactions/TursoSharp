use std::ffi::{c_char, c_void, CStr, CString};
use std::ptr;
use std::sync::{Arc, Mutex};
use std::num::NonZero;
use turso_core::{Connection, Database, Value};

pub mod transaction;

use transaction::TransactionBehavior;

#[derive(Debug, thiserror::Error)]
pub enum Error {
    #[error("SQL conversion failure: `{0}`")]
    ToSqlConversionFailure(BoxError),
    #[error("Mutex lock error: {0}")]
    MutexError(String),
    #[error("SQL execution failure: `{0}`")]
    SqlExecutionFailure(String),
    #[error("WAL operation error: `{0}`")]
    WalOperationError(String),
}

impl From<turso_core::LimboError> for Error {
    fn from(err: turso_core::LimboError) -> Self {
        Error::SqlExecutionFailure(err.to_string())
    }
}

pub(crate) type BoxError = Box<dyn std::error::Error + Send + Sync>;

pub type Result<T> = std::result::Result<T, Error>;

// Error handling structure compatible with C#
#[repr(C)]
pub struct TursoFFIResult {
    pub success: bool,
    pub error_message: *mut c_char,
}

impl TursoFFIResult {
    fn success() -> Self {
        Self {
            success: true,
            error_message: ptr::null_mut(),
        }
    }

    fn error(message: &str) -> Self {
        let c_message = CString::new(message).unwrap_or_else(|_| CString::new("Invalid error message").unwrap());
        Self {
            success: false,
            error_message: c_message.into_raw(),
        }
    }

    fn from_result(result: Result<()>) -> Self {
        match result {
            Ok(()) => Self::success(),
            Err(e) => Self::error(&e.to_string()),
        }
    }
}

// Opaque wrapper for Database (following the target API structure)
struct DatabaseWrapper {
    database: Arc<Database>,
}

// Opaque wrapper for Connection
struct ConnectionWrapper {
    connection: Arc<Mutex<Arc<Connection>>>,
    transaction_behavior: TransactionBehavior,
}

// Opaque wrapper for Statement
struct StatementWrapper {
    statement: Arc<Mutex<turso_core::Statement>>,
}

// Opaque wrapper for Rows
struct RowsWrapper {
    inner: Arc<Mutex<turso_core::Statement>>,
}

// Helper function to check if we should use indexes
fn indexes_enabled() -> bool {
    #[cfg(feature = "experimental_indexes")]
    return true;
    #[cfg(not(feature = "experimental_indexes"))]
    return false;
}

// Database operations
#[no_mangle]
pub extern "C" fn turso_database_open_memory() -> *mut c_void {
    let result = std::panic::catch_unwind(|| {
        let io: Arc<dyn turso_core::IO> = Arc::new(turso_core::MemoryIO::new());
        Database::open_file(io, ":memory:", false, indexes_enabled())
    });
    
    match result {
        Ok(Ok(database)) => {
            let wrapper = Box::new(DatabaseWrapper { database });
            Box::into_raw(wrapper) as *mut c_void
        }
        _ => ptr::null_mut(),
    }
}

#[no_mangle]
pub extern "C" fn turso_database_open_file(path: *const c_char) -> *mut c_void {
    if path.is_null() {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let path = unsafe {
            match CStr::from_ptr(path).to_str() {
                Ok(s) => s,
                Err(_) => return Err("Invalid path string"),
            }
        };

        let io: Arc<dyn turso_core::IO> = match path {
            ":memory:" => Arc::new(turso_core::MemoryIO::new()),
            _ => {
                let io_result = turso_core::PlatformIO::new();
                match io_result {
                    Ok(platform_io) => Arc::new(platform_io),
                    Err(_) => return Err("Failed to create platform IO"),
                }
            }
        };

        Database::open_file(io, path, false, indexes_enabled())
            .map_err(|_| "Failed to open database")
    });

    match result {
        Ok(Ok(database)) => {
            let wrapper = Box::new(DatabaseWrapper { database });
            Box::into_raw(wrapper) as *mut c_void
        }
        _ => ptr::null_mut(),
    }
}

#[no_mangle]
pub extern "C" fn turso_database_close(database_ptr: *mut c_void) -> TursoFFIResult {
    if database_ptr.is_null() {
        return TursoFFIResult::error("Database pointer is null");
    }

    let result = std::panic::catch_unwind(|| {
        unsafe {
            let _database = Box::from_raw(database_ptr as *mut DatabaseWrapper);
            // Database will be dropped automatically
        }
        Ok(())
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in database_close".to_string()))))
}

// Connection operations
#[no_mangle]
pub extern "C" fn turso_connection_open(database_ptr: *mut c_void) -> *mut c_void {
    if database_ptr.is_null() {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let database = unsafe { &*(database_ptr as *const DatabaseWrapper) };
        
        database.database.connect().map(|connection| {
            ConnectionWrapper {
                connection: Arc::new(Mutex::new(connection)),
                transaction_behavior: TransactionBehavior::Deferred,
            }
        })
    });

    match result {
        Ok(Ok(wrapper)) => {
            let boxed = Box::new(wrapper);
            Box::into_raw(boxed) as *mut c_void
        }
        _ => ptr::null_mut(),
    }
}

#[no_mangle]
pub extern "C" fn turso_connection_close(connection_ptr: *mut c_void) -> TursoFFIResult {
    if connection_ptr.is_null() {
        return TursoFFIResult::error("Connection pointer is null");
    }

    let result = std::panic::catch_unwind(|| {
        unsafe {
            let _connection = Box::from_raw(connection_ptr as *mut ConnectionWrapper);
            // Connection will be dropped automatically
        }
        Ok(())
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in connection_close".to_string()))))
}

#[no_mangle]
pub extern "C" fn turso_connection_execute(
    connection_ptr: *mut c_void,
    sql: *const c_char,
    rows_changed: *mut u64,
) -> TursoFFIResult {
    if connection_ptr.is_null() || sql.is_null() {
        return TursoFFIResult::error("Invalid parameters");
    }

    let result = std::panic::catch_unwind(|| {
        let connection_wrapper = unsafe { &*(connection_ptr as *const ConnectionWrapper) };
        
        let sql_str = unsafe {
            match CStr::from_ptr(sql).to_str() {
                Ok(s) => s,
                Err(_) => return Err(Error::SqlExecutionFailure("Invalid SQL string".to_string())),
            }
        };

        let conn = connection_wrapper.connection.lock()
            .map_err(|e| Error::MutexError(e.to_string()))?;

        let mut stmt = conn.prepare(sql_str)?;
        
        loop {
            match stmt.step() {
                Ok(turso_core::StepResult::Row) => {
                    return Err(Error::SqlExecutionFailure(
                        "unexpected row during execution".to_string(),
                    ));
                }
                Ok(turso_core::StepResult::Done) => {
                    let changes = stmt.n_change();
                    if !rows_changed.is_null() {
                        unsafe { *rows_changed = changes.max(0) as u64; }
                    }
                    return Ok(());
                }
                Ok(turso_core::StepResult::IO) => {
                    stmt.run_once()?;
                }
                Ok(turso_core::StepResult::Busy) => {
                    return Err(Error::SqlExecutionFailure("database is locked".to_string()));
                }
                Ok(turso_core::StepResult::Interrupt) => {
                    return Err(Error::SqlExecutionFailure("interrupted".to_string()));
                }
                Err(err) => {
                    return Err(err.into());
                }
            }
        }
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in connection_execute".to_string()))))
}

// Query operations
#[no_mangle]
pub extern "C" fn turso_connection_query(
    connection_ptr: *mut c_void,
    sql: *const c_char,
) -> *mut c_void {
    if connection_ptr.is_null() || sql.is_null() {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let connection_wrapper = unsafe { &*(connection_ptr as *const ConnectionWrapper) };
        
        let sql_str = unsafe {
            match CStr::from_ptr(sql).to_str() {
                Ok(s) => s,
                Err(_) => return Err("Invalid SQL string"),
            }
        };

        let conn = connection_wrapper.connection.lock()
            .map_err(|_| "Failed to acquire connection lock")?;

        let stmt = conn.prepare(sql_str)
            .map_err(|_| "Failed to prepare statement")?;

        Ok(RowsWrapper {
            inner: Arc::new(Mutex::new(stmt)),
        })
    });

    match result {
        Ok(Ok(wrapper)) => {
            let boxed = Box::new(wrapper);
            Box::into_raw(boxed) as *mut c_void
        }
        _ => ptr::null_mut(),
    }
}

#[no_mangle]
pub extern "C" fn turso_connection_query_scalar_int(
    connection_ptr: *mut c_void,
    sql: *const c_char,
    result: *mut i64,
) -> TursoFFIResult {
    if connection_ptr.is_null() || sql.is_null() || result.is_null() {
        return TursoFFIResult::error("Invalid parameters");
    }

    let query_result = std::panic::catch_unwind(|| {
        let connection_wrapper = unsafe { &*(connection_ptr as *const ConnectionWrapper) };
        
        let sql_str = unsafe {
            match CStr::from_ptr(sql).to_str() {
                Ok(s) => s,
                Err(_) => return Err(Error::SqlExecutionFailure("Invalid SQL string".to_string())),
            }
        };

        let conn = connection_wrapper.connection.lock()
            .map_err(|e| Error::MutexError(e.to_string()))?;

        let mut stmt = conn.prepare(sql_str)?;
        
        loop {
            match stmt.step() {
                Ok(turso_core::StepResult::Row) => {
                    if let Some(row) = stmt.row() {
                        let value = row.get_value(0);
                        match value {
                            Value::Integer(i) => {
                                unsafe { *result = *i };
                                return Ok(());
                            }
                            _ => return Err(Error::SqlExecutionFailure("Expected integer value".to_string())),
                        }
                    } else {
                        return Err(Error::SqlExecutionFailure("No row data available".to_string()));
                    }
                }
                Ok(turso_core::StepResult::Done) => {
                    return Err(Error::SqlExecutionFailure("No rows returned".to_string()));
                }
                Ok(turso_core::StepResult::IO) => {
                    stmt.run_once()?;
                }
                Ok(turso_core::StepResult::Busy) => {
                    return Err(Error::SqlExecutionFailure("database is locked".to_string()));
                }
                Ok(turso_core::StepResult::Interrupt) => {
                    return Err(Error::SqlExecutionFailure("interrupted".to_string()));
                }
                Err(err) => {
                    return Err(err.into());
                }
            }
        }
    });

    TursoFFIResult::from_result(query_result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in query_scalar_int".to_string()))))
}

#[no_mangle]
pub extern "C" fn turso_connection_query_scalar_string(
    connection_ptr: *mut c_void,
    sql: *const c_char,
) -> *mut c_char {
    if connection_ptr.is_null() || sql.is_null() {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let connection_wrapper = unsafe { &*(connection_ptr as *const ConnectionWrapper) };
        
        let sql_str = unsafe {
            match CStr::from_ptr(sql).to_str() {
                Ok(s) => s,
                Err(_) => return None,
            }
        };

        let conn = connection_wrapper.connection.lock().ok()?;
        let mut stmt = conn.prepare(sql_str).ok()?;
        
        loop {
            match stmt.step() {
                Ok(turso_core::StepResult::Row) => {
                    if let Some(row) = stmt.row() {
                        let value = row.get_value(0);
                        match value {
                            Value::Text(s) => {
                                let s_str = s.to_string();
                                return CString::new(s_str).ok().map(|c| c.into_raw());
                            }
                            _ => return None,
                        }
                    } else {
                        return None;
                    }
                }
                Ok(turso_core::StepResult::Done) => {
                    return None;
                }
                Ok(turso_core::StepResult::IO) => {
                    if stmt.run_once().is_err() {
                        return None;
                    }
                    continue;
                }
                _ => return None,
            }
        }
    });

    result.unwrap_or(None).unwrap_or(ptr::null_mut())
}

// Prepare statement
#[no_mangle]
pub extern "C" fn turso_connection_prepare(
    connection_ptr: *mut c_void,
    sql: *const c_char,
) -> *mut c_void {
    if connection_ptr.is_null() || sql.is_null() {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let connection_wrapper = unsafe { &*(connection_ptr as *const ConnectionWrapper) };
        
        let sql_str = unsafe {
            match CStr::from_ptr(sql).to_str() {
                Ok(s) => s,
                Err(_) => return Err("Invalid SQL string"),
            }
        };

        let conn = connection_wrapper.connection.lock()
            .map_err(|_| "Failed to acquire connection lock")?;

        let statement = conn.prepare(sql_str)
            .map_err(|_| "Failed to prepare statement")?;

        Ok(StatementWrapper {
            statement: Arc::new(Mutex::new(statement)),
        })
    });

    match result {
        Ok(Ok(wrapper)) => {
            let boxed = Box::new(wrapper);
            Box::into_raw(boxed) as *mut c_void
        }
        _ => ptr::null_mut(),
    }
}

// Rows operations
#[no_mangle]
pub extern "C" fn turso_rows_next(rows_ptr: *mut c_void) -> i32 {
    if rows_ptr.is_null() {
        return -1; // Error
    }

    let result = std::panic::catch_unwind(|| {
        let rows_wrapper = unsafe { &*(rows_ptr as *const RowsWrapper) };
        
        let mut stmt = rows_wrapper.inner.lock()
            .map_err(|_| "Failed to acquire statement lock")?;
        
        loop {
            match stmt.step() {
                Ok(turso_core::StepResult::Row) => return Ok(1), // Row available
                Ok(turso_core::StepResult::Done) => return Ok(0), // No more rows
                Ok(turso_core::StepResult::IO) => {
                    stmt.run_once().map_err(|_| "I/O error")?;
                    continue;
                }
                Ok(turso_core::StepResult::Busy) => return Err("Database is locked"),
                Ok(turso_core::StepResult::Interrupt) => return Err("Interrupted"),
                Err(_) => return Err("Step failed"),
            }
        }
    });

    result.unwrap_or(Err("Panic in rows_next")).unwrap_or(-1)
}

#[no_mangle]
pub extern "C" fn turso_rows_close(rows_ptr: *mut c_void) -> TursoFFIResult {
    if rows_ptr.is_null() {
        return TursoFFIResult::error("Rows pointer is null");
    }

    let result = std::panic::catch_unwind(|| {
        unsafe {
            let _rows = Box::from_raw(rows_ptr as *mut RowsWrapper);
            // Rows will be dropped automatically
        }
        Ok(())
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in rows_close".to_string()))))
}

#[no_mangle]
pub extern "C" fn turso_statement_step(statement_ptr: *mut c_void) -> i32 {
    if statement_ptr.is_null() {
        return -1; // Error
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        
        let mut stmt = statement_wrapper.statement.lock()
            .map_err(|_| "Failed to acquire statement lock")?;
        
        loop {
            match stmt.step() {
                Ok(turso_core::StepResult::Row) => return Ok(1), // Row available
                Ok(turso_core::StepResult::Done) => return Ok(0), // No more rows
                Ok(turso_core::StepResult::IO) => {
                    stmt.run_once().map_err(|_| "I/O error")?;
                    continue;
                }
                Ok(turso_core::StepResult::Busy) => return Err("Database is locked"),
                Ok(turso_core::StepResult::Interrupt) => return Err("Interrupted"),
                Err(_) => return Err("Step failed"),
            }
        }
    });

    result.unwrap_or(Err("Panic in statement_step")).unwrap_or(-1)
}

// Column operations for both Rows and Statement
#[no_mangle]
pub extern "C" fn turso_rows_column_count(rows_ptr: *mut c_void) -> i32 {
    if rows_ptr.is_null() {
        return -1;
    }

    let result = std::panic::catch_unwind(|| {
        let rows_wrapper = unsafe { &*(rows_ptr as *const RowsWrapper) };
        let stmt = rows_wrapper.inner.lock().ok()?;
        Some(stmt.num_columns() as i32)
    });

    result.unwrap_or(None).unwrap_or(-1)
}

#[no_mangle]
pub extern "C" fn turso_statement_column_count(statement_ptr: *mut c_void) -> i32 {
    if statement_ptr.is_null() {
        return -1;
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        let stmt = statement_wrapper.statement.lock().ok()?;
        Some(stmt.num_columns() as i32)
    });

    result.unwrap_or(None).unwrap_or(-1)
}

#[no_mangle]
pub extern "C" fn turso_rows_column_name(
    rows_ptr: *mut c_void,
    column_index: i32,
) -> *mut c_char {
    if rows_ptr.is_null() || column_index < 0 {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let rows_wrapper = unsafe { &*(rows_ptr as *const RowsWrapper) };
        let stmt = rows_wrapper.inner.lock().ok()?;
        let column_name = stmt.get_column_name(column_index as usize);
        CString::new(column_name.as_ref()).ok().map(|c| c.into_raw())
    });

    result.unwrap_or(None).unwrap_or(ptr::null_mut())
}

#[no_mangle]
pub extern "C" fn turso_statement_column_name(
    statement_ptr: *mut c_void,
    column_index: i32,
) -> *mut c_char {
    if statement_ptr.is_null() || column_index < 0 {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        let stmt = statement_wrapper.statement.lock().ok()?;
        let column_name = stmt.get_column_name(column_index as usize);
        CString::new(column_name.as_ref()).ok().map(|c| c.into_raw())
    });

    result.unwrap_or(None).unwrap_or(ptr::null_mut())
}

#[no_mangle]
pub extern "C" fn turso_rows_column_type(
    rows_ptr: *mut c_void,
    column_index: i32,
) -> i32 {
    if rows_ptr.is_null() || column_index < 0 {
        return -1;
    }

    let result = std::panic::catch_unwind(|| {
        let rows_wrapper = unsafe { &*(rows_ptr as *const RowsWrapper) };
        let stmt = rows_wrapper.inner.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            match value {
                Value::Null => Some(0),      // NULL
                Value::Integer(_) => Some(1), // INTEGER
                Value::Float(_) => Some(2),   // REAL/FLOAT
                Value::Text(_) => Some(3),    // TEXT
                Value::Blob(_) => Some(4),    // BLOB
            }
        } else {
            None
        }
    });

    result.unwrap_or(None).unwrap_or(-1)
}

#[no_mangle]
pub extern "C" fn turso_statement_column_type(
    statement_ptr: *mut c_void,
    column_index: i32,
) -> i32 {
    if statement_ptr.is_null() || column_index < 0 {
        return -1;
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        let stmt = statement_wrapper.statement.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            match value {
                Value::Null => Some(0),      // NULL
                Value::Integer(_) => Some(1), // INTEGER
                Value::Float(_) => Some(2),   // REAL/FLOAT
                Value::Text(_) => Some(3),    // TEXT
                Value::Blob(_) => Some(4),    // BLOB
            }
        } else {
            None
        }
    });

    result.unwrap_or(None).unwrap_or(-1)
}

#[no_mangle]
pub extern "C" fn turso_rows_column_int64(
    rows_ptr: *mut c_void,
    column_index: i32,
) -> i64 {
    if rows_ptr.is_null() || column_index < 0 {
        return 0;
    }

    let result = std::panic::catch_unwind(|| {
        let rows_wrapper = unsafe { &*(rows_ptr as *const RowsWrapper) };
        let stmt = rows_wrapper.inner.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            match value {
                Value::Integer(i) => Some(*i),
                _ => Some(0),
            }
        } else {
            Some(0)
        }
    });

    result.unwrap_or(None).unwrap_or(0)
}

#[no_mangle]
pub extern "C" fn turso_statement_column_int64(
    statement_ptr: *mut c_void,
    column_index: i32,
) -> i64 {
    if statement_ptr.is_null() || column_index < 0 {
        return 0;
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        let stmt = statement_wrapper.statement.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            match value {
                Value::Integer(i) => Some(*i),
                _ => Some(0),
            }
        } else {
            Some(0)
        }
    });

    result.unwrap_or(None).unwrap_or(0)
}

#[no_mangle]
pub extern "C" fn turso_rows_column_double(
    rows_ptr: *mut c_void,
    column_index: i32,
) -> f64 {
    if rows_ptr.is_null() || column_index < 0 {
        return 0.0;
    }

    let result = std::panic::catch_unwind(|| {
        let rows_wrapper = unsafe { &*(rows_ptr as *const RowsWrapper) };
        let stmt = rows_wrapper.inner.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            match value {
                Value::Float(f) => Some(*f),
                Value::Integer(i) => Some(*i as f64),
                _ => Some(0.0),
            }
        } else {
            Some(0.0)
        }
    });

    result.unwrap_or(None).unwrap_or(0.0)
}

#[no_mangle]
pub extern "C" fn turso_statement_column_double(
    statement_ptr: *mut c_void,
    column_index: i32,
) -> f64 {
    if statement_ptr.is_null() || column_index < 0 {
        return 0.0;
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        let stmt = statement_wrapper.statement.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            match value {
                Value::Float(f) => Some(*f),
                Value::Integer(i) => Some(*i as f64),
                _ => Some(0.0),
            }
        } else {
            Some(0.0)
        }
    });

    result.unwrap_or(None).unwrap_or(0.0)
}

#[no_mangle]
pub extern "C" fn turso_rows_column_text(
    rows_ptr: *mut c_void,
    column_index: i32,
) -> *mut c_char {
    if rows_ptr.is_null() || column_index < 0 {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let rows_wrapper = unsafe { &*(rows_ptr as *const RowsWrapper) };
        let stmt = rows_wrapper.inner.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            let text = match value {
                Value::Text(s) => s.to_string(),
                Value::Integer(i) => i.to_string(),
                Value::Float(f) => f.to_string(),
                _ => return None,
            };
            CString::new(text).ok().map(|c| c.into_raw())
        } else {
            None
        }
    });

    result.unwrap_or(None).unwrap_or(ptr::null_mut())
}

#[no_mangle]
pub extern "C" fn turso_statement_column_text(
    statement_ptr: *mut c_void,
    column_index: i32,
) -> *mut c_char {
    if statement_ptr.is_null() || column_index < 0 {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        let stmt = statement_wrapper.statement.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            let text = match value {
                Value::Text(s) => s.to_string(),
                Value::Integer(i) => i.to_string(),
                Value::Float(f) => f.to_string(),
                _ => return None,
            };
            CString::new(text).ok().map(|c| c.into_raw())
        } else {
            None
        }
    });

    result.unwrap_or(None).unwrap_or(ptr::null_mut())
}

#[no_mangle]
pub extern "C" fn turso_rows_column_is_null(
    rows_ptr: *mut c_void,
    column_index: i32,
) -> bool {
    if rows_ptr.is_null() || column_index < 0 {
        return true;
    }

    let result = std::panic::catch_unwind(|| {
        let rows_wrapper = unsafe { &*(rows_ptr as *const RowsWrapper) };
        let stmt = rows_wrapper.inner.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            Some(matches!(value, Value::Null))
        } else {
            Some(true)
        }
    });

    result.unwrap_or(None).unwrap_or(true)
}

#[no_mangle]
pub extern "C" fn turso_statement_column_is_null(
    statement_ptr: *mut c_void,
    column_index: i32,
) -> bool {
    if statement_ptr.is_null() || column_index < 0 {
        return true;
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        let stmt = statement_wrapper.statement.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            Some(matches!(value, Value::Null))
        } else {
            Some(true)
        }
    });

    result.unwrap_or(None).unwrap_or(true)
}

#[no_mangle]
pub extern "C" fn turso_statement_finalize(statement_ptr: *mut c_void) -> TursoFFIResult {
    if statement_ptr.is_null() {
        return TursoFFIResult::error("Statement pointer is null");
    }

    let result = std::panic::catch_unwind(|| {
        unsafe {
            let _statement = Box::from_raw(statement_ptr as *mut StatementWrapper);
            // Statement will be dropped automatically
        }
        Ok(())
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in statement_finalize".to_string()))))
}

// Parameter binding operations
#[no_mangle]
pub extern "C" fn turso_statement_bind_int64(
    statement_ptr: *mut c_void,
    param_index: i32,
    value: i64,
) -> TursoFFIResult {
    if statement_ptr.is_null() || param_index < 1 {
        return TursoFFIResult::error("Invalid parameters");
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        
        match NonZero::new(param_index as usize) {
            Some(index) => {
                let mut stmt = statement_wrapper.statement.lock()
                    .map_err(|e| Error::MutexError(e.to_string()))?;
                stmt.bind_at(index, turso_core::Value::Integer(value));
                Ok(())
            }
            None => Err(Error::SqlExecutionFailure("Parameter index must be greater than 0".to_string())),
        }
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in statement_bind_int64".to_string()))))
}

#[no_mangle]
pub extern "C" fn turso_statement_bind_double(
    statement_ptr: *mut c_void,
    param_index: i32,
    value: f64,
) -> TursoFFIResult {
    if statement_ptr.is_null() || param_index < 1 {
        return TursoFFIResult::error("Invalid parameters");
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        
        match NonZero::new(param_index as usize) {
            Some(index) => {
                let mut stmt = statement_wrapper.statement.lock()
                    .map_err(|e| Error::MutexError(e.to_string()))?;
                stmt.bind_at(index, turso_core::Value::Float(value));
                Ok(())
            }
            None => Err(Error::SqlExecutionFailure("Parameter index must be greater than 0".to_string())),
        }
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in statement_bind_double".to_string()))))
}

#[no_mangle]
pub extern "C" fn turso_statement_bind_text(
    statement_ptr: *mut c_void,
    param_index: i32,
    value: *const c_char,
) -> TursoFFIResult {
    if statement_ptr.is_null() || param_index < 1 || value.is_null() {
        return TursoFFIResult::error("Invalid parameters");
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        
        let value_str = unsafe {
            match CStr::from_ptr(value).to_str() {
                Ok(s) => s,
                Err(_) => return Err(Error::SqlExecutionFailure("Invalid UTF-8 string".to_string())),
            }
        };

        match NonZero::new(param_index as usize) {
            Some(index) => {
                let mut stmt = statement_wrapper.statement.lock()
                    .map_err(|e| Error::MutexError(e.to_string()))?;
                stmt.bind_at(index, turso_core::Value::from_text(value_str));
                Ok(())
            }
            None => Err(Error::SqlExecutionFailure("Parameter index must be greater than 0".to_string())),
        }
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in statement_bind_text".to_string()))))
}


#[no_mangle]
pub extern "C" fn turso_statement_reset(statement_ptr: *mut c_void) -> TursoFFIResult {
    if statement_ptr.is_null() {
        return TursoFFIResult::error("Statement pointer is null");
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        let mut stmt = statement_wrapper.statement.lock()
            .map_err(|e| Error::MutexError(e.to_string()))?;
        stmt.reset();
        Ok(())
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in statement_reset".to_string()))))
}

// Blob binding operations
#[no_mangle]
pub extern "C" fn turso_statement_bind_blob(
    statement_ptr: *mut c_void,
    param_index: i32,
    data: *const u8,
    data_len: i32,
) -> TursoFFIResult {
    if statement_ptr.is_null() || param_index < 1 || data_len < 0 {
        return TursoFFIResult::error("Invalid parameters");
    }

    // Allow null data pointer only if data_len is 0 (empty blob)
    if data.is_null() && data_len > 0 {
        return TursoFFIResult::error("Invalid parameters");
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        
        let blob_data = if data_len == 0 {
            Vec::new() // Empty blob
        } else {
            unsafe {
                std::slice::from_raw_parts(data, data_len as usize).to_vec()
            }
        };

        match NonZero::new(param_index as usize) {
            Some(index) => {
                let mut stmt = statement_wrapper.statement.lock()
                    .map_err(|e| Error::MutexError(e.to_string()))?;
                stmt.bind_at(index, turso_core::Value::from_blob(blob_data));
                Ok(())
            }
            None => Err(Error::SqlExecutionFailure("Parameter index must be greater than 0".to_string())),
        }
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in statement_bind_blob".to_string()))))
}

#[no_mangle]
pub extern "C" fn turso_rows_column_blob(
    rows_ptr: *mut c_void,
    column_index: i32,
    data_len: *mut i32,
) -> *mut u8 {
    if rows_ptr.is_null() || column_index < 0 || data_len.is_null() {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let rows_wrapper = unsafe { &*(rows_ptr as *const RowsWrapper) };
        let stmt = rows_wrapper.inner.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            match value {
                turso_core::Value::Blob(blob) => {
                    let blob_len = blob.len() as i32;
                    unsafe { *data_len = blob_len };
                    
                    if blob.is_empty() {
                        return Some(ptr::null_mut());
                    }
                    
                    // Allocate memory for the blob data
                    let layout = std::alloc::Layout::array::<u8>(blob.len()).ok()?;
                    let ptr = unsafe { std::alloc::alloc(layout) };
                    if ptr.is_null() {
                        unsafe { *data_len = 0 };
                        return None;
                    }
                    
                    // Copy blob data to allocated memory
                    unsafe {
                        std::ptr::copy_nonoverlapping(blob.as_ptr(), ptr, blob.len());
                    }
                    
                    Some(ptr)
                }
                _ => {
                    unsafe { *data_len = 0 };
                    None
                }
            }
        } else {
            unsafe { *data_len = 0 };
            None
        }
    });

    result.unwrap_or(None).unwrap_or_else(|| {
        unsafe { *data_len = 0; }
        ptr::null_mut()
    })
}

#[no_mangle]
pub extern "C" fn turso_statement_column_blob(
    statement_ptr: *mut c_void,
    column_index: i32,
    data_len: *mut i32,
) -> *mut u8 {
    if statement_ptr.is_null() || column_index < 0 || data_len.is_null() {
        return ptr::null_mut();
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        let stmt = statement_wrapper.statement.lock().ok()?;
        
        if let Some(row) = stmt.row() {
            let value = row.get_value(column_index as usize);
            match value {
                turso_core::Value::Blob(blob) => {
                    let blob_len = blob.len() as i32;
                    unsafe { *data_len = blob_len };
                    
                    if blob.is_empty() {
                        return Some(ptr::null_mut());
                    }
                    
                    // Allocate memory for the blob data
                    let layout = std::alloc::Layout::array::<u8>(blob.len()).ok()?;
                    let ptr = unsafe { std::alloc::alloc(layout) };
                    if ptr.is_null() {
                        unsafe { *data_len = 0 };
                        return None;
                    }
                    
                    // Copy blob data to allocated memory
                    unsafe {
                        std::ptr::copy_nonoverlapping(blob.as_ptr(), ptr, blob.len());
                    }
                    
                    Some(ptr)
                }
                _ => {
                    unsafe { *data_len = 0 };
                    None
                }
            }
        } else {
            unsafe { *data_len = 0 };
            None
        }
    });

    result.unwrap_or(None).unwrap_or_else(|| {
        unsafe { *data_len = 0; }
        ptr::null_mut()
    })
}

#[no_mangle]
pub extern "C" fn turso_free_blob(ptr: *mut u8, data_len: i32) {
    if !ptr.is_null() && data_len > 0 {
        unsafe {
            let layout = std::alloc::Layout::array::<u8>(data_len as usize).unwrap();
            std::alloc::dealloc(ptr, layout);
        }
    }
}

#[no_mangle]
pub extern "C" fn turso_statement_bind_null(
    statement_ptr: *mut c_void,
    param_index: i32,
) -> TursoFFIResult {
    if statement_ptr.is_null() || param_index < 1 {
        return TursoFFIResult::error("Invalid parameters");
    }

    let result = std::panic::catch_unwind(|| {
        let statement_wrapper = unsafe { &*(statement_ptr as *const StatementWrapper) };
        
        match NonZero::new(param_index as usize) {
            Some(index) => {
                let mut stmt = statement_wrapper.statement.lock()
                    .map_err(|e| Error::MutexError(e.to_string()))?;
                stmt.bind_at(index, turso_core::Value::Null);
                Ok(())
            }
            None => Err(Error::SqlExecutionFailure("Parameter index must be greater than 0".to_string())),
        }
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in statement_bind_null".to_string()))))
}

// Transaction operations
#[no_mangle]
pub extern "C" fn turso_connection_begin_transaction(
    connection_ptr: *mut c_void,
    behavior: i32, // 0 = Deferred, 1 = Immediate, 2 = Exclusive
) -> TursoFFIResult {
    if connection_ptr.is_null() {
        return TursoFFIResult::error("Connection pointer is null");
    }

    let result = std::panic::catch_unwind(|| {
        let connection_wrapper = unsafe { &mut *(connection_ptr as *mut ConnectionWrapper) };
        
        let behavior = match behavior {
            0 => TransactionBehavior::Deferred,
            1 => TransactionBehavior::Immediate,
            2 => TransactionBehavior::Exclusive,
            _ => return Err(Error::SqlExecutionFailure("Invalid transaction behavior".to_string())),
        };

        connection_wrapper.transaction_behavior = behavior;

        let conn = connection_wrapper.connection.lock()
            .map_err(|e| Error::MutexError(e.to_string()))?;

        let sql = match behavior {
            TransactionBehavior::Deferred => "BEGIN DEFERRED",
            TransactionBehavior::Immediate => "BEGIN IMMEDIATE", 
            TransactionBehavior::Exclusive => "BEGIN EXCLUSIVE",
        };

        let mut stmt = conn.prepare(sql)?;
        
        loop {
            match stmt.step() {
                Ok(turso_core::StepResult::Done) => return Ok(()),
                Ok(turso_core::StepResult::IO) => {
                    stmt.run_once()?;
                }
                Ok(turso_core::StepResult::Busy) => {
                    return Err(Error::SqlExecutionFailure("database is locked".to_string()));
                }
                Ok(turso_core::StepResult::Interrupt) => {
                    return Err(Error::SqlExecutionFailure("interrupted".to_string()));
                }
                Ok(turso_core::StepResult::Row) => {
                    return Err(Error::SqlExecutionFailure("unexpected row during transaction begin".to_string()));
                }
                Err(err) => {
                    return Err(err.into());
                }
            }
        }
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in begin_transaction".to_string()))))
}

#[no_mangle]
pub extern "C" fn turso_connection_commit_transaction(connection_ptr: *mut c_void) -> TursoFFIResult {
    if connection_ptr.is_null() {
        return TursoFFIResult::error("Connection pointer is null");
    }

    let result = std::panic::catch_unwind(|| {
        let connection_wrapper = unsafe { &*(connection_ptr as *const ConnectionWrapper) };
        
        let conn = connection_wrapper.connection.lock()
            .map_err(|e| Error::MutexError(e.to_string()))?;

        let mut stmt = conn.prepare("COMMIT")?;
        
        loop {
            match stmt.step() {
                Ok(turso_core::StepResult::Done) => return Ok(()),
                Ok(turso_core::StepResult::IO) => {
                    stmt.run_once()?;
                }
                Ok(turso_core::StepResult::Busy) => {
                    return Err(Error::SqlExecutionFailure("database is locked".to_string()));
                }
                Ok(turso_core::StepResult::Interrupt) => {
                    return Err(Error::SqlExecutionFailure("interrupted".to_string()));
                }
                Ok(turso_core::StepResult::Row) => {
                    return Err(Error::SqlExecutionFailure("unexpected row during commit".to_string()));
                }
                Err(err) => {
                    return Err(err.into());
                }
            }
        }
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in commit_transaction".to_string()))))
}

#[no_mangle]
pub extern "C" fn turso_connection_rollback_transaction(connection_ptr: *mut c_void) -> TursoFFIResult {
    if connection_ptr.is_null() {
        return TursoFFIResult::error("Connection pointer is null");
    }

    let result = std::panic::catch_unwind(|| {
        let connection_wrapper = unsafe { &*(connection_ptr as *const ConnectionWrapper) };
        
        let conn = connection_wrapper.connection.lock()
            .map_err(|e| Error::MutexError(e.to_string()))?;

        let mut stmt = conn.prepare("ROLLBACK")?;
        
        loop {
            match stmt.step() {
                Ok(turso_core::StepResult::Done) => return Ok(()),
                Ok(turso_core::StepResult::IO) => {
                    stmt.run_once()?;
                }
                Ok(turso_core::StepResult::Busy) => {
                    return Err(Error::SqlExecutionFailure("database is locked".to_string()));
                }
                Ok(turso_core::StepResult::Interrupt) => {
                    return Err(Error::SqlExecutionFailure("interrupted".to_string()));
                }
                Ok(turso_core::StepResult::Row) => {
                    return Err(Error::SqlExecutionFailure("unexpected row during rollback".to_string()));
                }
                Err(err) => {
                    return Err(err.into());
                }
            }
        }
    });

    TursoFFIResult::from_result(result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in rollback_transaction".to_string()))))
}

#[no_mangle]
pub extern "C" fn turso_connection_is_autocommit(
    connection_ptr: *mut c_void,
    result: *mut bool,
) -> TursoFFIResult {
    if connection_ptr.is_null() || result.is_null() {
        return TursoFFIResult::error("Invalid parameters");
    }

    let query_result = std::panic::catch_unwind(|| {
        let connection_wrapper = unsafe { &*(connection_ptr as *const ConnectionWrapper) };
        
        let conn = connection_wrapper.connection.lock()
            .map_err(|e| Error::MutexError(e.to_string()))?;

        let autocommit = conn.get_auto_commit();
        unsafe { *result = autocommit };
        Ok(())
    });

    TursoFFIResult::from_result(query_result.unwrap_or_else(|_| Err(Error::SqlExecutionFailure("Panic in is_autocommit".to_string()))))
}

// Memory management
#[no_mangle]
pub extern "C" fn turso_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe {
            let _ = CString::from_raw(ptr);
        }
    }
}

#[no_mangle]
pub extern "C" fn turso_free_error_message(result: *mut TursoFFIResult) {
    if !result.is_null() {
        unsafe {
            let result_ref = &mut *result;
            if !result_ref.error_message.is_null() {
                let _ = CString::from_raw(result_ref.error_message);
                result_ref.error_message = ptr::null_mut();
            }
        }
    }
}