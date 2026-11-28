# Lane nth-kafkafix — Opus — worktree .worktrees/nth-kafkafix, base main 8e0993b — CRITICAL-PATH FIX, READY by 12:35
Read HEADER.md (incl. Addenda) and OWNERS.md (Addendum 13). pg port 55535 if you need your own. Use the superpowers
systematic-debugging discipline: reproduce, hypothesise, prove, then fix.

Symptom (nth-refactor lane + integrator, reproduced on main 38db5bd and the refactor head):
the full `dotnet test` run gets 766 passes in Fugue.Tests, then the TEST HOST CRASHES (aborts the whole run, not a red
test) in `tests/Fugue.Tests/Messaging/KafkaPublisherTests.cs` —
`With_no_broker_the_outbox_row_is_not_marked_dispatched_and_is_retried_later` — librdkafka logs
"connect to 127.0.0.1:1 refused". Run alone, Messaging passes 2/2. The same test PASSED in the integrator's pre-push
suite at 11:29. This crash now blocks the integrator's pre-push hook, so main cannot reach origin: you are the critical path.
Machine is on battery; docker clock = host clock (checked 11:50), so FM36 is not the explanation by itself.

Hypotheses to test (not conclusions): native librdkafka callback/handle used after the producer is disposed (delivery
or error handler firing on a disposed object, or Flush/Dispose racing the test's teardown); an unobserved exception on a
librdkafka background thread crashing the process; parallel collections sharing a producer; timing under a loaded host
(it only dies late in a full run). Use `--blame-crash` / `--blame-hang-timeout` and the crash dump/sequence file to see
which test and thread really die. Reproduce with the full Fugue.Tests project (the lock is required for that) or a
loop of the Messaging class plus load.

Fix rules: never skip, quarantine or delete the test, never `--no-verify`. If the bug is in product code
(`src/Fugue.Infrastructure/**/Kafka*` — e.g. disposal, handlers on disposed objects), fix it there with the test
proving it (commit `fix(messaging): ...`, AGENTS check-fix-has-test). If it is test-only (lifetime, isolation, a
bounded timeout config like message.timeout.ms / socket timeouts for the no-broker case), fix the test.
Proof: the FULL suite in ONE run under /tmp/fugue-dotnet-suite.lock finishes: paste `Passed! ... Total: 849` (or the
real number) + ArchTests 37. Ideally twice. Report the root cause with evidence (dump/sequence lines).
If not fixed by 12:35: report BLOCKED with the evidence so far, and the integrator records a known-issue D-row.
Report + READY per HEADER; send the READY line to overnight-lead AND aspnet-cc.
