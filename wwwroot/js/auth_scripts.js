//Authentication Manager
class AuthManager {
    static API_BASE = '/api/auth';

    //Initialize login page
    static initLogin() {
        const form = document.getElementById('login-form');
        const usernameInput = document.getElementById('username');
        const passwordInput = document.getElementById('password');

        //Clear any existing session
        this.clearSession();

        //Form submission
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            await this.handleLogin();
        });

        //Clear errors on input
        usernameInput.addEventListener('input', () => this.clearFieldError('username'));
        passwordInput.addEventListener('input', () => this.clearFieldError('password'));

        //Focus first input
        usernameInput.focus();
    }

    //nitialize register page
    static initRegister() {
        const form = document.getElementById('register-form');
        const inputs = form.querySelectorAll('input');

        //Clear any existing session
        this.clearSession();

        //Form submission
        form.addEventListener('submit', async (e) => {
            e.preventDefault();
            await this.handleRegister();
        });

        //Clear errors on input
        inputs.forEach(input => {
            input.addEventListener('input', () => this.clearFieldError(input.name));
        });

        //Password confirmation validation
        const passwordInput = document.getElementById('password');
        const confirmPasswordInput = document.getElementById('confirm-password');

        if (confirmPasswordInput) {
            confirmPasswordInput.addEventListener('input', () => {
                if (passwordInput.value !== confirmPasswordInput.value) {
                    this.showFieldError('confirm-password', 'Passwords do not match');
                } else {
                    this.clearFieldError('confirm-password');
                }
            });
        }

        //Focus first input
        document.getElementById('name').focus();
    }

    //Handle login
    static async handleLogin() {
        const username = document.getElementById('username').value.trim();
        const password = document.getElementById('password').value;

        //Clear previous errors
        this.clearAllErrors();

        //Validate inputs
        if (!this.validateLoginForm(username, password)) {
            return;
        }

        //Show loading state
        this.setLoadingState('login-btn', true);

        try {
            const response = await fetch(`${this.API_BASE}/login`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    username: username,
                    password: password
                })
            });

            const data = await response.json();

            if (response.ok && data.success) {
                //Store session
                this.setSession(data.sessionId, data.user);

                //Show success message
                this.showSuccess('Login successful! Redirecting...');

                //Redirect to dashboard
                setTimeout(() => {
                    window.location.href = '/index.html';
                }, 1000);
            } else {
                this.showError(data.message || 'Login failed');
            }
        } catch (error) {
            console.error('Login error:', error);
            this.showError('Network error. Please try again.');
        } finally {
            this.setLoadingState('login-btn', false);
        }
    }

    //Handle registration
    static async handleRegister() {
        const name = document.getElementById('name').value.trim();
        const username = document.getElementById('username').value.trim();
        const email = document.getElementById('email').value.trim();
        const password = document.getElementById('password').value;
        const confirmPassword = document.getElementById('confirm-password').value;

        //Clear previous errors
        this.clearAllErrors();

        //Validate inputs
        if (!this.validateRegisterForm(name, username, email, password, confirmPassword)) {
            return;
        }

        //Show loading state
        this.setLoadingState('register-btn', true);

        try {
            const response = await fetch(`${this.API_BASE}/register`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    name: name,
                    username: username,
                    email: email,
                    password: password
                })
            });

            const data = await response.json();

            if (response.ok && data.success) {
                //Store session
                this.setSession(data.sessionId, data.user);

                //Show success message
                this.showSuccess('Registration successful! Redirecting...');

                //Redirect to dashboard
                setTimeout(() => {
                    window.location.href = '/index.html';
                }, 1500);
            } else {
                this.showError(data.message || 'Registration failed');
            }
        } catch (error) {
            console.error('Registration error:', error);
            this.showError('Network error. Please try again.');
        } finally {
            this.setLoadingState('register-btn', false);
        }
    }

    //Validate login form
    static validateLoginForm(username, password) {
        let isValid = true;

        if (!username) {
            this.showFieldError('username', 'Username is required');
            isValid = false;
        }

        if (!password) {
            this.showFieldError('password', 'Password is required');
            isValid = false;
        }

        return isValid;
    }

    //Validate register form
    static validateRegisterForm(name, username, email, password, confirmPassword) {
        let isValid = true;

        if (!name) {
            this.showFieldError('name', 'Full name is required');
            isValid = false;
        }

        if (!username) {
            this.showFieldError('username', 'Username is required');
            isValid = false;
        } else if (username.length < 3) {
            this.showFieldError('username', 'Username must be at least 3 characters');
            isValid = false;
        }

        if (!email) {
            this.showFieldError('email', 'Email is required');
            isValid = false;
        } else if (!this.isValidEmail(email)) {
            this.showFieldError('email', 'Please enter a valid email address');
            isValid = false;
        }

        if (!password) {
            this.showFieldError('password', 'Password is required');
            isValid = false;
        } else if (password.length < 6) {
            this.showFieldError('password', 'Password must be at least 6 characters');
            isValid = false;
        }

        if (!confirmPassword) {
            this.showFieldError('confirm-password', 'Please confirm your password');
            isValid = false;
        } else if (password !== confirmPassword) {
            this.showFieldError('confirm-password', 'Passwords do not match');
            isValid = false;
        }

        return isValid;
    }

    //Email validation
    static isValidEmail(email) {
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        return emailRegex.test(email);
    }

    //Session management
    static setSession(sessionId, user) {
        //Store in both cookie and localStorage for redundancy
        document.cookie = `SessionId=${sessionId}; path=/; max-age=${7 * 24 * 60 * 60}; SameSite=Strict`;
        localStorage.setItem('sessionId', sessionId);
        localStorage.setItem('user', JSON.stringify(user));
    }

    static getSession() {
        const sessionId = localStorage.getItem('sessionId');
        const user = localStorage.getItem('user');

        if (sessionId && user) {
            try {
                return {
                    sessionId: sessionId,
                    user: JSON.parse(user)
                };
            } catch (error) {
                console.error('Error parsing user data:', error);
                this.clearSession();
                return null;
            }
        }
        return null;
    }

    static clearSession() {
        //Clear cookie ehhehehehe
        document.cookie = 'SessionId=; path=/; expires=Thu, 01 Jan 1970 00:00:01 GMT;';

        //Clear localStorage
        localStorage.removeItem('sessionId');
        localStorage.removeItem('user');
    }

    //Check if user is authenticated
    static async isAuthenticated() {
        const session = this.getSession();
        if (!session) {
            return false;
        }

        try {
            const response = await fetch(`${this.API_BASE}/validate`, {
                headers: {
                    'Authorization': `Bearer ${session.sessionId}`
                }
            });

            if (response.ok) {
                const userData = await response.json();
                //Update stored user data
                localStorage.setItem('user', JSON.stringify(userData));
                return true;
            } else {
                console.log('Session validation failed:', response.status, response.statusText);
                this.clearSession();
                return false;
            }
        } catch (error) {
            console.error('Session validation error:', error);
            return false;
        }
    }

    //Logout
    static async logout() {
        const session = this.getSession();

        if (session) {
            try {
                await fetch(`${this.API_BASE}/logout`, {
                    method: 'POST',
                    headers: {
                        'Authorization': `Bearer ${session.sessionId}`
                    }
                });
            } catch (error) {
                console.error('Logout error:', error);
            }
        }

        this.clearSession();
        window.location.href = '/login.html';
    }

    //Make authenticated API requests
    static async makeAuthenticatedRequest(url, options = {}) {
        const session = this.getSession();

        if (!session) {
            console.log('No session found, redirecting to login');
            window.location.href = '/login.html';
            return null;
        }

        const headers = {
            'Authorization': `Bearer ${session.sessionId}`,
            ...options.headers
        };

        //Don't set Content-Type for FormData
        if (!(options.body instanceof FormData)) {
            headers['Content-Type'] = 'application/json';
        }

        try {
            console.log(`Making authenticated request to: ${url}`);

            const response = await fetch(url, {
                ...options,
                headers
            });

            console.log(`Response status: ${response.status} for ${url}`);

            if (response.status === 401) {
                //Session expired or invalid
                console.log('401 Unauthorized, clearing session and redirecting to login');
                this.clearSession();
                window.location.href = '/login.html';
                return null;
            }

            return response;
        } catch (error) {
            console.error('API request error:', error);
            throw error;
        }
    }

    //Check authentication status on page load
    static async checkAuthOnLoad() {
        const currentPage = window.location.pathname;
        const isAuthPage = currentPage.includes('login.html') || currentPage.includes('register.html');

        console.log(`Checking auth on load for page: ${currentPage}, isAuthPage: ${isAuthPage}`);

        if (isAuthPage) {
            //If user is already authenticated, redirect to dashboard
            if (await this.isAuthenticated()) {
                console.log('User already authenticated, redirecting to dashboard');
                window.location.href = '/index.html';
            }
        } else {
            //If user is not authenticated, redirect to login
            if (!(await this.isAuthenticated())) {
                console.log('User not authenticated, redirecting to login');
                window.location.href = '/login.html';
            }
        }
    }

    //Get current user info
    static getCurrentUser() {
        const session = this.getSession();
        return session ? session.user : null;
    }

    //UI Helper methods
    static showError(message) {
        const errorElement = document.getElementById('error-message');
        if (errorElement) {
            errorElement.textContent = message;
            errorElement.style.display = 'block';
        }
        console.error('Auth Error:', message);
    }

    static hideError() {
        const errorElement = document.getElementById('error-message');
        if (errorElement) {
            errorElement.style.display = 'none';
        }
    }

    static showSuccess(message) {
        const successElement = document.getElementById('success-message');
        if (successElement) {
            successElement.textContent = message;
            successElement.style.display = 'block';
        }
        console.log('Auth Success:', message);
    }

    static hideSuccess() {
        const successElement = document.getElementById('success-message');
        if (successElement) {
            successElement.style.display = 'none';
        }
    }

    static setLoadingState(buttonId, isLoading) {
        const button = document.getElementById(buttonId);
        if (!button) return;

        const btnText = button.querySelector('.btn-text');
        const btnLoader = button.querySelector('.btn-loader');

        if (isLoading) {
            button.disabled = true;
            if (btnText) btnText.style.display = 'none';
            if (btnLoader) btnLoader.style.display = 'inline-flex';
        } else {
            button.disabled = false;
            if (btnText) btnText.style.display = 'inline-block';
            if (btnLoader) btnLoader.style.display = 'none';
        }
    }

    static showFieldError(fieldName, message) {
        const errorElement = document.getElementById(`${fieldName}-error`);
        const inputElement = document.getElementById(fieldName);

        if (errorElement) {
            errorElement.textContent = message;
        }

        if (inputElement) {
            inputElement.classList.add('error');
        }
    }

    static clearFieldError(fieldName) {
        const errorElement = document.getElementById(`${fieldName}-error`);
        const inputElement = document.getElementById(fieldName);

        if (errorElement) {
            errorElement.textContent = '';
        }

        if (inputElement) {
            inputElement.classList.remove('error');
        }
    }

    static clearAllErrors() {
        const errorElements = document.querySelectorAll('.error-message');
        const inputElements = document.querySelectorAll('input.error');

        errorElements.forEach(element => {
            element.textContent = '';
        });

        inputElements.forEach(element => {
            element.classList.remove('error');
        });

        this.hideError();
        this.hideSuccess();
    }
}

//check authentication on page load for non-auth pages
document.addEventListener('DOMContentLoaded', () => {
    console.log('DOM Content Loaded, checking authentication...');
    const currentPage = window.location.pathname;
    const isAuthPage = currentPage.includes('login.html') || currentPage.includes('register.html');

    if (!isAuthPage) {
        AuthManager.checkAuthOnLoad();
    }
});

//Global logout function for navigation
window.logout = () => {
    AuthManager.logout();
};

//Export for use in other scripts
window.AuthManager = AuthManager;