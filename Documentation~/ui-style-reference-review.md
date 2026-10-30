# UI style reference coherence

Discover `uitoolkit/review-style-references` through the bounded catalog and read
its exact registered contract. Pass up to eight explicit Assets-relative USS/UXML
paths, with optional `includeMatchingReferences`. A UXML contributes only its own
inline sheet; list its imported USS/templates explicitly when needed.

The review snapshots cached managed wrappers before creating SerializedObject,
then compares native serialized references. Each pair retains CLR nullness,
Unity liveness, concrete type, shared cross-version object identity and available
GUID/local ID. Array shape and engine field mismatches reject rather than falling
back to text parsing or inferred values. Nothing is imported, saved or repaired.

`passed` certifies reference coherence for those sheets at that instant. It does
not certify image availability, the winning selector, live UI or visual quality.
Pair it with the actual runtime resource audit, explicit forbidden warning-image
checks, native screenshots and the full warning/error interval. The detailed
budget is in [the cost ledger](ui-style-reference-cost-ledger.md).
