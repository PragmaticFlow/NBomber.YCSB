# postgres-client Specification

## Purpose

Defines the observable behaviour of the PostgreSQL backend of the YCSB benchmark, so that Postgres runs start from a known data set and stay comparable with other databases.

## Requirements

### Requirement: Reset benchmark data
The PostgreSQL client's delete-all-data operation SHALL drop the benchmark table, removing every record, so that after initialisation a subsequent bulk load produces exactly the configured record count with freshly generated field values.

#### Scenario: Leftover records from a previous run are removed
- **WHEN** the benchmark table already contains records from an earlier run, including keys that the next bulk load will insert again
- **THEN** after delete-all-data and initialisation succeed the table contains no records, and the following bulk load stores, for every key, exactly the newly generated field set and values (no fields or values from the earlier run remain)

#### Scenario: Reset on an empty table
- **WHEN** delete-all-data runs on an existing, empty benchmark table
- **THEN** the operation succeeds and, after initialisation, the table is empty

#### Scenario: Reset when the table does not exist
- **WHEN** delete-all-data runs and the benchmark table does not exist
- **THEN** the operation succeeds

### Requirement: Reset is limited to the benchmark table
Delete-all-data SHALL drop only the benchmark table. It MUST NOT remove or alter other tables, schemas or databases. Initialisation SHALL create the benchmark table with the YCSB schema (primary key and JSONB value column, no other index) when it does not exist.

#### Scenario: Other data is untouched
- **WHEN** the database contains other tables alongside the benchmark table
- **THEN** delete-all-data leaves those tables and their rows unchanged

#### Scenario: Table remains usable after reset
- **WHEN** delete-all-data and then initialisation succeed
- **THEN** the benchmark table exists with the primary-key and JSONB columns and no other index, and inserts, reads and updates succeed

#### Scenario: Leftover index is removed
- **WHEN** the benchmark table has an extra index from an earlier version and delete-all-data then initialisation run
- **THEN** the benchmark table has only its primary key

### Requirement: Reset reports its outcome
Delete-all-data SHALL return a success response when the records were removed. When they cannot be removed it SHALL write the underlying error to the error output and propagate the error, so the benchmark initialisation fails rather than continuing with stale data.

#### Scenario: Successful reset
- **WHEN** delete-all-data removes all records
- **THEN** it returns a success response

#### Scenario: Failed reset
- **WHEN** the records cannot be removed, for example because a database connection cannot be opened or the statement is rejected (insufficient privilege, lock timeout)
- **THEN** the error message is written to the error output and the error is propagated to the caller

### Requirement: Schema matches YCSB
The benchmark table SHALL have only the primary-key column and the JSONB value column, with no secondary index on the value. Initialisation SHALL NOT create an index on the value column. An index `idx_ycsb_table_jsonb` left by a previous version is removed by the reset, which drops the table; initialisation recreates it without the index (change scope-delete-all-data).

#### Scenario: Fresh database
- **WHEN** initialisation runs and the benchmark table does not exist
- **THEN** the table is created with the primary key only and no index on the JSONB column

### Requirement: Prepared statements
Repeated statements SHALL be prepared on the server after repeated use (automatic preparation after 5 uses), and projected reads SHALL use the same SQL text whichever field is requested.

#### Scenario: Projected reads of different fields
- **WHEN** reads request `field1` and later `field7`
- **THEN** both use the same SQL text with the field name as a parameter

### Requirement: Update reports a missing record
Update and the write phase of read-modify-write SHALL return `Response.Fail(statusCode: "no data")` when no row has the given key, and SHALL keep merging the given fields into the stored value otherwise.

#### Scenario: Update a missing key
- **WHEN** an update targets a key that does not exist
- **THEN** the operation returns `Response.Fail(statusCode: "no data")` and no row is created

#### Scenario: Update keeps other fields
- **WHEN** `field2` of an existing record is updated
- **THEN** `field2` has the new value and every other field keeps its value
