// Mobile navigation toggle
document.addEventListener('DOMContentLoaded', function() {
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
