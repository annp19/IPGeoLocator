# C Unit Test Chatmode for Google Test

Purpose
- Guide an automated agent to generate Google Test unit test files for C modules located in `input/`.
- Ensure tests are well-documented: purpose, inputs, expected outputs.
- Enforce macro handling rules: do not change macro definitions in `input/`; respect `#if/#ifdef` guards.
- Follow ISO 26262-6:2018 coverage goals: C0=100%, C1=100%, MC/DC=100%.

High-level process the agent must follow
1. Inspect `input/` and list available `<module>.c` and `<module>.h` files. Parse headers for function declarations.
2. For a chosen `<module>.c`, the agent must parse the source and header to determine:
   - Public functions to test (non-static functions declared in header).
   - Macro definitions in headers or a provided `config.h` in `input/`.
   - Conditional compilation regions (#if/#ifdef/#else/#endif) and their controlling macros.
3. Evaluate whether to generate mocks/stubs:
   - If the module calls external functions (from other modules) that are not available in `input/`, generate stubs/mocks in the test file.
   - If the external dependency is available under `input/`, prefer to include it and test behavior without altering its macro settings.
4. Do NOT change macro definitions from `input/` when generating tests. If code is protected by `#if (MACRO == yes)` and `MACRO` equals `no` in inputs, do not force it to `yes` to cover internal branches. Instead:
   - Document that the protected branch is not covered due to configuration.
   - Add test cases for the active configuration only.
5. For each public function, derive test inputs to meet:
   - C0 (statement coverage) 100%: ensure tests execute every statement for the active configuration.
   - C1 (branch coverage) 100%: include tests for both branch outcomes where compile-time macros permit run-time branching.
   - MC/DC 100%: design tests that show each condition independently affecting decisions (only for run-time boolean conditions; compile-time excluded if disabled by macros).
6. Format for generated test files:
   - `test_<module>.h`: forward declares helpers, mock function prototypes, and common fixtures (though we recommend using TEST(), not TEST_F()).
   - `test_<module>.cpp`: includes `gtest/gtest.h`, includes module header and any stubs, defines TEST cases, documents purpose/input/output for each test.
7. Documentation: for each TEST() block include a compact comment with:
   - Purpose: which branch/statement/decision this test validates.
   - Inputs: function arguments and any relevant global or macro settings.
   - Expected output: return value, side-effects, or state changes.

Templates and conventions
- Use `TEST(ModuleName, CaseName)` style per test.
- Keep one logical assertion per test focused on one requirement to help MC/DC reasoning.
- Name tests to reflect the target (e.g., `Add_PositiveOperands_ReturnsSum`).

Coverage guidance and MC/DC strategy
- Agent must attempt to produce input combinations that demonstrate each decision's effect independently.
- For decisions that depend only on compile-time macros that are disabled in `input/`, the agent must mark them as untestable under current config and list what macro value would be needed to test them.
- If MC/DC cannot be achieved because some conditions are compile-time-fixed, the agent must document the limitation and include the minimal set of tests covering all run-time conditions.

Build integration notes (suggested)
- Provide a CMake snippet for adding tests with GoogleTest. Example:
  - Add `FetchContent` for googletest in CMake, include `test_*.cpp` files into `add_executable` and `add_test`.

Safety and constraints
- Agent must never edit files under `input/`.
- Agent must not change macro definitions nor flip their values.
- Agent may create helper mock/stub files under `tests/` or `build/tests/` but must keep a mapping of generated files.

Output expected from the agent
- A set of `test_<module>.cpp` and `test_<module>.h` files per module requested.
- A short `README_tests.md` explaining how to build and run tests and where generated files live.
- A compact coverage report guidance describing how to measure coverage (lcov/gcov for C compiled with `-fprofile-arcs -ftest-coverage`).

Examples and edge cases
- If a function uses time or randomness, the agent should generate a stub to control those calls.
- For hardware I/O or OS-specific calls, replace with mocks that simulate behavior.

---

Guidelines for the human operator
- The agent will ask which module to test if multiple are present.
- Provide any missing headers or build flags in `input/build_flags.txt` if necessary.
