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
