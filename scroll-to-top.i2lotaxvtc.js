let nextSubscriptionId = 1;
const subscriptions = new Map();

export function initialize(button) {
    if (!(button instanceof HTMLButtonElement)) {
        throw new Error('The scroll-to-top button was not found.');
    }

    const subscriptionId = nextSubscriptionId++;
    let animationFrame = 0;

    const updateVisibility = () => {
        animationFrame = 0;
        const revealAt = Math.max(320, window.innerHeight * 0.5);
        const isVisible = window.scrollY > revealAt;
        button.classList.toggle('is-visible', isVisible);
        button.setAttribute('aria-hidden', isVisible ? 'false' : 'true');
        button.tabIndex = isVisible ? 0 : -1;
    };

    const scheduleVisibilityUpdate = () => {
        if (animationFrame === 0) {
            animationFrame = requestAnimationFrame(updateVisibility);
        }
    };

    const scrollToTop = () => {
        document.getElementById('main')?.focus({ preventScroll: true });
        window.scrollTo({
            top: 0,
            left: 0,
            behavior: matchMedia('(prefers-reduced-motion: reduce)').matches
                ? 'auto'
                : 'smooth'
        });
    };

    window.addEventListener('scroll', scheduleVisibilityUpdate, { passive: true });
    window.addEventListener('resize', scheduleVisibilityUpdate);
    button.addEventListener('click', scrollToTop);
    updateVisibility();

    subscriptions.set(subscriptionId, {
        button,
        scheduleVisibilityUpdate,
        scrollToTop,
        get animationFrame() {
            return animationFrame;
        }
    });

    return subscriptionId;
}

export function dispose(subscriptionId) {
    const subscription = subscriptions.get(subscriptionId);
    if (!subscription) {
        return;
    }

    window.removeEventListener('scroll', subscription.scheduleVisibilityUpdate);
    window.removeEventListener('resize', subscription.scheduleVisibilityUpdate);
    subscription.button.removeEventListener('click', subscription.scrollToTop);
    if (subscription.animationFrame !== 0) {
        cancelAnimationFrame(subscription.animationFrame);
    }
    subscriptions.delete(subscriptionId);
}
