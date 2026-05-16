// highlight.js — modern syntax highlighting loaded on-demand via Blazor JS interop.
// Uses highlight.js's tree-shakeable core + only the grammars we need so the network
// payload stays small. Tokens are emitted as `.hljs-*` spans and themed via site.css.

let _loader;

function load() {
    if (_loader) {
        return _loader;
    }

    _loader = (async () => {
        const core = (await import('https://esm.sh/highlight.js@11.10.0/lib/core')).default;
        const [json, csharp, xml, css] = await Promise.all([
            import('https://esm.sh/highlight.js@11.10.0/lib/languages/json').then(m => m.default),
            import('https://esm.sh/highlight.js@11.10.0/lib/languages/csharp').then(m => m.default),
            import('https://esm.sh/highlight.js@11.10.0/lib/languages/xml').then(m => m.default),
            import('https://esm.sh/highlight.js@11.10.0/lib/languages/css').then(m => m.default),
        ]);

        core.registerLanguage('json', json);
        core.registerLanguage('csharp', csharp);
        core.registerLanguage('cs', csharp);
        core.registerLanguage('html', xml);
        core.registerLanguage('xml', xml);
        core.registerLanguage('css', css);

        return core;
    })();

    return _loader;
}

export async function highlight(code, lang) {
    if (!code) {
        return '';
    }

    try {
        const hljs = await load();
        const language = hljs.getLanguage(lang) ? lang : 'json';
        const result = hljs.highlight(code, { language, ignoreIllegals: true });
        return result.value;
    }
    catch (e) {
        console.warn('[highlight] failed, falling back to plain text', e);
        return escapeHtml(code);
    }
}

function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, c =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
}
