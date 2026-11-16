# core-workload Specification

## Purpose

Defines the shared YCSB Core workload behaviour that applies to every database backend, so that workloads generate the same operations, keys and request sizes as the original YCSB.

## Requirements

### Requirement: Uniform scan length
Each Scan operation SHALL request a record count drawn uniformly at random from the inclusive range minscanlength..maxscanlength, independently for every scan.

#### Scenario: Scan length stays within bounds
- **WHEN** a workload performs scans with minscanlength 1 and maxscanlength 100
- **THEN** every scan requests between 1 and 100 records, and over many scans both 1 and 100 are requested

#### Scenario: Equal bounds give a fixed length
- **WHEN** minscanlength and maxscanlength are both 10
- **THEN** every scan requests exactly 10 records

### Requirement: Scan length defaults per workload
When no scan length is configured, minscanlength SHALL be 1, and maxscanlength SHALL be 100 for workload E and 1000 for every other workload, matching the original YCSB defaults and workload files.

#### Scenario: Workload E defaults
- **WHEN** workload E runs without minscanlength or maxscanlength
- **THEN** scans request between 1 and 100 records

#### Scenario: Default for other workloads
- **WHEN** any workload other than E is configured without minscanlength or maxscanlength
- **THEN** the effective scan length range is 1 to 1000

### Requirement: Explicit scan length overrides
An explicitly configured minscanlength or maxscanlength SHALL replace the corresponding default for any workload.

#### Scenario: Override maximum for workload E
- **WHEN** workload E runs with maxscanlength 500
- **THEN** scans request between 1 and 500 records

#### Scenario: Override minimum only
- **WHEN** workload E runs with minscanlength 20 and no maxscanlength
- **THEN** scans request between 20 and 100 records

### Requirement: Scan length validation
The configuration SHALL be rejected before the benchmark starts unless 1 <= minscanlength <= maxscanlength, where maxscanlength is the explicit value or the workload default.

#### Scenario: Minimum below one
- **WHEN** minscanlength is 0
- **THEN** validation fails with a message naming minscanlength and the benchmark does not run

#### Scenario: Minimum above maximum
- **WHEN** minscanlength is 50 and maxscanlength is 10
- **THEN** validation fails with a message naming both settings

#### Scenario: Minimum above workload default maximum
- **WHEN** workload E is configured with minscanlength 200 and no maxscanlength
- **THEN** validation fails because the effective maximum is 100

### Requirement: Operation latency covers only the database call
For every operation (read, read latest, update, insert, scan, read-modify-write), the key, field set, values and scan length SHALL be prepared before the operation's latency measurement starts, so the measured latency covers only the database call, as in original YCSB.

#### Scenario: Insert inputs prepared before timing
- **WHEN** an insert operation runs
- **THEN** its key and all field values are generated before the insert's measured step starts, and the step only sends them to the database

#### Scenario: Read inputs prepared before timing
- **WHEN** a read operation with readallfields false runs
- **THEN** its key and the single field name are chosen before the read's measured step starts

#### Scenario: Operation names and mix unchanged
- **WHEN** any workload A-F runs
- **THEN** the operations are reported under the same names and in the same proportions as before

### Requirement: Generated field values
Each generated field value SHALL be exactly fieldlength characters, each a lowercase letter a to z.

#### Scenario: Default field length
- **WHEN** a record is generated with fieldlength 100
- **THEN** every field value is 100 characters long and contains only the letters a to z

#### Scenario: Field length of one
- **WHEN** a record is generated with fieldlength 1
- **THEN** every field value is a single lowercase letter

### Requirement: Field set of generated records
An inserted record and an update with writeallfields true SHALL contain exactly the fields field1 to fieldN, where N is fieldcount, each with a generated value.

#### Scenario: Full record
- **WHEN** a record is generated with fieldcount 10
- **THEN** it has exactly the fields field1 to field10

### Requirement: Single field selection
When readallfields is false a read SHALL request one field, and when writeallfields is false an update SHALL write one field, each chosen uniformly at random from field1 to fieldN.

#### Scenario: Selection stays within the field set
- **WHEN** many single-field reads and updates are generated with fieldcount 10
- **THEN** every chosen field is one of field1 to field10, and both field1 and field10 are chosen

#### Scenario: Single field record
- **WHEN** fieldcount is 1 and a single field is chosen
- **THEN** the chosen field is field1

### Requirement: Concurrent data generation
Generation of keys, field names and values SHALL be safe and independent across concurrent scenario copies, without a lock shared by all copies.

#### Scenario: Parallel generation
- **WHEN** many records and single-field choices are generated in parallel from several threads
- **THEN** no exception occurs and every result satisfies the field value and field set requirements

### Requirement: Reads target only acknowledged inserts
Read, read latest, update, scan and read-modify-write operations SHALL choose keys only up to the acknowledged key limit: the highest key such that it and every key below it are either part of the initial load or acknowledged inserts. A key reserved by an insert that has not been acknowledged SHALL NOT be chosen.

#### Scenario: Key reserved but not acknowledged
- **WHEN** the initial load has keys 1 to 1000 and an insert has reserved key 1001 but not yet acknowledged it
- **THEN** read latest, read, update, scan and read-modify-write never choose key 1001

#### Scenario: Newest acknowledged key is reachable by read latest
- **WHEN** inserts for keys 1001 and 1002 are both acknowledged
- **THEN** read latest can choose key 1002 and never chooses a key above 1002

### Requirement: Insert key acknowledgement
Each insert SHALL reserve a new key before its measured database call and acknowledge that key after the call completes, whether the insert succeeded or failed, matching original YCSB.

#### Scenario: Successful insert
- **WHEN** an insert of key 1001 succeeds
- **THEN** key 1001 is acknowledged

#### Scenario: Failed insert
- **WHEN** an insert of key 1001 returns a failure or throws
- **THEN** key 1001 is still acknowledged, so later inserts can advance the limit past it

### Requirement: Acknowledged limit advances over contiguous keys
The acknowledged key limit SHALL advance only over a contiguous run of acknowledged keys starting right after the current limit; a key acknowledged out of order SHALL become readable only when every lower reserved key is acknowledged.

#### Scenario: Out-of-order acknowledgement
- **WHEN** keys 1001, 1002 and 1003 are reserved after an initial load of 1000 records, and 1003 is acknowledged first
- **THEN** the limit stays 1000; after 1001 is acknowledged it is 1001; after 1002 is acknowledged it is 1003

#### Scenario: No inserts
- **WHEN** a workload without inserts runs after an initial load of 1000 records
- **THEN** the limit is 1000 for the whole run

### Requirement: Unique insert keys under concurrency
Concurrent inserts SHALL reserve distinct, consecutive keys starting right after recordcount, and concurrent acknowledgements SHALL leave the limit equal to the highest reserved key once all are acknowledged.

#### Scenario: Parallel reservation and acknowledgement
- **WHEN** 10000 inserts reserve and acknowledge keys in parallel after an initial load of 1000 records
- **THEN** the reserved keys are exactly 1001 to 11000, each once, and the limit ends at 11000
