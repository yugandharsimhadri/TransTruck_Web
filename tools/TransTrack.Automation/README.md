# TransTrack.Automation

One set of business journeys through TransTruck, written once and consumed twice: `TransTrack.UatTests`
runs them headless and asserts they complete, and `TransTrack.DemoRunner` runs the same objects headed
and paced so OBS can record them.

That is the point of the layout. The scenarios live here rather than in the test project, so the
videos and the acceptance run are the same journey **by construction** — not because someone
remembered to keep two scripts in step. A workflow's verification steps are inside the workflow, so
footage can only ever come from a journey that passed its own checks.

Screen capture is OBS's job. Nothing here records video.

## Layout

| | |
|---|---|
| `Workflows/` | `IWorkflow`, the base class, the catalog, the runner, and the scenarios themselves |
| `TransTruckSession.cs` | browser + viewport + sign-in; what a workflow is handed |
| `ApiServer.cs` / `WebDevServer.cs` | starts the two servers a run needs, or reuses yours |
| `DemoData.cs` | the fixed cast, and the seeder that creates it through the product's own API |
| `Narrator.cs` | on-screen captions and pacing; a no-op under `Test` |

## Running it

```bash
# List the catalog. Starts nothing — the Studio calls this to build its module list.
dotnet run --project tools/TransTrack.DemoRunner -- --list

# The whole catalog, on camera, desktop.
dotnet run --project tools/TransTrack.DemoRunner -- --viewport desktop

# Two segments, phone-shaped, slower with numbered captions.
dotnet run --project tools/TransTrack.DemoRunner -- --viewport mobile --mode userguide --workflow Dashboard BrowseTrips

# The acceptance run: every workflow, both viewports, headless.
dotnet test tests/TransTrack.UatTests
```

| Flag | Meaning |
|---|---|
| `--workflow K1 K2 …` | Only these keys. Omitted ⇒ the whole catalog in catalog order. |
| `--viewport desktop\|mobile` | One viewport per run. Default `desktop`. |
| `--mode demo\|userguide\|test` | Pacing and captions. Default `demo`. |
| `--manifest <path>` | Default `artifacts/uat/demo-manifest-<viewport>.json`. |
| `--list` | Print the catalog and exit 0. |

Exit codes: **0** all requested workflows passed, **1** at least one failed, **2** the command line
could not be parsed.

## Environment variables

Every one has a working default, so a bare `dotnet test` needs no configuration.

| Variable | Default | |
|---|---|---|
| `TRANSTRUCK_UAT_BASE_URL` | `http://localhost:5310` | where the client is served |
| `TRANSTRUCK_UAT_API_BASE_URL` | `http://localhost:5311` | where the API is served |
| `TRANSTRUCK_UAT_RUN_MODE` | `test` | `test` / `demo` / `userguide` |
| `TRANSTRUCK_UAT_VIEWPORT` | `desktop` | `desktop` / `mobile` |
| `TRANSTRUCK_UAT_WEB_PATH` | `web/transtrack-web` | absolute path, if repo discovery fails |
| `TRANSTRUCK_UAT_MANAGE_SERVERS` | `true` | `false` to point at servers you started |
| `TRANSTRUCK_UAT_MOBILE_DEVICE` | `iPhone 15 Pro` | any Playwright device descriptor |

## Ports: 5310 and 5311

Next defaults to 3000 and several projects on this machine use the common defaults. A run that
silently attached to someone else's app would fail in a way that reads as a broken TransTruck, so the
ports are pinned — and **6041, the product's own production port, is deliberately never bound**, so a
UAT run cannot reach a real installation.

Next has no `--strictPort` (that is a Vite flag): given a busy port it prints a notice and quietly
starts on the next free one. `WebDevServer` therefore binds the port itself before starting Next and
refuses with a sentence naming the port. It also binds `IPv6Any` in dual mode, because Next binds
`::` — probing `127.0.0.1` alone reports the port free and then Next dies with `EADDRINUSE`.

If something is already serving 5310, it is identified before it is trusted (its `/login` must
mention LorryOwner) and then reused and left running, so `npm run dev` in another terminal survives a
UAT run.

## Capture dimensions

Report these to the video pipeline; they are printed at the end of every DemoRunner run.

| Viewport | Page (CSS px) | DPR | Captured pixels | OS window |
|---|---|---|---|---|
| Desktop | 1600 × 900 | 1× | 1600 × 900 | 1600 × 992 |
| Mobile (iPhone 15 Pro) | 393 × 659 | 3× | **1179 × 1977** | 393 × 751 |

The mobile window is sized to the phone plus browser chrome so the recording is phone-shaped rather
than a narrow strip down a widescreen canvas, and so the bottom tab bar — the control the mobile
videos most need to show — is inside the frame rather than under it.

## Why the real API, not a mock

`ABPS_WEB.Automation`, which this mirrors, answers every `/api/**` call from fixtures. That is right
there: its API needs SQL Server, so a mock is what makes the suite runnable at all.

Here the API is one process over a *file*, and `TRANSTRUCKWEB_DB` already exists to point it
somewhere harmless. So each run starts the real API against a throwaway SQLite database under
`artifacts/uat/api-data/<timestamp>/` and seeds it through the product's own onboarding endpoint.
That buys three things a fixture set cannot:

- **Determinism without maintenance.** The dataset is rebuilt from empty every run, so it is
  reproducible for the same reason a fixture is — but it cannot drift from the API the way a
  hand-written fixture silently does the first time a response shape changes.
- **Real coverage.** A mocked UAT proves the client renders a shape someone typed into a fixture.
  This proves the client, the controllers, EF, the tenant filter and SQLite agree.
- **No second implementation.** Mocking this product means hand-maintaining ~40 endpoints — a shadow
  backend that has to be kept in step forever.

The price is a few seconds of startup. The database is left on disk after a run, because a failed
scenario is far easier to diagnose against the data it actually ran on.

## Two viewports, one set of workflows

The same `IWorkflow` objects run in both. Where the journey genuinely differs, the workflow branches
on `context.IsMobile` and says why. The real differences today:

- **Navigation.** The desktop sidebar lists all nine screens. The phone carries four in its tab bar
  and files the rest behind **More** — so secondary screens take two taps, through a screen the
  desktop does not have. `WorkflowContext.NavigateAsync` owns this.
- **Signing out.** A sidebar button on desktop; behind the avatar menu on a phone.
- **The company name.** Permanently in the desktop sidebar; on mobile the top bar keeps the product
  name and the company name is the dashboard's own heading.

Two things that look like they should branch and deliberately do not: the trips list is the same
stack of cards in both, and the status filter is the same listbox.

**`ExpectVisibleAsync` and the `Visible()` helper matter more than they look.** This app ships both
layouts in the DOM and hides one with a breakpoint, so a plain `.First` keeps binding to the layout
that is not on screen and then waits out its timeout. Everything here filters to visible first.

## Adding a workflow

1. Add a class to `Workflows/`, deriving from `Workflow`, with a **stable PascalCase `Key`** — the
   Studio pairs narration and video against it, so treat it as a public API.
2. Write `RunAsync` as `StepAsync("narration", …)` beats. The narration is what the caption shows and
   what the AI writes the voice-over from, so write it as a sentence about the business, not the UI.
3. Put the verification inside the step. Prefer `c.ExpectVisibleAsync`, `c.Link`, `c.Button`,
   `c.LinkTo` over raw locators — they filter to the visible layout.
4. Register it in `WorkflowCatalog.All`, in the position it belongs in the product narrative — that
   list is the running order of the full demo.
5. Add a `[Theory]`/`[MemberData(nameof(BothViewports))]` test in `TransTrack.UatTests`, one line.
   Give the class an XML `<summary>` describing the module **in business terms**: the Studio scrapes
   it to generate the narration script, so its quality goes straight into the voice-over.

Locate controls by what is stable. This app's call-to-action links are anchors rendered through the
Button component, so they carry `role="button"` and are not findable as links — `c.LinkTo("/expenses/new")`
matches on the route instead, which is the product's own contract.

## Playwright version

Pinned to **1.61.0**, matching `ABPS_WEB.Automation`. Playwright pins an exact Chromium build per
version, so sharing the version shares the download rather than adding another build to
`~/AppData/Local/ms-playwright`. If `web/transtrack-web` ever adds `@playwright/test`, pin it to the
same major/minor for the same reason.

Chromium is installed on first launch failure, not up front — `playwright install` evicts builds no
longer referenced by the installed version, so running it routinely would keep deleting the browsers
other projects on this machine depend on.
