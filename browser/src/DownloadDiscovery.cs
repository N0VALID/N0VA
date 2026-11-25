namespace Nova;
internal static class DownloadDiscovery
{
    // Verified against Epic's public Windows download configuration. This is the
    // launcher installer; Fortnite itself is installed by the launcher.
    public const string EpicInstaller = "https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/installer/download/EpicGamesLauncherInstaller.exe";
    public static PageFile? Official(string source)
    {
        if (!Policy.WebUrl(source)) return null;
        var host = new Uri(source).Host;
        return host is "fortnite.com" or "www.fortnite.com" or "epicgames.com" or "www.epicgames.com" or "store.epicgames.com"
            ? new("Epic Games Launcher · Windows", EpicInstaller) : null;
    }
    public const string Script = """
        (() => {
          const result = [], seen = new Set(); let visited = 0;
          const pattern = /\.(exe|msi|msix|zip|7z|rar|tar|gz|bz2|xz|pdf|epub|dmg|pkg|deb|rpm|apk|iso|img|mp3|mp4|wav|ogg|webm|csv|docx?|xlsx?|pptx?)$/i;
          const label = e => (e.innerText || e.textContent || e.getAttribute('aria-label') || e.getAttribute('title') || '').trim().replace(/\s+/g,' ').slice(0,180);
          const release = u => u.hostname === 'github.com' && /^\/[^/]+\/[^/]+\/releases(?:\/(?:latest|tag\/[^?#]+))?\/?$/.test(u.pathname);
          const add = (name, raw, kind, base, actionId) => {
            let u; try { u = new URL(raw, base); } catch { return; }
            if (!['https:','http:'].includes(u.protocol) || u.username || u.password || u.href.length > 8192) return;
            u.hash = ''; const key = actionId || u.href; if (seen.has(key) || result.length >= 100) return;
            seen.add(key); result.push({name:(name || 'Download').slice(0,240),url:u.href,kind,actionId});
          };
          const here = new URL(location.href); if (release(here)) add('Release files', here.href, 'Release');
          const scan = root => {
            for (const e of root.querySelectorAll('*')) {
              if (++visited > 10000) return;
              if (e.shadowRoot) scan(e.shadowRoot);
              if (e.tagName === 'IFRAME') { try { if (e.contentDocument) scan(e.contentDocument); } catch {} }
              if (!e.matches('a[href],button,[role=button],[data-download-url],[data-href]')) continue;
              const text = label(e), raw = e.getAttribute('href') || e.getAttribute('data-download-url') || e.getAttribute('data-href');
              if (raw && !raw.startsWith('#')) {
                let u; try { u = new URL(raw, e.baseURI); } catch { continue; }
                const direct = e.hasAttribute('download') || e.hasAttribute('data-download-url') || pattern.test(u.pathname);
                const kind = release(u) ? 'Release' : direct ? 'File' : /download|installer/i.test(text + ' ' + u.pathname) ? 'Page' : '';
                if (kind) add(direct ? e.getAttribute('download') || text || u.pathname.split('/').pop() : text || 'Download page', u.href, kind);
              } else if (e.matches('button,[role=button]') && /download|install on windows|get (the )?launcher/i.test(text) && e.getClientRects().length > 0 && !e.disabled) {
                if (!e.dataset.novaDownload) e.dataset.novaDownload = 'n' + Math.random().toString(16).slice(2);
                add(text + ' · website button', location.href, 'Action', location.href, e.dataset.novaDownload);
              }
            }
          }; scan(document);
          return result;
        })()
        """;
}
