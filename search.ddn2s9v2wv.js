let nextSubscriptionId = 1;
const subscriptions = new Map();
let catalogPromise;
let pagefindPromise;

export function initialize(dialogId, dotNetReference) {
    const subscriptionId = nextSubscriptionId++;
    const handler = event => {
        const dialog = document.getElementById(dialogId);
        if (event.key === 'Escape' &&
            dialog instanceof HTMLDialogElement &&
            dialog.open) {
            event.preventDefault();
            dialog.close();
            return;
        }

        if (!(event.ctrlKey || event.metaKey) || event.key.toLowerCase() !== 'k') {
            return;
        }

        event.preventDefault();
        dotNetReference.invokeMethodAsync('OpenFromShortcutAsync');
    };

    document.addEventListener('keydown', handler);
    subscriptions.set(subscriptionId, { handler, dialogId });
    return subscriptionId;
}

export function dispose(subscriptionId) {
    const subscription = subscriptions.get(subscriptionId);
    if (!subscription) {
        return;
    }

    document.removeEventListener('keydown', subscription.handler);
    subscriptions.delete(subscriptionId);
}

export function showDialog(dialogId) {
    const dialog = document.getElementById(dialogId);
    if (!(dialog instanceof HTMLDialogElement)) {
        throw new Error(`Search dialog '${dialogId}' was not found.`);
    }

    if (!dialog.open) {
        dialog.showModal();
    }

    requestAnimationFrame(() => {
        dialog.querySelector('input[type="search"]')?.focus();
    });
}

export function closeDialog(dialogId) {
    const dialog = document.getElementById(dialogId);
    if (dialog instanceof HTMLDialogElement && dialog.open) {
        dialog.close();
    }
}

export async function search(query) {
    const normalizedQuery = String(query ?? '').trim();
    if (normalizedQuery.length < 2) {
        return { provider: 'Local catalog', results: [] };
    }

    const pagefind = await loadPagefind();
    if (pagefind) {
        const response = await pagefind.search(normalizedQuery);
        const results = await Promise.all(
            response.results.slice(0, 10).map(async result => {
                const data = await result.data();
                return {
                    title: data.meta.title ?? data.url,
                    category: data.meta.category ?? 'Blazorators',
                    summary: data.meta.summary ?? stripMarkup(data.excerpt),
                    url: data.url,
                    icon: data.meta.icon ?? 'file'
                };
            }));

        return { provider: 'Pagefind', results };
    }

    const catalog = await loadCatalog();
    return {
        provider: 'Local catalog',
        results: searchCatalog(catalog, normalizedQuery)
    };
}

async function loadPagefind() {
    if (pagefindPromise) {
        return pagefindPromise;
    }

    pagefindPromise = (async () => {
        const provider = document.querySelector(
            'meta[name="blazorators-search-provider"][content="pagefind"]');
        if (!provider) {
            return null;
        }

        try {
            const bundleUrl = new URL('pagefind/pagefind.js', document.baseURI);
            const pagefind = await import(bundleUrl.href);
            const baseUrl = new URL(document.baseURI).pathname;
            const basePath = new URL('pagefind/', document.baseURI).pathname;
            await pagefind.options({
                baseUrl,
                basePath,
                excerptLength: 24
            });
            return pagefind;
        } catch (error) {
            console.warn('[search] Pagefind could not load; using the local catalog.', error);
            return null;
        }
    })();

    return pagefindPromise;
}

function loadCatalog() {
    catalogPromise ??= fetch(new URL('search-catalog.json', document.baseURI))
        .then(response => {
            if (!response.ok) {
                throw new Error(`Search catalog request failed with ${response.status}.`);
            }
            return response.json();
        });
    return catalogPromise;
}

function searchCatalog(catalog, query) {
    const terms = normalize(query).split(/\s+/).filter(Boolean);
    return catalog
        .map(item => ({ item, score: scoreItem(item, terms) }))
        .filter(match => match.score > 0)
        .sort((left, right) =>
            right.score - left.score ||
            (right.item.weight ?? 0) - (left.item.weight ?? 0) ||
            left.item.title.localeCompare(right.item.title))
        .slice(0, 10)
        .map(({ item }) => ({
            title: item.title,
            category: item.category,
            summary: item.summary,
            url: item.path || document.baseURI,
            icon: item.icon
        }));
}

function scoreItem(item, terms) {
    const title = normalize(item.title);
    const summary = normalize(item.summary);
    const keywords = normalize((item.keywords ?? []).join(' '));
    const content = normalize(item.content ?? '');
    let score = 0;

    for (const term of terms) {
        let termScore = 0;
        if (title === term) termScore += 24;
        if (title.startsWith(term)) termScore += 14;
        else if (title.includes(term)) termScore += 10;
        if (keywords.includes(term)) termScore += 7;
        if (summary.includes(term)) termScore += 4;
        if (content.includes(term)) termScore += 2;
        if (termScore === 0) return 0;
        score += termScore;
    }

    return score;
}

function normalize(value) {
    return String(value ?? '')
        .normalize('NFKD')
        .replace(/[\u0300-\u036f]/g, '')
        .toLowerCase();
}

function stripMarkup(value) {
    const template = document.createElement('template');
    template.innerHTML = String(value ?? '');
    return template.content.textContent?.trim() ?? '';
}
