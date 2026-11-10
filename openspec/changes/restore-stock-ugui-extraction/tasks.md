## 1. Runtime compatibility

- [x] 1.1 Add stock public-API fallback while retaining custom instance-method dispatch.
- [x] 1.2 Add explicit and automatic scratch resource cleanup.

## 2. Regression coverage

- [x] 2.1 Add forced-fallback topology, offsets, invalid-range, empty-stream, vertex-limit, allocation and cleanup tests with stable governance records.
- [x] 2.2 Prepare reusable stock consumer validation and dependency provenance without launching Unity or changing the original dependency overrides.

## 3. Verification and records

- [x] 3.1 Run package structure, test governance, OpenSpec and diff checks; document compatibility and pending Unity verification.
- [x] 3.2 With editor launch authorization, run extraction tests and relevant regressions in original custom and clean stock environments; record real results and exact dependency sources.
- [x] 3.3 Review the entire TODO queue and close VERIFY-02 only when actual stock acceptance passes; record outcome in roadmap and changelog.
