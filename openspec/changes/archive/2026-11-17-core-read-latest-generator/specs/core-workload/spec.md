## ADDED Requirements

### Requirement: Read latest key distribution
Read latest SHALL choose its key like YCSB's `SkewedLatestGenerator`: with n the acknowledged key limit, a rank r SHALL be drawn from YCSB's Zipfian draw (Gray et al.) with constant 0.99 over n items using ζ(n, 0.99), and the key SHALL be n − r, between 1 and n. The newest acknowledged key SHALL be the most likely key.

#### Scenario: Newest key is the hottest
- **WHEN** workload D runs with recordcount 1000 and no acknowledged inserts
- **THEN** key 1000 is chosen in about 12.9% of read-latest operations (1/ζ(1000, 0.99)), key 999 in about 6.5%, and no key outside 1..1000 is chosen

#### Scenario: Newly acknowledged key becomes the hottest
- **WHEN** an insert of key 1001 is acknowledged
- **THEN** key 1001 becomes the most likely read-latest key and no key above 1001 is chosen

#### Scenario: Single record
- **WHEN** recordcount is 1 and no insert is acknowledged
- **THEN** read latest always chooses key 1

#### Scenario: Missing key
- **WHEN** a chosen key is not found by the database
- **THEN** the operation returns `Response.Fail(statusCode: "no data")`, as for any read

### Requirement: Read latest key choice cost
Choosing a read-latest key SHALL take constant time, independent of the number of keys, and SHALL NOT take a lock. ζ(recordcount, 0.99) SHALL be computed when the key generator is created, before any measured operation. When the acknowledged key limit grows, ζ SHALL be extended, not recomputed, at a cost proportional to the number of newly acknowledged keys. Each draw SHALL use a ζ that matches the n used for the draw.

#### Scenario: Large dataset
- **WHEN** workload D runs with recordcount 1,000,000
- **THEN** each read-latest key choice costs microseconds or less, not time proportional to 1,000,000

#### Scenario: Concurrent growth
- **WHEN** several scenario copies choose read-latest keys while inserts are acknowledged
- **THEN** every draw uses a consistent (n, ζ(n)) pair and returns a key between 1 and that n

## MODIFIED Requirements

### Requirement: Zipfian keys above the acknowledged limit are redrawn
A zipfian key above the acknowledged key limit, or below 1, SHALL be redrawn until it is between 1 and the limit, so the distribution over existing keys keeps its shape. Creating the key generator with recordcount 0 for a workload that chooses zipfian or read-latest keys (A, B, C, D, E, F) SHALL fail with an error naming recordcount; no key SHALL be returned for an empty key range.

#### Scenario: Redraw is deterministic
- **WHEN** recordcount is 3 (item count 4) and rank 0 maps to key 4
- **THEN** key 4 is redrawn and the key for the next rank is returned

#### Scenario: Unacknowledged keys never chosen
- **WHEN** workload E has recordcount 1000, item count 1101 and acknowledged key limit 1000
- **THEN** every chosen key is between 1 and 1000

#### Scenario: Acknowledged new keys become reachable
- **WHEN** the acknowledged key limit grows to 1100
- **THEN** keys between 1001 and 1100 can be chosen and no key above 1100 is chosen

#### Scenario: Empty dataset rejected
- **WHEN** workload A, B, C, D, E or F is set up with recordcount 0
- **THEN** creation fails with an error naming recordcount before any database operation
