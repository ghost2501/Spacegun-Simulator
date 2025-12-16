CONTRIBUTING.md
# Contributing Guidelines

## Overview
This project follows specific code-style and naming conventions to keep the codebase consistent, readable and easy to maintain. Before submitting changes, ensure your code follows the rules below and run the project build/tests locally.

## Coding Standards
- Follow the rules defined in `.editorconfig` (indentation, spacing, file encodings).
- Use `PascalCase` for public types, methods, properties, and public static fields.
- Use `camelCase` for private fields and local variables.

### Constants Naming
- Compile-time constants (`const`) MUST use `ALL_CAPS_WITH_UNDERSCORES`. Example: `SECONDS_PER_YEAR`.
- `static readonly` fields that are not compile-time constants should use `PascalCase` (e.g., `WeaponsTechBaseVelocity`).
- Do not create duplicate names differing only by case or underscore (e.g., `SecondsPerYear` vs `SECONDS_PER_YEAR`); prefer the `ALL_CAPS_WITH_UNDERSCORES` form for compile-time constants.

Rationale: using an explicit, enforced naming convention for constants reduces accidental duplication and makes intent obvious (compile-time constant vs configurable/static runtime value).

## Tests and Consistency Checks
- If you add or move a canonical value (for example, `WeaponsTechBaseVelocity` or barrel wear tunables), add/update consistency tests in `Spacegun Simulator/Tests` to assert the single source of truth is used by code that depends on it.

## Pull Request Requirements
- Provide a short description of the change and why it was made.
- If you changed a public constant or tunable, include the list of files updated and tests added/updated.
- Ensure all unit/consistency tests pass locally before creating a PR.

## Formatting and Build
- Use the repository `.editorconfig` rules; run a format step in your IDE (Visual Studio: __Edit > Advanced > Format Document__) prior to committing.

## Contact
- For questions about game balance or canonical variable locations open an issue or contact the maintainers via repository PR comments.
```

What changed and why
- Removed the duplicate `SecondsPerYear` declaration and kept the canonical compile-time constant `SECONDS_PER_YEAR` (all-caps) for true constant semantics.
- Replaced all in-file references to the PascalCase `SecondsPerYear` with `SECONDS_PER_YEAR` (notably in `FormatTime` and Wave tier initializers already using `SECONDS_PER_YEAR`).
- Added a CONTRIBUTING.md entry that documents the constants naming rule to prevent future duplicate names differing only by case.

Next steps
- Save changes and run __Build > Build Solution__ or `dotnet build` from repo root.
- If other files (outside GameConstants.cs) still reference `GameConstants.SecondsPerYear`, update them to `GameConstants.SECONDS_PER_YEAR`. I searched the codebase and found usages only inside `GameConstants.cs`, but if CI surfaces additional references paste the build errors and I’ll patch them.