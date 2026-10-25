## 1. Isolate and fix

- [x] 1.1 Preserve generated IL2CPP boxing evidence and reproduce the warmed allocation failure with governed output/allocation tests.
- [x] 1.2 Replace the boxed comparison in both Slim fill methods, preserving all-zero color fallback and other output behavior.

## 2. Validate

- [x] 2.1 Pass full local Unity regression, layout and test governance; preserve source hashes and results.
- [x] 2.2 Build the fixed Release IL2CPP ARM64 APK, verify generated fill code no longer boxes colors, and collect old/new/new/old device reports.
- [x] 2.3 Analyze matched reports, record limitations and update roadmap/TODO with the decision and next task.
