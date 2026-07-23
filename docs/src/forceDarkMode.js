const forceDarkMode = () => {
  if (typeof document === 'undefined') {
    return;
  }

  if (document.documentElement.getAttribute('data-theme') !== 'dark') {
    document.documentElement.setAttribute('data-theme', 'dark');
  }

  if (document.documentElement.getAttribute('data-theme-choice') !== 'dark') {
    document.documentElement.setAttribute('data-theme-choice', 'dark');
  }

  try {
    window.localStorage.setItem('theme', 'dark');
  } catch (_error) {
    // Ignore storage access restrictions; the DOM attributes are the source of truth here.
  }
};

forceDarkMode();

if (typeof window !== 'undefined') {
  window.addEventListener('storage', forceDarkMode);
  document.addEventListener('DOMContentLoaded', forceDarkMode);

  new MutationObserver(forceDarkMode).observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['data-theme', 'data-theme-choice'],
  });
}
