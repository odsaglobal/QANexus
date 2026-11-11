namespace ATIP.Infrastructure.Exploration;

/// <summary>
/// Detects and closes a modal covering the middle of the viewport (login walls, promo or location
/// pop-ups). Shared by the MCP explorer and the direct-Playwright replay so both judge "a popup is in
/// the way" identically — which is what lets a decision made while exploring replay the same way.
/// </summary>
internal static class BlockingPopup
{
    /// <summary>
    /// A function of <c>mode</c>: <c>"detect"</c> returns <c>"clear"</c> or <c>"blocked|&lt;popup text&gt;"</c>;
    /// <c>"close"</c> clicks the popup's own close control and returns <c>closed</c>/<c>no-close-control</c>.
    /// The element must look like a modal (very high z-index, a dialog role, or a translucent backdrop),
    /// so a full-page app shell that merely uses position:fixed is never mistaken for one.
    /// </summary>
    public const string Script = """
        (mode) => {
          const vw = window.innerWidth, vh = window.innerHeight;
          const looksModal = (el, s) =>
            (s.zIndex !== 'auto' && Number(s.zIndex) >= 1000)
            || el.matches('[role=dialog],[role=alertdialog],[aria-modal=true]')
            || !!el.querySelector('[role=dialog],[role=alertdialog],[aria-modal=true]')
            || /rgba\([^)]*,\s*0?\.\d+\)/.test(s.backgroundColor);
          let el = document.elementFromPoint(vw / 2, vh / 2);
          let blocker = null;
          while (el && el !== document.body && el !== document.documentElement) {
            const s = getComputedStyle(el);
            if (s.position === 'fixed') {
              const r = el.getBoundingClientRect();
              if (r.width >= vw * 0.5 && r.height >= vh * 0.4 && looksModal(el, s)) blocker = el;
            }
            el = el.parentElement;
          }
          if (!blocker) return 'clear';
          if (mode === 'detect') {
            const text = (blocker.innerText || '').replace(/[✕×✖]/g, ' ').replace(/\s+/g, ' ').trim();
            return 'blocked|' + text.slice(0, 160);
          }
          const norm = (t) => (t || '').trim().toLowerCase();
          const visible = (e) => { const r = e.getBoundingClientRect(); const s = getComputedStyle(e); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none'; };
          const words = ['✕', '×', '✖', 'x', 'close', 'not now', 'no thanks', 'maybe later', 'skip', 'dismiss'];
          const isClose = (e) => {
            const label = norm(e.getAttribute('aria-label')) + ' ' + norm(e.getAttribute('title'));
            return words.includes(norm(e.textContent)) || /\bclose\b|\bdismiss\b/.test(label);
          };
          const close = [...blocker.querySelectorAll('button,[role=button],a,span,div,svg,i')]
            .find((e) => visible(e) && (e.children.length === 0 || e.matches('button,[role=button]')) && isClose(e));
          if (!close) return 'no-close-control';
          (close.closest('button,[role=button],a') || close).click();
          return 'closed';
        }
        """;

    /// <summary>Parses a detect result into the popup's visible text, or null when nothing is blocking.</summary>
    public static string? ParseDetect(string? result)
    {
        if (result is null)
        {
            return null;
        }

        var start = result.IndexOf("blocked|", StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var text = result[(start + "blocked|".Length)..];
        var end = text.IndexOfAny(['"', '\n']);
        return (end >= 0 ? text[..end] : text).Trim();
    }

    /// <summary>A stable identity for "the same popup" across appearances: its first few words.</summary>
    public static string KeyFor(string popupText)
    {
        var words = popupText.ToLowerInvariant()
            .Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray()))
            .Where(w => w.Length > 0)
            .Take(6);
        return string.Join(' ', words);
    }
}
