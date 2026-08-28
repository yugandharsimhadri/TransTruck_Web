using System.Net;
using Microsoft.Playwright;

namespace TransTrack.Automation;

/// <summary>
/// Turns a workflow's narration beats into something a camera can capture: an on-screen caption
/// strip and the pauses that make each step followable. In <see cref="RunMode.Test"/> every method
/// is a no-op beyond recording the step for the run log, which is what lets one set of workflow
/// definitions serve both the UAT run and the recorded videos.
///
/// The overlay is injected into the page rather than composited afterwards so the caption is burned
/// into the OBS capture in sync with the action it describes — no post-hoc alignment needed. It is
/// re-injected on demand because a client-side route change discards it.
/// </summary>
public sealed class Narrator(IPage page, AutomationOptions options, Action<string>? log = null)
{
    private const string OverlayId = "transtruck-demo-narration";

    private readonly List<string> _steps = [];

    private int _stepNumber;

    /// <summary>
    /// The narration beats of the workflow currently being run, in order — cleared by
    /// <see cref="AnnounceWorkflowAsync"/>, so this is per segment and not per session. Both the demo
    /// manifest and a failed scenario's report read it, and both want just this workflow's steps.
    /// </summary>
    public IReadOnlyList<string> Steps => _steps;

    /// <summary>
    /// Announces one step: logs it, shows it on screen in the video modes, and holds long enough to
    /// read it. Call immediately before the interaction it describes.
    /// </summary>
    public async Task SayAsync(string narration)
    {
        _stepNumber++;
        _steps.Add(narration);
        log?.Invoke($"  {_stepNumber,2}. {narration}");

        if (!options.ShowCaptions)
            return;

        var caption = options.RunMode == RunMode.UserGuide
            ? $"Step {_stepNumber} — {narration}"
            : narration;

        await ShowAsync(WebUtility.HtmlEncode(caption), isTitle: false);
        await page.WaitForTimeoutAsync(options.CaptionHoldMs);
    }

    /// <summary>
    /// Shows the workflow's title card and resets the step counter. Held slightly longer than a
    /// normal caption so the editor has a clean segment boundary to cut on.
    /// </summary>
    public async Task AnnounceWorkflowAsync(string displayName, string module)
    {
        _stepNumber = 0;
        _steps.Clear();
        log?.Invoke($"[{module}] {displayName}");

        if (!options.ShowCaptions)
            return;

        var html = $"{WebUtility.HtmlEncode(displayName)}<span class=\"tt-module\">{WebUtility.HtmlEncode(module)}</span>";
        await ShowAsync(html, isTitle: true);
        await page.WaitForTimeoutAsync(options.CaptionHoldMs + 600);
    }

    /// <summary>Holds the closing frame, then clears the caption so the next segment starts clean.</summary>
    public async Task CloseAsync()
    {
        if (!options.ShowCaptions)
            return;

        await page.WaitForTimeoutAsync(options.ClosingHoldMs);
        await HideAsync();
    }

    /// <summary>
    /// A deliberate pause with no caption, for letting a list finish rendering on camera. Skipped
    /// entirely under test, where waiting on a locator is both faster and more reliable.
    /// </summary>
    public async Task BeatAsync(int milliseconds = 700)
    {
        if (options.RunMode == RunMode.Test)
            return;

        await page.WaitForTimeoutAsync(milliseconds);
    }

    /// <summary>
    /// Renders the caption inside a <b>closed</b> shadow root, which is the whole point of the
    /// design: Playwright's text and role locators pierce open shadow roots and the light DOM, so a
    /// caption living in either would be matched by the very assertions the workflows make about the
    /// page. A caption reading "the balance still to collect" would collide with the tile labelled
    /// "Still to collect" and turn an ordinary assertion into a strict-mode violation — failing in
    /// the video modes only, where the caption exists. A closed root is not pierced, so the overlay
    /// is visible to the camera and invisible to the automation.
    ///
    /// Sized in vw so the same code reads correctly on a 1600px desktop frame and a 393px phone,
    /// where a fixed 20px caption would swallow a third of the screen.
    /// </summary>
    private async Task ShowAsync(string html, bool isTitle)
    {
        // Tolerated rather than propagated: a caption that fails to render (mid-navigation, page
        // closing) must never fail the workflow it is only describing.
        try
        {
            await page.EvaluateAsync(
                """
                ([overlayId, html, isTitle]) => {
                  let state = window.__ttNarration;

                  if (!state || !document.body.contains(state.host)) {
                    const host = document.createElement('div');
                    host.id = overlayId;
                    document.body.appendChild(host);

                    const root = host.attachShadow({ mode: 'closed' });

                    const style = document.createElement('style');
                    style.textContent = `
                      :host {
                        position: fixed; inset: 0;
                        z-index: 2147483647; pointer-events: none; display: block;
                      }
                      .caption {
                        position: absolute; left: 50%; bottom: 5%; transform: translateX(-50%);
                        max-width: 88vw; box-sizing: border-box;
                        padding: clamp(10px, 1.6vw, 16px) clamp(16px, 2.4vw, 28px);
                        border-radius: 14px;
                        background: rgba(9, 17, 28, 0.9); color: #f2f7fc;
                        font-family: system-ui, -apple-system, 'Segoe UI', sans-serif;
                        font-size: clamp(15px, 2.2vw, 21px); line-height: 1.4;
                        font-weight: 600; text-align: center;
                        box-shadow: 0 14px 44px rgba(0,0,0,.5);
                        border: 1px solid rgba(96,165,250,.45);
                      }
                      .caption.title {
                        bottom: auto; top: 50%; transform: translate(-50%, -50%);
                        font-size: clamp(26px, 4.4vw, 46px); font-weight: 700;
                        padding: clamp(22px, 3vw, 36px) clamp(28px, 4vw, 60px);
                        letter-spacing: .3px;
                      }
                      .caption .tt-module {
                        display: block; margin-top: 10px;
                        font-size: clamp(11px, 1.4vw, 16px); letter-spacing: 2px;
                        text-transform: uppercase; color: #7dd3fc; font-weight: 600;
                      }
                    `;

                    const box = document.createElement('div');
                    root.appendChild(style);
                    root.appendChild(box);

                    state = { host, box };
                    window.__ttNarration = state;
                  }

                  state.box.className = isTitle ? 'caption title' : 'caption';
                  state.box.innerHTML = html;
                }
                """,
                new object[] { OverlayId, html, isTitle });
        }
        catch (PlaywrightException)
        {
        }
    }

    private async Task HideAsync()
    {
        try
        {
            await page.EvaluateAsync(
                """
                () => {
                  const state = window.__ttNarration;
                  if (state && state.host) state.host.remove();
                  window.__ttNarration = null;
                }
                """);
        }
        catch (PlaywrightException)
        {
        }
    }
}
