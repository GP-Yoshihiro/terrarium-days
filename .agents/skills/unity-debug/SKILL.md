---
name: unity-debug
description: Diagnose one reproducible Unity compiler, test, or runtime failure without expanding scope.
---

# Unity debugging workflow

Input required: one failing command or reproducible interaction, plus the smallest relevant error excerpt.

1. Restate the observed failure and expected behavior.
2. Inspect the named file and its direct callers only.
3. Form one primary hypothesis and make the smallest change that tests it.
4. Rerun the same narrow command or interaction.
5. If it still fails, report evidence and the next hypothesis; do not perform broad speculative rewrites.

Never erase saves, generated project files, or user work to make a failure disappear.
