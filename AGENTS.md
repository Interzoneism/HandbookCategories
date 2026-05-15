## Context
- Vintage Story API and decompiled source code live in `Decompiled_VS_1.22_SourceCode/`
- We only work with the latest stable Vintage Story version 1.22 - all the code in Decompiled_VS_1.22_SourceCode are from the 1.22 version so you can trust it completely.
- The folder "refcode 1.21" is ONLY to compare in order to see what changed from 1.21 to 1.22.

## Instructions
- When changing or using functions, methods, classes, variables or other things from the Vintage Story API or source, always check the corresponding file in Decompiled_VS_1.22_SourceCode/

## Build & test
- Do not build or test Cake / ZZCakeBuild / Program.cs
- Ignore all warnings about NET7 compatiblity or legacy net 7 warnings
- Build: `dotnet build -nologo -clp:Summary -warnaserror`
- Test: `dotnet test --nologo --verbosity=minimal`
- Lint (optional): `dotnet format --verify-no-changes`

## Project
- Solution: `Enhanced Handbook.sln`
