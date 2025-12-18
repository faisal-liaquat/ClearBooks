//Main scripts for navigation and common functionality


//Wait for DOM to be ready
document.addEventListener('DOMContentLoaded', function () {
    console.log('Main scripts loaded');

    //Highlight active menu item
    initializeNavigation();

    //Setup common event listeners
    setupCommonEventListeners();
});

function initializeNavigation() {
    //Get current page path
    const currentPath = window.location.pathname;

    //Find all navigation links
    const navLinks = document.querySelectorAll('.sidebar-menu a, .nav-menu a');

    navLinks.forEach(link => {
        //Remove any existing active classes
        link.classList.remove('active');

        //Get the link's href path
        const linkPath = new URL(link.href, window.location.origin).pathname;

        //Check if this link matches the current page
        if (linkPath === currentPath ||
            (currentPath === '/' && linkPath.includes('index.html')) ||
            (currentPath.includes('index.html') && linkPath === '/')) {
            link.classList.add('active');
        }

        //Add click event listener for navigation highlighting
        link.addEventListener('click', function () {
            //Remove active class from all navigation links
            navLinks.forEach(navLink => navLink.classList.remove('active'));
            //Add active class to clicked link
            this.classList.add('active');
        });
    });
}

function setupCommonEventListeners() {
    //Handle modal close buttons (generic)
    const closeButtons = document.querySelectorAll('.close, .modal-close');
    closeButtons.forEach(button => {
        button.addEventListener('click', function () {
            const modal = this.closest('.modal');
            if (modal) {
                modal.style.display = 'none';
            }
        });
    });

    //Handle clicking outside modals to close them
    window.addEventListener('click', function (event) {
        if (event.target.classList.contains('modal')) {
            event.target.style.display = 'none';
        }
    });

    //Handle form validation styling
    const forms = document.querySelectorAll('form');
    forms.forEach(form => {
        const inputs = form.querySelectorAll('input, select, textarea');

        inputs.forEach(input => {
            //Remove error styling on focus
            input.addEventListener('focus', function () {
                this.classList.remove('error', 'invalid');
                const errorElement = document.getElementById(this.id + '-error');
                if (errorElement) {
                    errorElement.textContent = '';
                }
            });

            //Add basic validation on blur
            input.addEventListener('blur', function () {
                if (this.hasAttribute('required') && !this.value.trim()) {
                    this.classList.add('error');
                }
            });
        });
    });

    //Handle responsive navigation toggle (if exists)
    const navToggle = document.querySelector('.nav-toggle');
    const sidebar = document.querySelector('.sidebar');

    if (navToggle && sidebar) {
        navToggle.addEventListener('click', function () {
            sidebar.classList.toggle('collapsed');
        });
    }

    //Handle table row highlighting
    const tables = document.querySelectorAll('table');
    tables.forEach(table => {
        const rows = table.querySelectorAll('tbody tr');
        rows.forEach(row => {
            row.addEventListener('mouseenter', function () {
                this.classList.add('highlighted');
            });

            row.addEventListener('mouseleave', function () {
                this.classList.remove('highlighted');
            });
        });
    });

    //Handle loading states for buttons
    const buttons = document.querySelectorAll('button[type="submit"], .btn-primary');
    buttons.forEach(button => {
        button.addEventListener('click', function () {
            //Don't add loading state if button is already disabled
            if (!this.disabled) {
                const originalText = this.textContent;
                this.setAttribute('data-original-text', originalText);

                //Add loading state after a short delay to avoid flickering
                setTimeout(() => {
                    if (!this.disabled) return; // Don't change if not disabled

                    this.innerHTML = '<span class="loading-spinner"></span> Loading...';
                    this.classList.add('loading');
                }, 100);
            }
        });
    });

    //Handle success/error message auto-hide
    const messages = document.querySelectorAll('.success-message, .error-message, .alert');
    messages.forEach(message => {
        if (message.textContent.trim()) {
            //Auto-hide after 5 seconds
            setTimeout(() => {
                message.style.opacity = '0';
                setTimeout(() => {
                    message.style.display = 'none';
                }, 300);
            }, 5000);
        }
    });

    console.log('Common event listeners initialized');
}

//Utility function to show loading state
function showLoadingState(button, loadingText = 'Loading...') {
    if (button) {
        const originalText = button.textContent;
        button.setAttribute('data-original-text', originalText);
        button.disabled = true;
        button.innerHTML = `<span class="loading-spinner"></span> ${loadingText}`;
        button.classList.add('loading');
    }
}

//Utility function to hide loading state
function hideLoadingState(button) {
    if (button) {
        const originalText = button.getAttribute('data-original-text');
        button.disabled = false;
        button.textContent = originalText || 'Submit';
        button.classList.remove('loading');
        button.removeAttribute('data-original-text');
    }
}

//Utility function to format currency
function formatCurrency(amount, currency = 'USD') {
    return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: currency,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    }).format(amount || 0);
}

//Utility function to format date
function formatDate(dateString, options = {}) {
    if (!dateString) return '';

    const defaultOptions = {
        year: 'numeric',
        month: 'short',
        day: 'numeric'
    };

    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', { ...defaultOptions, ...options });
}

//Utility function to debounce function calls
function debounce(func, wait, immediate) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            timeout = null;
            if (!immediate) func(...args);
        };
        const callNow = immediate && !timeout;
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
        if (callNow) func(...args);
    };
}

//Make utility functions globally available
window.showLoadingState = showLoadingState;
window.hideLoadingState = hideLoadingState;
window.formatCurrency = formatCurrency;
window.formatDate = formatDate;
window.debounce = debounce;

console.log('Main scripts loaded and utilities available globally');