//wait for AuthManager to be available
function waitForAuthManager(callback) {
    if (typeof window.AuthManager !== 'undefined') {
        callback();
    } else {
        console.log('Waiting for AuthManager to load...');
        setTimeout(() => waitForAuthManager(callback), 100);
    }
}

//Initialize when both DOM and AuthManager are ready
function initializeDashboard() {
    console.log('Initializing Dashboard module...');

    try {
        //Display user information
        displayUserInfo();

        //Load dashboard statistics
        loadDashboardStats();

        //Setup event listeners
        setupEventListeners();

        console.log('Dashboard module initialized successfully');
    } catch (error) {
        console.error('Error initializing dashboard:', error);
    }
}

function displayUserInfo() {
    const currentUser = AuthManager.getCurrentUser();

    if (currentUser) {
        const userNameElement = document.getElementById('user-name');
        const welcomeUserNameElement = document.getElementById('welcome-user-name');

        if (userNameElement) {
            userNameElement.textContent = currentUser.name;
        }

        if (welcomeUserNameElement) {
            welcomeUserNameElement.textContent = currentUser.name;
        }

        console.log('User info displayed for:', currentUser.name);
    } else {
        console.warn('No user information available');
    }
}

async function loadDashboardStats() {
    try {
        console.log('Loading dashboard statistics...');

        //Show loading state
        setLoadingState();

        //Load statistics from various endpoints
        const [accounts, vouchers, payments, receipts] = await Promise.allSettled([
            fetchCount('/api/ChartOfAccounts'),
            fetchCount('/api/Vouchers'),
            fetchCount('/api/Payments'),
            fetchCount('/api/Receipts')
        ]);

        //Update stat cards with animation
        updateStatCard('accounts-count', accounts.status === 'fulfilled' ? accounts.value : 0);
        updateStatCard('vouchers-count', vouchers.status === 'fulfilled' ? vouchers.value : 0);
        updateStatCard('payments-count', payments.status === 'fulfilled' ? payments.value : 0);
        updateStatCard('receipts-count', receipts.status === 'fulfilled' ? receipts.value : 0);

        console.log('Dashboard statistics loaded successfully');

    } catch (error) {
        console.error('Error loading dashboard statistics:', error);
        //Set all counts to 0 if there's an error
        updateStatCard('accounts-count', 0);
        updateStatCard('vouchers-count', 0);
        updateStatCard('payments-count', 0);
        updateStatCard('receipts-count', 0);
    }
}

function setLoadingState() {
    const statElements = ['accounts-count', 'vouchers-count', 'payments-count', 'receipts-count'];
    statElements.forEach(elementId => {
        const element = document.getElementById(elementId);
        if (element) {
            element.textContent = '';
            element.style.opacity = '0.5';
        }
    });
}

async function fetchCount(endpoint) {
    try {
        console.log(`Fetching data from ${endpoint}...`);
        const response = await AuthManager.makeAuthenticatedRequest(endpoint);

        if (!response || !response.ok) {
            throw new Error(`Failed to fetch data from ${endpoint}: ${response?.status} ${response?.statusText}`);
        }

        const data = await response.json();
        const count = Array.isArray(data) ? data.length : 0;
        console.log(`${endpoint} returned ${count} items`);
        return count;

    } catch (error) {
        console.error(`Error fetching count from ${endpoint}:`, error);
        return 0;
    }
}

function updateStatCard(elementId, count) {
    const element = document.getElementById(elementId);
    if (element) {
        //Add animation effect
        element.style.opacity = '0.3';

        setTimeout(() => {
            element.textContent = count.toLocaleString();
            element.style.opacity = '1';

            //Add a subtle bounce animation
            element.style.transform = 'scale(1.1)';
            setTimeout(() => {
                element.style.transform = 'scale(1)';
            }, 200);
        }, 300);
    }
}

function setupEventListeners() {
    console.log('Setting up event listeners...');

    //Logout button functionality
    const logoutBtn = document.getElementById('logout-btn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', handleLogout);
        console.log('Logout button event listener added');
    } else {
        console.warn('Logout button not found');
    }

    //Menu item highlighting
    highlightCurrentPage();

    //Add click handlers for stat cards
    addStatCardClickHandlers();

    //Add refresh functionality
    addRefreshFunctionality();

    console.log('Event listeners setup completed');
}

function handleLogout() {
    console.log('Logout button clicked');

    //Show confirmation dialog
    if (confirm('Are you sure you want to logout?')) {
        console.log('Logout confirmed by user');

        //Show loading state on logout button
        const logoutBtn = document.getElementById('logout-btn');
        let originalText = 'Logout';

        if (logoutBtn) {
            originalText = logoutBtn.textContent;
            logoutBtn.textContent = 'Logging out...';
            logoutBtn.disabled = true;
        }

        //Perform logout
        try {
            AuthManager.logout();
        } catch (error) {
            console.error('Error during logout:', error);
            //Restore button state if logout fails
            if (logoutBtn) {
                logoutBtn.textContent = originalText;
                logoutBtn.disabled = false;
            }
            alert('Logout failed. Please try again.');
        }
    } else {
        console.log('Logout cancelled by user');
    }
}

function highlightCurrentPage() {
    const currentPath = window.location.pathname;
    const menuLinks = document.querySelectorAll('.sidebar-menu a');

    menuLinks.forEach(link => {
        link.classList.remove('active');

        //Check if the link href matches current path
        const linkPath = new URL(link.href).pathname;
        if (linkPath === currentPath ||
            (currentPath === '/' && linkPath.includes('index.html')) ||
            (currentPath.includes('index.html') && linkPath === '/')) {
            link.classList.add('active');
        }
    });
}

function addStatCardClickHandlers() {
    //Add click handlers to stat cards for navigation
    const statCards = document.querySelectorAll('.stat-card');

    statCards.forEach(card => {
        const link = card.querySelector('.stat-link');
        if (link) {
            card.style.cursor = 'pointer';
            card.addEventListener('click', (e) => {
                //Don't trigger if clicking on the actual link
                if (e.target !== link) {
                    window.location.href = link.href;
                }
            });
        }
    });
}

function addRefreshFunctionality() {
    //Add keyboard shortcut for refresh (Ctrl+R or F5)
    document.addEventListener('keydown', (e) => {
        if ((e.ctrlKey && e.key === 'r') || e.key === 'F5') {
            e.preventDefault();
            refreshDashboard();
        }
    });

    //Add double-click to refresh stats
    document.addEventListener('dblclick', (e) => {
        if (e.target.closest('.stats-grid')) {
            refreshDashboard();
        }
    });
}

function refreshDashboard() {
    console.log('Refreshing dashboard...');
    loadDashboardStats();

    //Show brief feedback
    const header = document.querySelector('.content-header h1');
    if (header) {
        const originalText = header.textContent;
        header.textContent = 'Dashboard Overview (Refreshing...)';
        setTimeout(() => {
            header.textContent = originalText;
        }, 1000);
    }
}

//Add visibility change handler to refresh when tab becomes active
document.addEventListener('visibilitychange', () => {
    if (!document.hidden && typeof AuthManager !== 'undefined' && AuthManager.getCurrentUser()) {
        console.log('Tab became active, refreshing dashboard...');
        loadDashboardStats();
    }
});

//Global error handler for dashboard
window.addEventListener('error', (e) => {
    console.error('Dashboard error:', e.error);
});

//Export functions for debugging (development only)
if (typeof window !== 'undefined') {
    window.dashboardDebug = {
        refreshStats: loadDashboardStats,
        refreshDashboard: refreshDashboard,
        getCurrentUser: () => AuthManager.getCurrentUser()
    };
}

//Check authentication and initialize
async function checkAuthAndInit() {
    try {
        console.log('Checking authentication...');
        const isAuthenticated = await AuthManager.isAuthenticated();
        if (!isAuthenticated) {
            console.log('User not authenticated, redirecting to login...');
            window.location.href = '/login.html';
            return;
        }

        console.log('User authenticated, initializing Dashboard...');
        initializeDashboard();
    } catch (error) {
        console.error('Authentication check failed:', error);
        window.location.href = '/login.html';
    }
}

//Start the process when DOM is ready
document.addEventListener("DOMContentLoaded", function () {
    console.log('DOM loaded, waiting for AuthManager...');
    waitForAuthManager(checkAuthAndInit);
});

//Refresh dashboard stats periodically (every 5 minutes)
setInterval(() => {
    if (typeof AuthManager !== 'undefined' && AuthManager.getCurrentUser()) {
        console.log('Auto-refreshing dashboard stats...');
        loadDashboardStats();
    }
}, 5 * 60 * 1000);