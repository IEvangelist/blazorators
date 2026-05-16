// spotlight.js — pointer-tracked CSS custom properties for spotlight/glow hovers.
//
// For every matching element, listens to pointer events and writes
// `--mx` / `--my` (in px, relative to the element's box) so that CSS
// `radial-gradient(circle at var(--mx) var(--my), ...)` can paint a
// spotlight that follows the cursor. The element opts in with
// `data-spotlight` and is otherwise ordinary HTML.

const wired = new WeakSet();

function attach(card) {
    if (wired.has(card)) {
        return;
    }
    wired.add(card);

    card.addEventListener('pointermove', e => {
        const r = card.getBoundingClientRect();
        card.style.setProperty('--mx', `${e.clientX - r.left}px`);
        card.style.setProperty('--my', `${e.clientY - r.top}px`);
    });

    card.addEventListener('pointerleave', () => {
        card.style.removeProperty('--mx');
        card.style.removeProperty('--my');
    });
}

export function init(scopeSelector) {
    const scope = scopeSelector ? document.querySelector(scopeSelector) : document;
    if (!scope) {
        return 0;
    }

    const cards = scope.querySelectorAll('[data-spotlight]');
    for (const card of cards) {
        attach(card);
    }
    return cards.length;
}
