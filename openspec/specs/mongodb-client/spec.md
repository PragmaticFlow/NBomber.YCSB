# mongodb-client Specification

## Purpose
Defines the observable behaviour of the MongoDB backend of the YCSB benchmark, so that MongoDB runs follow the Java YCSB `mongodb` binding and stay comparable with other databases.

## Requirements

### Requirement: Flat record layout
Each record SHALL be stored as one document with `_id` set to the key and one top-level field per YCSB field (`field1..fieldN`), with no other fields.

#### Scenario: Inserted document
- **WHEN** a record with fields `field1..field10` is inserted
- **THEN** the stored document contains exactly `_id` and `field1..field10`

### Requirement: Ordered scan from the start key
Scan SHALL return up to `count` records whose `_id` is greater than or equal to the start key, in ascending `_id` order, with the requested fields (all fields when none are requested). A scan that finds no record SHALL return `Response.Fail(statusCode: "no data")`.

#### Scenario: Scan returns the requested number of records
- **WHEN** keys `k1 < k2 < ... < k10` exist and a scan starts at `k3` with count 5
- **THEN** the records `k3..k7` are read, in that order

#### Scenario: Scan near the end of the key range
- **WHEN** a scan starts at the second-largest key with count 5
- **THEN** only the two remaining records are read

### Requirement: Field projection
Read, scan and read-modify-write with a requested field set SHALL return exactly those fields (plus `_id`); with no requested fields they SHALL return all fields.

#### Scenario: Read one field
- **WHEN** a read requests `field3` of an existing record
- **THEN** the result contains `_id` and `field3` with its stored value

### Requirement: Missing records
Read, scan, update and read-modify-write on a missing key SHALL return `Response.Fail(statusCode: "no data")` without throwing.

#### Scenario: Read a missing key
- **WHEN** a read targets a key that does not exist
- **THEN** the operation returns `Response.Fail(statusCode: "no data")`

### Requirement: Partial update
Update and the write phase of read-modify-write SHALL set only the given fields and SHALL leave all other fields unchanged.

#### Scenario: Update one field
- **WHEN** `field2` of an existing record is updated
- **THEN** `field2` has the new value and every other field keeps its value

### Requirement: Acknowledged writes
The client SHALL use write concern `w:1`, the Java YCSB default.

#### Scenario: Client settings
- **WHEN** the client is created
- **THEN** its write concern is `w:1`

### Requirement: Reset is limited to the benchmark database
Delete-all-data SHALL drop only the benchmark database (`ycsb`), removing all its collections, and MUST NOT drop or alter any other database on the server.

#### Scenario: Other databases are untouched
- **WHEN** the server holds another database alongside `ycsb`
- **THEN** after delete-all-data that database and its collections are unchanged, and `ycsb` holds no documents

#### Scenario: Load after reset
- **WHEN** delete-all-data succeeds and the bulk load runs
- **THEN** the benchmark collection contains exactly the loaded records
