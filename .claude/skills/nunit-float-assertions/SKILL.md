---
name: nunit-float-assertions
description: When an NUnit Assert.That(double/float, Is.EqualTo(...)) needs a .Within(tolerance) modifier versus when exact equality is safe. Consult when writing a new EditMode/PlayMode test that asserts on a double/float produced by decay, growth, accumulated-time, or UI-pixel math.
---

# NUnit float/double equality: when `.Within()` is required

## The default is exact (zero-tolerance) equality, not a "close enough" comparison

Official NUnit docs: **"Values of type float and double are normally compared using a tolerance specified by the Within modifier"** — but that tolerance only applies if you actually add `.Within(...)`. Without it: **"Float and double comparisons for which no tolerance is specified use a default value... If this is not in place, a tolerance of 0.0d is used."** This project doesn't configure `DefaultFloatingPointToleranceAttribute` anywhere, so every bare `Assert.That(someDouble, Is.EqualTo(x))` in this codebase is a bit-exact comparison, not a fuzzy one — it only passes today because of *which* values are being compared, not because NUnit is forgiving by default.

## This project's existing tests already draw the right line — know why, so new tests draw it the same way

Grep across `Assets/Tests/` shows both styles in active use, and the split is not arbitrary:

**Exact `Is.EqualTo(...)` is safe** when the asserted value is a literal/direct assignment, or a single clean arithmetic step whose inputs and output are exactly representable — e.g. `Assert.That(view.State.Hunger, Is.EqualTo(80d))` after a slider directly sets a field, or `Assert.That(state.Hydration, Is.EqualTo(50d))` when nothing touched that field. IEEE-754 double addition/subtraction of exactly-representable decimal values (whole numbers, halves, quarters — the kind of round tuning numbers this project's `CareTuning` uses) is itself exact, so a single `50d + tuning.FeedHungerAmount` is safe without `.Within()` too, as this project's `CareServiceTests` assumes.

**`.Within(tolerance)` is required** once a value has gone through repeated/compounded floating-point math — this project uses it precisely where that's true:
- `Assert.That(view.State.Hunger, Is.EqualTo(80d - tuning.HungerDecayPerHour).Within(1e-6))` — decay applied via the minute-stepped offline-progress/live-tick loop (many small accumulated operations, not one clean subtraction).
- `Assert.That(growthGaugeFill.style.width.value.value, Is.EqualTo(50f).Within(0.01f))` — a UI Toolkit rendered pixel width, itself a `float` (narrower than `double`) derived from a percentage calculation with intermediate casts; the looser `0.01f` tolerance (vs. the `1e-6` used for core gameplay math) reflects that this value crossed a `double`→`float` narrowing cast and a rendering-layer calculation, not just repeated arithmetic — pick the tolerance to match how many lossy conversions the value actually passed through, not a single default number copy-pasted everywhere.

## Rule of thumb for a new test

Ask **"did this value accumulate through a loop, cross a `double`→`float` cast, or pass through any calculation this project doesn't fully control (rendering, a third-party API)?"** — if yes, use `.Within(...)` sized to the actual precision need (tight, e.g. `1e-6`, for gameplay math staying in `double`; looser, e.g. `0.01f`, for anything that touched UI Toolkit's `float`-based layout system). If the value is a direct field assignment or one exact-representable arithmetic step, bare `Is.EqualTo(...)` is correct and adding an unnecessary `.Within()` would only hide a real regression (a future bug that shifts the value by more than the tolerance) behind a false pass.

## Sources

- [NUnit docs: EqualConstraint](https://docs.nunit.org/articles/nunit/writing-tests/constraints/EqualConstraint.html) — "Values of type float and double are normally compared using a tolerance specified by the Within modifier"; default-tolerance-is-0.0d-without-one statement, quoted above.
- Patterns cross-checked against this repo's own passing test suite: `Assets/Tests/PlayMode/TerrariumViewTests.cs`, `Assets/Tests/EditMode/CareServiceTests.cs`.
