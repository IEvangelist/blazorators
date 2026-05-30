// spotlight.js — pointer-tracked CSS custom properties for spotlight/glow hovers.
//
// For every matching element, listens to pointer events and writes
// `--mx` / `--my` (in px, relative to the element's box) so that CSS
// `radial-gradient(circle at var(--mx) var(--my), ...)` can paint a
// spotlight that follows the cursor. The element opts in with
// `data-spotlight` and is otherwise ordinary HTML.

const wired = new WeakSet();
const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
const coarsePointer = window.matchMedia('(pointer: coarse)');

function supportsSpotlight() {
    return !prefersReducedMotion.matches && !coarsePointer.matches;
}

function setSpotlightPosition(card, x, y) {
    const r = card.getBoundingClientRect();
    card.style.setProperty('--mx', `${x - r.left}px`);
    card.style.setProperty('--my', `${y - r.top}px`);
}

function centerSpotlight(card) {
    const r = card.getBoundingClientRect();
    card.style.setProperty('--mx', `${r.width / 2}px`);
    card.style.setProperty('--my', `${r.height / 2}px`);
}

function clearSpotlight(card) {
    card.style.removeProperty('--mx');
    card.style.removeProperty('--my');
}

function attach(card) {
    if (wired.has(card)) {
        return;
    }
    wired.add(card);

    card.addEventListener('pointermove', e => {
        if (supportsSpotlight()) {
            setSpotlightPosition(card, e.clientX, e.clientY);
        }
    });

    card.addEventListener('pointerleave', () => {
        clearSpotlight(card);
    });

    card.addEventListener('focus', () => {
        if (supportsSpotlight()) {
            centerSpotlight(card);
        }
    });

    card.addEventListener('blur', () => {
        clearSpotlight(card);
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
