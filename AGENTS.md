## Context
- Vintage Story API and decompiled source code for the supported game version live in `Vintage Story API 1.22.6/`.
- We only work with Vintage Story 1.22.6. Treat that folder as the current API/source reference.
- Older reference folders are comparison-only and must not be used as the implementation contract.

## Instructions
- When changing or using functions, methods, classes, variables, or other Vintage Story API/source symbols, always check the corresponding file in `Vintage Story API 1.22.6/`.
- Follow `Vintage Story API 1.22.6/AGENTS.md`; the reference tree is read-only and must not be built or edited.

## Build & test
- Do not build or test Cake / ZZCakeBuild / Program.cs
- Ignore all warnings about NET7 compatiblity or legacy net 7 warnings
- Build: `dotnet build -nologo -clp:Summary -warnaserror`
- Test: `dotnet test --nologo --verbosity=minimal`
- Lint (optional): `dotnet format --verify-no-changes`

## Project
- Solution: `Enhanced Handbook.sln`
