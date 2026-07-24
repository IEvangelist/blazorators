// Load highlight.js and only the grammar requested by the rendered snippet.

const version = '11.10.0';
const languageModules = new Set([
    'bash',
    'csharp',
    'css',
    'go',
    'java',
    'javascript',
    'json',
    'markdown',
    'powershell',
    'python',
    'rust',
    'sql',
    'typescript',
    'xml',
    'yaml',
]);
const aliases = new Map([
    ['cs', 'csharp'],
    ['html', 'xml'],
    ['md', 'markdown'],
    ['ps1', 'powershell'],
    ['razor', 'xml'],
    ['shell', 'bash'],
    ['sh', 'bash'],
]);

let coreLoader;
const grammarLoaders = new Map();

function loadCore() {
    coreLoader ??= import(`https://esm.sh/highlight.js@${version}/lib/core`)
        .then(module => module.default);
    return coreLoader;
}

async function loadGrammar(language) {
    const normalized = aliases.get(language) ?? language;
    if (!languageModules.has(normalized)) {
        return null;
    }

    const core = await loadCore();
    if (!core.getLanguage(normalized)) {
        if (!grammarLoaders.has(normalized)) {
            grammarLoaders.set(
                normalized,
                import(`https://esm.sh/highlight.js@${version}/lib/languages/${normalized}`)
                    .then(module => {
                        core.registerLanguage(normalized, module.default);
                        return normalized;
                    }));
        }
        await grammarLoaders.get(normalized);
    }

    return { core, language: normalized };
}

export async function highlight(code, lang) {
    if (!code) {
        return '';
    }

    const language = String(lang ?? '').trim().toLowerCase();
    if (language === 'plaintext' || language === 'text') {
        return escapeHtml(code);
    }

    try {
        const grammar = await loadGrammar(language);
        if (!grammar) {
            return escapeHtml(code);
        }

        return grammar.core.highlight(code, {
            language: grammar.language,
            ignoreIllegals: true,
        }).value;
    }
    catch (error) {
        console.warn('[highlight] failed, falling back to plain text', error);
        return escapeHtml(code);
    }
}

function escapeHtml(value) {
    return String(value).replace(/[&<>"']/g, character =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]));
}
