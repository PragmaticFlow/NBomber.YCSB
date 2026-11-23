## Purpose

Defines the observable behaviour of the Aerospike backend of the YCSB benchmark: its concurrency setting, record-level operation semantics and the unsupported scan, so Aerospike runs stay consistent with the other database clients.

## ADDED Requirements

### Requirement: Configurable client concurrency
The Aerospike client SHALL read `aerospike.maxconcurrency` from the run's props, defaulting to 512. The value SHALL bound the commands in flight, the connections per server node and the parallelism of the initial bulk load. Commands beyond the bound SHALL wait on the client instead of being rejected.

#### Scenario: Default concurrency
- **WHEN** a run sets no `aerospike.maxconcurrency`
- **THEN** at most 512 commands are in flight and the bulk load inserts at most 512 records at a time

#### Scenario: Custom concurrency
- **WHEN** a run sets `aerospike.maxconcurrency=64`
- **THEN** at most 64 commands are in flight and the bulk load inserts at most 64 records at a time

#### Scenario: More threads than the limit
- **WHEN** the run's thread count exceeds the configured limit
- **THEN** the extra operations wait on the client instead of being rejected, and the wait is part of their measured latency

### Requirement: Invalid concurrency is rejected
The default of 512 SHALL apply only when `aerospike.maxconcurrency` is absent. An explicitly provided value that is not a positive integer SHALL be rejected with an error naming the setting and the given value, before any connection to the server is made.

#### Scenario: Zero or negative value
- **WHEN** a run sets `aerospike.maxconcurrency=0` or `-5`
- **THEN** client creation fails with an error that names `aerospike.maxconcurrency`

#### Scenario: Non-numeric or empty value
- **WHEN** a run sets `aerospike.maxconcurrency=abc` or an empty value
- **THEN** client creation fails with an error that names `aerospike.maxconcurrency` instead of using the default

### Requirement: Insert creates only new records
Insert SHALL create the record and SHALL fail when a record with the key already exists, leaving the stored record unchanged.

#### Scenario: Duplicate key
- **WHEN** an insert targets a key that already exists
- **THEN** the operation fails and the original record keeps its values

### Requirement: Update and read-modify-write are partial
Update and the write of read-modify-write SHALL merge the given fields into the existing record and keep all other fields. On a missing key they SHALL return `Response.Fail(statusCode: "no data")` and create nothing.

#### Scenario: Update one field
- **WHEN** `field2` of an existing record is updated
- **THEN** `field2` has the new value and every other field keeps its value

#### Scenario: Update a missing key
- **WHEN** an update or read-modify-write targets a key that does not exist
- **THEN** it returns `Response.Fail(statusCode: "no data")` and no record is created

### Requirement: Bulk load replaces records
The initial bulk load SHALL create each record or fully replace an existing record with the same key, so a repeated initialisation stores exactly the generated fields.

#### Scenario: Existing record with extra fields
- **WHEN** the bulk load writes a key that already holds a record with other fields
- **THEN** the record holds exactly the newly generated fields

### Requirement: Scan is not supported
Scan SHALL return `Response.Fail(statusCode: "scan not supported")` without reading data, and a run of workload E with `--db aerospike` SHALL be rejected before it starts.

#### Scenario: Scan call
- **WHEN** a scan is requested
- **THEN** it returns `Response.Fail(statusCode: "scan not supported")` and reads no records

#### Scenario: Workload E
- **WHEN** a run uses `--db aerospike --workload E`
- **THEN** validation fails with a message that Aerospike does not support scan
