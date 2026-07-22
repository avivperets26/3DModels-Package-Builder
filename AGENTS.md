# Package Builder Project Rules

These rules apply permanently to the entire `C:\Dev\PackageBuilder` tree unless the user explicitly replaces or amends them.

1. `C:\Dev\PackageBuilder` is the single project root.
2. All source code, documentation, downloads, local tools, runtime data, logs, fixtures, and generated artifacts must remain beneath this root.
3. Never use `C:\Dev\PackageBuilderData` or scatter project files elsewhere.
4. Keep large local tools, downloads, logs, caches, and generated artifacts out of Git using `.gitignore`.
5. The required stack must have a free local or self-hosted workflow. No paid IDE, library, SaaS service, or subscription may be mandatory.
6. Unity and Unreal integrations must clearly disclose their external licensing, eligibility, seat, and royalty conditions.
7. The repository must build, test, run, and debug from Visual Studio Code without requiring paid Visual Studio or paid extensions.
8. Use the latest approved stable/LTS versions of .NET, Blender, Unity, and Unreal. Never use preview versions for production without explicit approval.
9. Pin every approved tool version so builds are reproducible.
10. Read these documents before making architectural or implementation changes:
    - `docs/Package_Builder_Plan.md`
    - `docs/TECH_STACK_AND_ARCHITECTURE.md`
    - `docs/IMPLEMENTATION_BACKLOG.md`
11. Work on one PB task per branch using its documented branch name.
12. Do not mark a task complete until its acceptance criteria pass, tests pass, the branch is pushed, GitHub CI passes, and the work is merged, or an approved exception is documented.
13. Record completed work in the backlog completion log.
14. Preserve unrelated user changes and never use destructive Git or filesystem commands without explicit authorization.
15. Validate files, paths, names, textures, rigs, animations, and package contents before publishing.
16. Publisher names such as `AvivPeretsFBX` must be configuration values and never hard-coded.
17. Keep platform-specific logic behind separate portable, Unity, Unreal, and marketplace adapters.
18. Prefer deterministic, repeatable builds and produce validation reports for every package.
