# Command effect ownership

The Automation route profile is the authority for a command's effects and mode requirements. Catalog construction publishes both the existing asset/runtime flags and explicit effects for operations such as build output, preferences, job cancellation and debugger execution. A consumer may not replace an absent effect list with a read-only claim.

The reproduced `build/start` contract had no asset/runtime effect flags. Pipeline converted that absence into `read`, although the build owner writes output files and can launch a Player. The same conversion affected the other routes whose effects do not belong to asset or gameplay mutation. Build polling also accepts `clear`, so its contract must declare job-history mutation and require an explicit project binding.

Entry: built-in route registration. Owner: VmAutomationToolProfileCatalog. Producer: a profile with an immutable effect list. Publication: VmAutomationCatalog's existing metadata product. Consumer: the Pipeline contract adapter and callers inspecting that contract. Lifetime: one immutable catalog generation, retired at domain reload. Missing/contradictory metadata belongs to the producer; the adapter preserves supplied effects exactly. Runtime handlers and authored project content are unchanged.

Static Cost Ledger: at most 1,024 existing route profiles, at most four declared additional effects per affected route, and at most 4,096 effect strings copied during catalog initialization. No new asset scan, Unity build, runtime loop or retained job. Existing normalized catalog publication owns sorting and serialization. Focused tests inspect the build start/get contracts, read-only preservation, immutable input ownership and the bounded built-in profile set. No test builds a Player. Pass.

Build start requires stable Edit Mode. Its public effects include build output writes, process launch and potential domain reload. Build polling declares its optional durable-history cleanup. The build owner's existing overwrite/run options remain explicit in its input schema. These declarations describe the command's possible effects, not a claim that every invocation uses every option.
