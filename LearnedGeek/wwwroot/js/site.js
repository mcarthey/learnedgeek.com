// Blog search functionality
function initBlogSearch() {
    const searchInput = document.getElementById('blog-search');
    const clearButton = document.getElementById('search-clear');
    const resultsCount = document.getElementById('search-results-count');
    const postsGrid = document.getElementById('posts-grid');
    const noResults = document.getElementById('no-search-results');
    const clearSearchLink = document.getElementById('clear-search-link');

    if (!searchInput || !postsGrid) return;

    const postCards = postsGrid.querySelectorAll('.post-card');
    let debounceTimer;

    function performSearch(query) {
        const searchTerms = query.toLowerCase().trim().split(/\s+/).filter(t => t.length > 0);
        let visibleCount = 0;

        postCards.forEach(card => {
            if (searchTerms.length === 0) {
                card.style.display = '';
                visibleCount++;
                return;
            }

            const title = card.dataset.title || '';
            const description = card.dataset.description || '';
            const tags = card.dataset.tags || '';
            const searchableText = `${title} ${description} ${tags}`;

            const matches = searchTerms.every(term => searchableText.includes(term));
            card.style.display = matches ? '' : 'none';
            if (matches) visibleCount++;
        });

        // Update UI
        if (clearButton) {
            clearButton.classList.toggle('hidden', query.length === 0);
        }

        if (resultsCount) {
            if (searchTerms.length > 0) {
                resultsCount.textContent = `${visibleCount} post${visibleCount !== 1 ? 's' : ''} found`;
                resultsCount.classList.remove('hidden');
            } else {
                resultsCount.classList.add('hidden');
            }
        }

        if (noResults) {
            noResults.classList.toggle('hidden', visibleCount > 0 || searchTerms.length === 0);
        }

        // Hide/show the grid based on results
        if (postsGrid) {
            postsGrid.classList.toggle('hidden', visibleCount === 0 && searchTerms.length > 0);
        }
    }

    function clearSearch() {
        searchInput.value = '';
        performSearch('');
        searchInput.focus();
    }

    // Debounced search (300ms delay)
    searchInput.addEventListener('input', function() {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => performSearch(this.value), 300);
    });

    if (clearButton) {
        clearButton.addEventListener('click', clearSearch);
    }

    if (clearSearchLink) {
        clearSearchLink.addEventListener('click', function(e) {
            e.preventDefault();
            clearSearch();
        });
    }

    // Handle Escape key
    searchInput.addEventListener('keydown', function(e) {
        if (e.key === 'Escape') {
            clearSearch();
        }
    });
}

// Theme toggle functionality
function initThemeToggle() {
    const themeToggle = document.getElementById('theme-toggle');
    const themeToggleMobile = document.getElementById('theme-toggle-mobile');
    const sunIcon = document.getElementById('theme-icon-sun');
    const moonIcon = document.getElementById('theme-icon-moon');
    const sunIconMobile = document.getElementById('theme-icon-sun-mobile');
    const moonIconMobile = document.getElementById('theme-icon-moon-mobile');

    function updateIcons(isDark) {
        // Desktop icons
        if (sunIcon && moonIcon) {
            sunIcon.classList.toggle('hidden', !isDark);
            moonIcon.classList.toggle('hidden', isDark);
        }
        // Mobile icons
        if (sunIconMobile && moonIconMobile) {
            sunIconMobile.classList.toggle('hidden', !isDark);
            moonIconMobile.classList.toggle('hidden', isDark);
        }
    }

    function toggleTheme() {
        const isDark = document.documentElement.classList.toggle('dark');
        localStorage.setItem('theme', isDark ? 'dark' : 'light');
        updateIcons(isDark);
    }

    // Set initial icon state
    updateIcons(document.documentElement.classList.contains('dark'));

    // Attach click handlers
    if (themeToggle) {
        themeToggle.addEventListener('click', toggleTheme);
    }
    if (themeToggleMobile) {
        themeToggleMobile.addEventListener('click', toggleTheme);
    }
}

// Initialize on DOM load
document.addEventListener('DOMContentLoaded', function() {
    initThemeToggle();
    initBlogSearch();

    // Mobile navigation toggle
    const menuButton = document.getElementById('mobile-menu-button');
    const mobileMenu = document.getElementById('mobile-menu');
    const menuIconOpen = document.getElementById('menu-icon-open');
    const menuIconClose = document.getElementById('menu-icon-close');

    if (menuButton && mobileMenu) {
        menuButton.addEventListener('click', function() {
            const isExpanded = menuButton.getAttribute('aria-expanded') === 'true';

            // Toggle menu visibility
            mobileMenu.classList.toggle('hidden');

            // Toggle icons
            if (menuIconOpen && menuIconClose) {
                menuIconOpen.classList.toggle('hidden');
                menuIconClose.classList.toggle('hidden');
            }

            // Update aria-expanded
            menuButton.setAttribute('aria-expanded', !isExpanded);
        });

        // Close mobile menu when clicking on a link
        const mobileLinks = mobileMenu.querySelectorAll('a');
        mobileLinks.forEach(function(link) {
            link.addEventListener('click', function() {
                mobileMenu.classList.add('hidden');
                if (menuIconOpen && menuIconClose) {
                    menuIconOpen.classList.remove('hidden');
                    menuIconClose.classList.add('hidden');
                }
                menuButton.setAttribute('aria-expanded', 'false');
            });
        });
    }
});
