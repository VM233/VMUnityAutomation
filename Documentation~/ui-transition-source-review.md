# UI transition source inventory

Discover `uitoolkit/transition-source-review` and its current typed schema through
the official project-bound catalog. The same source service is available to
framework/project graph tooling. The public result separates full declared source
coverage from affected paths with adjacent calls. A local report may set
SelectedPassed; its Passed remains false. RuntimeAccepted is always false here.

The main-thread owner captures Unity's actual owning compilations. The worker
reconstructs each selected source's complete sibling source/reference/define
context under that native assembly identity and unsafe policy, inventories visibility,
content publication, hierarchy mutation and panel navigation, and retains the
resolved caller/construction links. Each structural link uses actual original
symbol definitions, distinguishes overloads and retains every call coordinate
in Locations, following Roslyn's SymbolCallerInfo representation. Anonymous and
local functions retain their declaration identity. Method kinds use actual enum
values rather than unspecified alias formatting. Source sites include coordinates, actual
target expressions and code. Helpers must declare their actual target expression;
direct mutations need a native motion/lifetime declaration. Constructor-only
initialization is distinct from subsequent UI publication. Source is checked
again before completion and the exact scope receives a digest.

Publication coverage follows actual anonymous/named callback symbols and their
synchronous call graph. Initialize is a separate runtime-checked unpublished
binding callback. A helper is covered only when every reviewed incoming
use is an initialization/publication callback or an already covered
caller. A deferred method-group binding never inherits its binding method's
publication context. Constructor-created lambdas are deferred, not initial
construction. Mismatched helper declarations cannot register their callbacks.
Async callbacks and calls cannot confer synchronous coverage. Actual override
declarations connect their OverriddenMethod original symbol to the overriding
symbol, so abstract binding methods do not hide derived mutations. An uncovered
call to that implementation or its helpers still rejects coverage. Constructors
automatically admit only direct Add/class calls on their own VisualElement;
mutating an external host requires the explicit checked boundary.
Interface membership is indexed through AllInterfaces once per declared type.
Only methods in actual calls or method-group/publication bindings request a
GetMembers(name)/FindImplementationForInterfaceMember lookup in each matching
concrete interface context; original symbol identity distinguishes overloads.
This follows Roslyn's member-directed SymbolFinder hierarchy query, rather than
eagerly enumerating every inherited member of every control:
https://raw.githubusercontent.com/dotnet/roslyn/main/src/Workspaces/Core/Portable/FindSymbols/SymbolFinder_Hierarchy.cs
Uncalled inherited interface members generate no dispatch edges or resolutions.
This uses Roslyn's IAnonymousFunctionOperation.Symbol and
IMethodReferenceOperation.Method contracts:
https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.operations.ianonymousfunctionoperation.symbol
https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.operations.imethodreferenceoperation
It differs from blanket registration of any method named as a callback. A raw
mutation reached from an uncovered caller must still fail. A native matched
control that wrongly registers such a bypass stops this coverage method.

Registered Display/Replace state edges come from the loaded runtime's immutable
UiTransitionProtocol, captured on the main thread with its module identity and
returned as RuntimeProtocol. The runtime advances through that same content
protocol and validates visibility arcs. Missing protocol data rejects helper
registration. The source worker does not maintain an outgoing/incoming copy.
Clear/Remove helpers have the runtime's unpublished-only hierarchy contract and
must retain the exact source target. Native driver declarations resolve one
actual bool/numeric/enum field or property plus one zero-argument retirement
operation or Task completion property; missing, ambiguous or wrong-kind members
remain structured registration gaps. This is source linkage, not motion proof.
Context-only sources do not count as reviewed UI or add their sites to the graph.
SemanticMode defaults to NativeAssemblies and rejects uncompiled source membership.
DeclaredFixture explicitly compiles standalone fixture roots against loaded
metadata. It is a separate test contract, never a production fallback. The report
publishes both declared-source and semantic-context digests and separate cost counts.
Unresolved calls retain exact sites, candidate reasons and candidate symbols,
and reject that selected call graph;
compiler-bound nameof operations are counted separately as intrinsics.
Invocation syntax owns the called method; a delegate receiver expression can
instead yield a field or no symbol. DelegateCallSites records actual bound Invoke
calls without claiming their runtime delegate targets. These remain distinct
from unresolved semantic bindings and actual runtime navigation proof.

The framework/project adapter must connect these source sites to its actual
registered configurations, serialized components, UXML hosts and navigation
routes. This tool cannot infer a game's possible menu destinations from test
names. Unregistered mutations, missing host links and missing runtime witnesses
are different gaps. Source registration, nonempty animation settings or a passing
tool receipt never establish a natural intermediate rendered frame.

One native scope owns one cancellable worker and paired reload retirement.
Its actual retirement task is admitted to the SDK before source work starts;
temporary-object creation is declared as a runtime effect even though source
analysis itself only reads project files.
Canonical cleanup cancels and joins that same task before destroying its scope;
there are no polling asset scans, copied documents or additional Editor transport.
The scope rejects missing capabilities and over-budget inputs, never truncates
them into a passing report. Native drawing/input, interrupted navigation,
languages, viewports and lifecycle acceptance belong to the current game.

## Static Cost Ledger

Graph admission bounds remain64 roots,64 explicit exclusions,128 local selectors,
1024 C# files,16MiB source,250000 selected syntax nodes and12000 structural
call/state edges. Each unresolved binding
admits at most128 candidate symbols; it otherwise fails without truncation.
These are fail boundaries.

Virtual override declarations add one original-symbol edge per actual override.
Native Task drivers also bind their actual delegate arguments, using resolved
method/type UiTransitionDriver attributes and their real phase/retirement
members. NativeDriverCallback links are distinct from synchronous initialization
and replacement. They accept synchronous Action or Task-returning asynchronous
callbacks; async-void, invalid driver, void lifetime, direct and deferred bypasses
remain uncovered. This declaration belongs to an existing presentation lifetime,
not an exemption for ordinary rebuild or placement. Hierarchy Add/Insert/Clear
are inventoried even through VisualElement.Hierarchy.

Callable metadata admits2048 distinct method descriptors,32 attributes per
method/type and128 parameters: at most131072 attribute inspections and262144
parameter visits. AttributeClass/ConstructorArguments bind through the exact
public AttributeData base contract, bounded to16 base types per attribute;
at most2097152 base-type visits. Both publication propagation passes preserve
the existing12000-edge/24000-endpoint limits. No callback signature enlarges the
dispatch budget or grants synchronous Initialize permission.
Task identity resolves the actual class name/namespace and at most64 base types,
so TaskFactory and other prefix matches cannot qualify. The selected250000-node
syntax census bounds invocation Task checks at16000000 base-type visits; actual
Task classes terminate on the first match, and value/void returns do not traverse.

Interface dispatch admits4096 distinct declared types and64 interfaces per type,
at most262144 indexed memberships. Actual referenced methods admit at most12000
implementation lookups; each examines at most128 same-name members (1536000
original-symbol comparisons). No method-pair cross product is built. Dispatch retains the same
12000 structural-edge budget, so excess input fails before propagation.
Publication propagation indexes at most12000 structural edges and24000 endpoint
symbols. Each edge is admitted once and visited once when its source becomes
covered; queue nodes enter once. Deferred bindings remain blocked inputs. This
costs at most24000 endpoint insertions plus24000 edge visits, with two bounded
adjacency/count maps and one queue, and no graph-depth recursion or all-pairs
search. Method-reference and anonymous-function operation reads reuse the same
250000-node selected syntax census, at mostone extra semantic operation per
identifier/member/lambda node. Publication argument lookup reads the actual
invocation's at most128 arguments; it rejects a larger invocation. Pass within
these frozen bounds; runtime target containment remains separately verified.

Stylesheet coverage declares its actual roots separately. The existing USS rule
parser identifies selectors that own display, visibility or zero opacity. Class
mutation sites retain the matching rule locations; a dynamic class remains a
potential presentation mutation. A missing stylesheet census is explicit in the
report. UIDocument enable/root replacement and native hierarchy Add operations
remain separate source sites, not implicit acceptance of a registered document.
Style admission is256 roots,1024 unique USS files/16MiB,8192 parsed rules and32768
selector-class references, plus32768 published site/rule links. Each file is read
and parsed once and verified once before completion; class lookup is indexed.
Temporary text totals16MiB plus parser products bounded by8192 rules and32768
links; no Unity object is read on the worker. This is a fail boundary, not a
truncated pass. The main project adapter supplies its actual host dependencies.

Protocol discovery inspects at most512 loaded assembly identities and reads two
families of at most32 arcs each once. Native driver declarations are cached by
their actual attribute syntax, capped at4096 entries. Source ancestry is capped
at128 nodes; each declaration resolves two bounded names through at most64 base
types and128 same-name candidates. Each lookup stops at the first declaring
type: at most524,288 base/member lookups and1,048,576 returned candidate checks.
Only128 phase and128 retirement candidates are held per resolution; the cache
retains bounded names/results and existing syntax identities, not member arrays.
Protocol arcs remain subject to
the existing12000-edge graph budget. No persistent cache or runtime asset scan.
Files are parsed once. Line starts are indexed once; source coordinates use binary
search. Symbol requests receive the actual cancellation token. Graph adjacency is
indexed by symbol/path; all call locations are bounded by the syntax-node census.
Local selection performs one edge pass, with no all-pairs
site search. Output is complete within those bounds.

Native capture admits256 compilations/32768 source-membership records, checking
64 roots and64 exclusions at most4,194,304 times. Only selected owners' metadata
is retained. At most8 contexts admit4096 source parses/64MiB/2000000 syntax nodes,
with512 reference images/512MiB per context and2048 unique images/1GiB total.
Thus at most4096 reference views represent4GiB of logical read-only images;
the separate view-byte and unique-byte products preserve that distinction.
Standalone fixture references retain their512-path/512MiB bound.
Parsing, semantic binding, hashing and source/reference identity readback run on
one worker. Context source/hash buffers are bounded copies of64MiB UTF-8 input;
all context traversals stop at2000000 nodes. Native Roslyn owns opaque syntax/
semantic metadata allocations; these admitted image and source sizes are input
bounds, not a claim about compiler resident memory. No persistent cache is created.
There is no main-thread busy wait during ordinary
review. Reload must join its canceled compiler work before unloading the domain.
No Unity object, AssetDatabase or UI call runs on the worker. Pass for the declared
bounded source capability; actual measured runtime and host coverage remain open.

Fixtures preserve actual symbol binding for a raw native display write, real
registered helpers, nested and named content publication, a mismatched target,
and full/local caller boundaries. The native public facade must also be invoked
after package adoption; direct fixture execution alone cannot certify that route.

The native compiled-sibling control intentionally selects a source value plus
its producer while its consumer is compiled metadata. DeclaredFixture must expose
the resulting type-identity mismatch; NativeAssemblies must bind it using the
actual sibling source without claiming the sibling file as reviewed graph scope.
Basis: [Unity compilation assembly products](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Compilation.Assembly.html)
and [Roslyn's complete compilation model](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/work-with-semantics).
