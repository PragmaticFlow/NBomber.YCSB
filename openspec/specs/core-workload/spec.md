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
