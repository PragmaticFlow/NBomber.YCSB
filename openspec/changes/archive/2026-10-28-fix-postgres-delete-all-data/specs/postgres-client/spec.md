# Spec Delta

## Purpose

Defines the observable behaviour of the PostgreSQL backend of the YCSB benchmark, so that Postgres runs start from a known data set and stay comparable with other databases.

## ADDED Requirements

### Requirement: Reset benchmark data
The PostgreSQL client's delete-all-data operation SHALL remove every record from the benchmark table, so that a subsequent bulk load produces exactly the configured record count with freshly generated field values.

#### Scenario: Leftover records from a previous run are removed
- **WHEN** the benchmark table already contains records from an earlier run, including keys that the next bulk load will insert again
- **THEN** after delete-all-data succeeds the table contains no records, and the following bulk load stores, for every key, exactly the newly generated field set and values (no fields or values from the earlier run remain)

#### Scenario: Reset on an empty table
- **WHEN** delete-all-data runs on an existing, empty benchmark table
- **THEN** the operation succeeds and the table remains empty

### Requirement: Reset is limited to the benchmark table
Delete-all-data SHALL affect only the benchmark table's records. It MUST NOT remove or alter other tables, schemas or databases, and MUST keep the benchmark table's structure and indexes so the run can continue without re-initialisation.

#### Scenario: Other data is untouched
- **WHEN** the database contains other tables alongside the benchmark table
- **THEN** delete-all-data leaves those tables and their rows unchanged

#### Scenario: Table remains usable after reset
- **WHEN** delete-all-data succeeds
- **THEN** the benchmark table still exists with the same columns and indexes, and inserts, reads and updates succeed without running initialisation again

### Requirement: Reset reports its outcome
Delete-all-data SHALL return a success response when the records were removed. When they cannot be removed it SHALL write the underlying error to the error output and propagate the error, so the benchmark initialisation fails rather than continuing with stale data.

#### Scenario: Successful reset
- **WHEN** delete-all-data removes all records
- **THEN** it returns a success response

#### Scenario: Failed reset
- **WHEN** the records cannot be removed, for example because a database connection cannot be opened or the statement is rejected (insufficient privilege, lock timeout)
- **THEN** the error message is written to the error output and the error is propagated to the caller
