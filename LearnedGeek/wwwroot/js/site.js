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

// Mobile navigation toggle
document.addEventListener('DOMContentLoaded', function() {
    initThemeToggle();
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
