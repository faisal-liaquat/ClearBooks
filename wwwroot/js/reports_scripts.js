//Wait for AuthManager to be available
function waitForAuthManager(callback) {
    if (typeof window.AuthManager !== 'undefined') {
        callback();
    } else {
        console.log('Waiting for AuthManager to load...');
        setTimeout(() => waitForAuthManager(callback), 100);
    }
}

//Initialize when both DOM and AuthManager are ready
function initializeReports() {
    console.log('Initializing Reports module...');

    //State Management
    let currentReport = 'general-ledger';
    let accounts = [];

    //DOM Elements
    const tabButtons = document.querySelectorAll('.tab-btn');
    const filterSections = document.querySelectorAll('.filter-section');
    const reportSections = document.querySelectorAll('.report-section');
    const loadingIndicator = document.getElementById('loading-indicator');
    const errorContainer = document.getElementById('error-container');

    //Initialize the page
    initialize();

    async function initialize() {
        try {
            await fetchAccounts();
            setupEventListeners();
            setDefaultDates();
            console.log('Reports module initialized successfully');
        } catch (error) {
            console.error('Error initializing reports:', error);
            showError('Failed to initialize reports module');
        }
    }

    function setupEventListeners() {
        //Tab switching
        tabButtons.forEach(button => {
            button.addEventListener('click', () => {
                const reportType = button.getAttribute('data-report');
                switchReport(reportType);
            });
        });

        //Report generation buttons
        document.getElementById('generate-gl-btn').addEventListener('click', generateGeneralLedger);
        document.getElementById('generate-tb-btn').addEventListener('click', generateTrialBalance);
        document.getElementById('generate-is-btn').addEventListener('click', generateIncomeStatement);
        document.getElementById('generate-pl-btn').addEventListener('click', generateProfitLoss);
        document.getElementById('generate-bs-btn').addEventListener('click', generateBalanceSheet);
        document.getElementById('generate-al-btn').addEventListener('click', generateAccountLedger);

        //Export buttons
        document.getElementById('export-gl-csv-btn').addEventListener('click', () => exportToCSV('general-ledger'));
        document.getElementById('export-gl-pdf-btn').addEventListener('click', () => exportToPDF('general-ledger'));
        document.getElementById('export-tb-csv-btn').addEventListener('click', () => exportToCSV('trial-balance'));
        document.getElementById('export-tb-pdf-btn').addEventListener('click', () => exportToPDF('trial-balance'));
        document.getElementById('export-is-csv-btn').addEventListener('click', () => exportToCSV('income-statement'));
        document.getElementById('export-is-pdf-btn').addEventListener('click', () => exportToPDF('income-statement'));
        document.getElementById('export-pl-csv-btn').addEventListener('click', () => exportToCSV('profit-loss'));
        document.getElementById('export-pl-pdf-btn').addEventListener('click', () => exportToPDF('profit-loss'));
        document.getElementById('export-bs-csv-btn').addEventListener('click', () => exportToCSV('balance-sheet'));
        document.getElementById('export-bs-pdf-btn').addEventListener('click', () => exportToPDF('balance-sheet'));
        document.getElementById('export-al-csv-btn').addEventListener('click', () => exportToCSV('account-ledger'));
        document.getElementById('export-al-pdf-btn').addEventListener('click', () => exportToPDF('account-ledger'));

        //Print buttons
        document.getElementById('print-gl-btn').addEventListener('click', () => printReport('general-ledger'));
        document.getElementById('print-tb-btn').addEventListener('click', () => printReport('trial-balance'));
        document.getElementById('print-is-btn').addEventListener('click', () => printReport('income-statement'));
        document.getElementById('print-pl-btn').addEventListener('click', () => printReport('profit-loss'));
        document.getElementById('print-bs-btn').addEventListener('click', () => printReport('balance-sheet'));
        document.getElementById('print-al-btn').addEventListener('click', () => printReport('account-ledger'));
    }

    function setDefaultDates() {
        const today = new Date().toISOString().split('T')[0];
        const startOfYear = new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0];

        //Set default dates for all date inputs
        document.getElementById('gl-from-date').value = startOfYear;
        document.getElementById('gl-to-date').value = today;
        document.getElementById('tb-as-of-date').value = today;
        document.getElementById('is-from-date').value = startOfYear;
        document.getElementById('is-to-date').value = today;
        document.getElementById('pl-from-date').value = startOfYear;
        document.getElementById('pl-to-date').value = today;
        document.getElementById('bs-as-of-date').value = today;
        document.getElementById('al-from-date').value = startOfYear;
        document.getElementById('al-to-date').value = today;
    }

    async function fetchAccounts() {
        try {
            const response = await AuthManager.makeAuthenticatedRequest('/api/ChartOfAccounts');
            if (!response || !response.ok) {
                throw new Error('Failed to fetch accounts');
            }

            accounts = await response.json();
            populateAccountDropdowns();
        } catch (error) {
            console.error('Error fetching accounts:', error);
            showError('Failed to load accounts');
        }
    }

    function populateAccountDropdowns() {
        const glAccountSelect = document.getElementById('gl-account');
        const alAccountSelect = document.getElementById('al-account');

        //Clear existing options (except the first one)
        glAccountSelect.innerHTML = '<option value="">All Accounts</option>';
        alAccountSelect.innerHTML = '<option value="">Select Account</option>';

        accounts.forEach(account => {
            const option1 = document.createElement('option');
            option1.value = account.accountId;
            option1.textContent = `${account.accountNumber} - ${account.accountName}`;
            glAccountSelect.appendChild(option1);

            const option2 = document.createElement('option');
            option2.value = account.accountId;
            option2.textContent = `${account.accountNumber} - ${account.accountName}`;
            alAccountSelect.appendChild(option2);
        });
    }

    function switchReport(reportType) {
        currentReport = reportType;

        //Update tab buttons
        tabButtons.forEach(btn => {
            btn.classList.remove('active');
            if (btn.getAttribute('data-report') === reportType) {
                btn.classList.add('active');
            }
        });

        //Update filter sections
        filterSections.forEach(section => {
            section.classList.remove('active');
        });
        document.getElementById(`${reportType}-filters`).classList.add('active');

        //Update report sections
        reportSections.forEach(section => {
            section.classList.remove('active');
        });
        document.getElementById(`${reportType}-report`).classList.add('active');
    }

    //Report Generation Functions
    async function generateGeneralLedger() {
        try {
            showLoading();
            hideError();

            const fromDate = document.getElementById('gl-from-date').value;
            const toDate = document.getElementById('gl-to-date').value;
            const accountId = document.getElementById('gl-account').value;

            if (!fromDate || !toDate) {
                throw new Error('Please select both from and to dates');
            }

            let url = `/api/Reports/GeneralLedger?fromDate=${fromDate}&toDate=${toDate}`;
            if (accountId) {
                url += `&accountId=${accountId}`;
            }

            const response = await AuthManager.makeAuthenticatedRequest(url);
            if (!response || !response.ok) {
                throw new Error('Failed to generate general ledger report');
            }

            const data = await response.json();
            renderGeneralLedger(data);
        } catch (error) {
            console.error('Error generating general ledger:', error);
            showError(error.message);
        } finally {
            hideLoading();
        }
    }

    async function generateTrialBalance() {
        try {
            showLoading();
            hideError();

            const asOfDate = document.getElementById('tb-as-of-date').value;
            if (!asOfDate) {
                throw new Error('Please select an as-of date');
            }

            const response = await AuthManager.makeAuthenticatedRequest(
                `/api/Reports/TrialBalance?asOfDate=${asOfDate}`
            );
            if (!response || !response.ok) {
                throw new Error('Failed to generate trial balance report');
            }

            const data = await response.json();
            renderTrialBalance(data);
        } catch (error) {
            console.error('Error generating trial balance:', error);
            showError(error.message);
        } finally {
            hideLoading();
        }
    }

    async function generateIncomeStatement() {
        try {
            showLoading();
            hideError();

            const fromDate = document.getElementById('is-from-date').value;
            const toDate = document.getElementById('is-to-date').value;

            if (!fromDate || !toDate) {
                throw new Error('Please select both from and to dates');
            }

            const response = await AuthManager.makeAuthenticatedRequest(
                `/api/Reports/IncomeStatement?fromDate=${fromDate}&toDate=${toDate}`
            );
            if (!response || !response.ok) {
                throw new Error('Failed to generate income statement');
            }

            const data = await response.json();
            renderIncomeStatement(data);
        } catch (error) {
            console.error('Error generating income statement:', error);
            showError(error.message);
        } finally {
            hideLoading();
        }
    }

    //P&L Generation
    async function generateProfitLoss() {
        try {
            showLoading();
            hideError();

            const fromDate = document.getElementById('pl-from-date').value;
            const toDate = document.getElementById('pl-to-date').value;
            const comparison = document.getElementById('pl-comparison').value;

            //Validation
            if (!fromDate || !toDate) {
                throw new Error('Please select both from and to dates');
            }

            if (new Date(fromDate) > new Date(toDate)) {
                throw new Error('From date cannot be later than to date');
            }

            //Check if date range is too large (more than 5 years)
            const daysDiff = (new Date(toDate) - new Date(fromDate)) / (1000 * 60 * 60 * 24);
            if (daysDiff > 1825) { // 5 years
                const confirmLarge = confirm('You selected a date range longer than 5 years. This may take some time to process. Continue?');
                if (!confirmLarge) {
                    hideLoading();
                    return;
                }
            }

            let url = `/api/Reports/ProfitLoss?fromDate=${fromDate}&toDate=${toDate}`;
            if (comparison && comparison !== 'none') {
                url += `&comparison=${comparison}`;
            }

            const response = await AuthManager.makeAuthenticatedRequest(url);

            if (!response) {
                throw new Error('No response received from server');
            }

            if (!response.ok) {
                const errorData = await response.text();
                let errorMessage = 'Failed to generate profit & loss report';

                try {
                    const errorJson = JSON.parse(errorData);
                    errorMessage = errorJson.message || errorMessage;
                } catch (e) {
                    //If not JSON, use status text
                    errorMessage = response.statusText || errorMessage;
                }

                throw new Error(errorMessage);
            }

            const data = await response.json();

            //Validate response data structure
            if (!data || typeof data !== 'object') {
                throw new Error('Invalid response format received');
            }

            renderProfitLoss(data);

            //Update period display
            const periodDisplay = document.getElementById('pl-period-display');
            if (periodDisplay) {
                periodDisplay.textContent = `${formatDate(fromDate)} to ${formatDate(toDate)}`;
            }

            //Log successful generation for debugging
            console.log('P&L report generated successfully', {
                fromDate,
                toDate,
                comparison,
                recordCount: (data.revenues?.length || 0) + (data.expenses?.length || 0)
            });

        } catch (error) {
            console.error('Error generating profit & loss:', error);

            // error messages based on error type
            let userMessage = error.message;

            if (error.name === 'TypeError' && error.message.includes('fetch')) {
                userMessage = 'Network error. Please check your internet connection and try again.';
            } else if (error.message.includes('401')) {
                userMessage = 'Session expired. Please log in again.';
                setTimeout(() => window.location.href = '/login.html', 2000);
            } else if (error.message.includes('403')) {
                userMessage = 'You do not have permission to access this report.';
            } else if (error.message.includes('500')) {
                userMessage = 'Server error. Please try again later or contact support.';
            }

            showError(userMessage);
        } finally {
            hideLoading();
        }
    }

    async function generateBalanceSheet() {
        try {
            showLoading();
            hideError();

            const asOfDate = document.getElementById('bs-as-of-date').value;
            if (!asOfDate) {
                throw new Error('Please select an as-of date');
            }

            const response = await AuthManager.makeAuthenticatedRequest(
                `/api/Reports/BalanceSheet?asOfDate=${asOfDate}`
            );
            if (!response || !response.ok) {
                throw new Error('Failed to generate balance sheet');
            }

            const data = await response.json();
            renderBalanceSheet(data);
        } catch (error) {
            console.error('Error generating balance sheet:', error);
            showError(error.message);
        } finally {
            hideLoading();
        }
    }

    async function generateAccountLedger() {
        try {
            showLoading();
            hideError();

            const accountId = document.getElementById('al-account').value;
            const fromDate = document.getElementById('al-from-date').value;
            const toDate = document.getElementById('al-to-date').value;

            if (!accountId) {
                throw new Error('Please select an account');
            }
            if (!fromDate || !toDate) {
                throw new Error('Please select both from and to dates');
            }

            const response = await AuthManager.makeAuthenticatedRequest(
                `/api/Reports/AccountLedger/${accountId}?fromDate=${fromDate}&toDate=${toDate}`
            );
            if (!response || !response.ok) {
                throw new Error('Failed to generate account ledger');
            }

            const data = await response.json();
            renderAccountLedger(data);
        } catch (error) {
            console.error('Error generating account ledger:', error);
            showError(error.message);
        } finally {
            hideLoading();
        }
    }

    //Rendering Functions
    function renderGeneralLedger(data) {
        const tbody = document.querySelector('#gl-table tbody');
        tbody.innerHTML = '';

        data.entries.forEach(entry => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${formatDate(entry.date)}</td>
                <td>${entry.voucherNumber}</td>
                <td>${entry.accountNumber} - ${entry.accountName}</td>
                <td>${entry.description}</td>
                <td class="amount">${formatCurrency(entry.debitAmount)}</td>
                <td class="amount">${formatCurrency(entry.creditAmount)}</td>
                <td class="amount">${formatCurrency(entry.balance)}</td>
            `;
            tbody.appendChild(row);
        });

        document.getElementById('gl-total-debits').textContent = formatCurrency(data.totalDebits);
        document.getElementById('gl-total-credits').textContent = formatCurrency(data.totalCredits);
    }

    function renderTrialBalance(data) {
        const tbody = document.querySelector('#tb-table tbody');
        tbody.innerHTML = '';

        data.entries.forEach(entry => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${entry.accountNumber}</td>
                <td>${entry.accountName}</td>
                <td>${entry.accountType}</td>
                <td class="amount">${formatCurrency(entry.debitBalance)}</td>
                <td class="amount">${formatCurrency(entry.creditBalance)}</td>
            `;
            tbody.appendChild(row);
        });

        document.getElementById('tb-total-debits').textContent = formatCurrency(data.totalDebits);
        document.getElementById('tb-total-credits').textContent = formatCurrency(data.totalCredits);
    }

    function renderIncomeStatement(data) {
        //Render revenues
        const revenueTbody = document.querySelector('#is-revenue-table tbody');
        revenueTbody.innerHTML = '';
        data.revenues.forEach(revenue => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${revenue.accountNumber}</td>
                <td>${revenue.accountName}</td>
                <td class="amount">${formatCurrency(revenue.amount)}</td>
            `;
            revenueTbody.appendChild(row);
        });

        //Render expenses
        const expenseTbody = document.querySelector('#is-expense-table tbody');
        expenseTbody.innerHTML = '';
        data.expenses.forEach(expense => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${expense.accountNumber}</td>
                <td>${expense.accountName}</td>
                <td class="amount">${formatCurrency(expense.amount)}</td>
            `;
            expenseTbody.appendChild(row);
        });

        //Update totals
        document.getElementById('is-total-revenue').textContent = formatCurrency(data.totalRevenue);
        document.getElementById('is-total-expenses').textContent = formatCurrency(data.totalExpenses);
        document.getElementById('is-net-income').textContent = formatCurrency(data.netIncome);

        //Color the net income based on positive/negative
        const netIncomeElement = document.getElementById('is-net-income');
        if (data.netIncome < 0) {
            netIncomeElement.style.color = '#e74c3c';
        } else {
            netIncomeElement.style.color = '#27ae60';
        }
    }

    //P&L rendering with comprehensive data validation
    function renderProfitLoss(data) {
        try {
            //Validate data structure
            const requiredFields = ['revenues', 'expenses', 'totalRevenue', 'totalExpenses', 'netIncome'];
            for (const field of requiredFields) {
                if (!(field in data)) {
                    throw new Error(`Missing required field: ${field}`);
                }
            }

            //Ensure arrays are actually arrays
            if (!Array.isArray(data.revenues)) {
                console.warn('Revenues is not an array, converting:', data.revenues);
                data.revenues = [];
            }
            if (!Array.isArray(data.expenses)) {
                console.warn('Expenses is not an array, converting:', data.expenses);
                data.expenses = [];
            }

            //Enhanced revenue categorization
            const categorizedRevenues = categorizeRevenues(data.revenues);
            renderRevenueCategories(categorizedRevenues);

            //Enhanced expense categorization
            const categorizedExpenses = categorizeExpenses(data.expenses);
            renderExpenseCategories(categorizedExpenses);

            //Calculate and display key metrics
            const metrics = calculatePLMetrics(data);
            renderPLMetrics(metrics);

            //Render comparison data if available
            if (data.comparison) {
                renderComparisonAnalysis(data, data.comparison);
            }

            //Update summary section
            updatePLSummary(data, metrics);

            console.log('P&L report rendered successfully');

        } catch (error) {
            console.error('Error rendering P&L report:', error);
            showError('Error displaying report data: ' + error.message);
        }
    }

    //Revenue categorization for better P&L structure
    function categorizeRevenues(revenues) {
        const categories = {
            operating: [],
            nonOperating: [],
            other: []
        };

        revenues.forEach(revenue => {
            //Validate revenue object
            if (!revenue || typeof revenue !== 'object') {
                console.warn('Invalid revenue object:', revenue);
                return;
            }

            //Ensure required fields exist
            revenue.accountNumber = revenue.accountNumber || 'N/A';
            revenue.accountName = revenue.accountName || 'Unknown Account';
            revenue.amount = parseFloat(revenue.amount) || 0;

            //Categorize based on account type or name patterns
            const accountName = (revenue.accountName || '').toLowerCase();
            const accountNumber = (revenue.accountNumber || '').toString();

            if (accountName.includes('sales') ||
                accountName.includes('service') ||
                accountName.includes('revenue') ||
                accountNumber.startsWith('4')) {
                categories.operating.push(revenue);
            } else if (accountName.includes('interest') ||
                accountName.includes('investment') ||
                accountName.includes('gain')) {
                categories.nonOperating.push(revenue);
            } else {
                categories.other.push(revenue);
            }
        });

        return categories;
    }

    //Expense categorization for detailed P&L
    function categorizeExpenses(expenses) {
        const categories = {
            costOfSales: [],
            operatingExpenses: [],
            administrativeExpenses: [],
            financialExpenses: [],
            other: []
        };

        expenses.forEach(expense => {
            //Validate expense object
            if (!expense || typeof expense !== 'object') {
                console.warn('Invalid expense object:', expense);
                return;
            }

            //Ensure required fields exist
            expense.accountNumber = expense.accountNumber || 'N/A';
            expense.accountName = expense.accountName || 'Unknown Account';
            expense.amount = parseFloat(expense.amount) || 0;

            const accountName = (expense.accountName || '').toLowerCase();
            const accountNumber = (expense.accountNumber || '').toString();

            if (accountName.includes('cost of') ||
                accountName.includes('cogs') ||
                accountName.includes('materials') ||
                accountNumber.startsWith('50')) {
                categories.costOfSales.push(expense);
            } else if (accountName.includes('salary') ||
                accountName.includes('rent') ||
                accountName.includes('utilities') ||
                accountNumber.startsWith('51')) {
                categories.operatingExpenses.push(expense);
            } else if (accountName.includes('admin') ||
                accountName.includes('office') ||
                accountName.includes('management') ||
                accountNumber.startsWith('52')) {
                categories.administrativeExpenses.push(expense);
            } else if (accountName.includes('interest') ||
                accountName.includes('bank') ||
                accountName.includes('loan') ||
                accountNumber.startsWith('53')) {
                categories.financialExpenses.push(expense);
            } else {
                categories.other.push(expense);
            }
        });

        return categories;
    }

    //Calculate comprehensive P&L metrics
    function calculatePLMetrics(data) {
        const revenue = parseFloat(data.totalRevenue) || 0;
        const expenses = parseFloat(data.totalExpenses) || 0;
        const netIncome = parseFloat(data.netIncome) || 0;

        return {
            grossProfit: revenue - (data.costOfSales || 0),
            grossProfitMargin: revenue > 0 ? ((revenue - (data.costOfSales || 0)) / revenue * 100) : 0,
            operatingIncome: revenue - (data.operatingExpenses || 0),
            operatingMargin: revenue > 0 ? ((revenue - (data.operatingExpenses || 0)) / revenue * 100) : 0,
            netProfitMargin: revenue > 0 ? (netIncome / revenue * 100) : 0,
            expenseRatio: revenue > 0 ? (expenses / revenue * 100) : 0,
            revenueGrowth: data.comparison ? calculateGrowthRate(revenue, data.comparison.totalRevenue) : null,
            profitGrowth: data.comparison ? calculateGrowthRate(netIncome, data.comparison.netIncome) : null
        };
    }

    //Calculate growth rate safely
    function calculateGrowthRate(current, previous) {
        if (!previous || previous === 0) return null;
        return ((current - previous) / Math.abs(previous)) * 100;
    }

    //Render revenue categories
    function renderRevenueCategories(categories) {
        const container = document.getElementById('pl-revenue-categories');
        if (!container) return;

        let html = '';
        let totalRevenue = 0;

        Object.entries(categories).forEach(([categoryName, items]) => {
            if (items.length === 0) return;

            const categoryTotal = items.reduce((sum, item) => sum + item.amount, 0);
            totalRevenue += categoryTotal;

            html += `
                <div class="pl-category">
                    <h5>${formatCategoryName(categoryName)} Revenue</h5>
                    <table class="report-table">
                        <thead>
                            <tr>
                                <th>Account Number</th>
                                <th>Account Name</th>
                                <th>Amount</th>
                            </tr>
                        </thead>
                        <tbody>
            `;

            items.forEach(item => {
                html += `
                    <tr>
                        <td>${escapeHtml(item.accountNumber)}</td>
                        <td>${escapeHtml(item.accountName)}</td>
                        <td class="amount">${formatCurrency(item.amount)}</td>
                    </tr>
                `;
            });

            html += `
                        </tbody>
                        <tfoot>
                            <tr>
                                <td colspan="2"><strong>Total ${formatCategoryName(categoryName)} Revenue</strong></td>
                                <td class="amount"><strong>${formatCurrency(categoryTotal)}</strong></td>
                            </tr>
                        </tfoot>
                    </table>
                </div>
            `;
        });

        container.innerHTML = html;

        //Update total revenue display
        const totalElement = document.getElementById('pl-total-revenue');
        if (totalElement) {
            totalElement.textContent = formatCurrency(totalRevenue);
        }
    }

    //Render expense categories
    function renderExpenseCategories(categories) {
        const container = document.getElementById('pl-expense-categories');
        if (!container) return;

        let html = '';
        let totalExpenses = 0;

        Object.entries(categories).forEach(([categoryName, items]) => {
            if (items.length === 0) return;

            const categoryTotal = items.reduce((sum, item) => sum + item.amount, 0);
            totalExpenses += categoryTotal;

            html += `
                <div class="pl-category">
                    <h5>${formatCategoryName(categoryName)}</h5>
                    <table class="report-table">
                        <thead>
                            <tr>
                                <th>Account Number</th>
                                <th>Account Name</th>
                                <th>Amount</th>
                            </tr>
                        </thead>
                        <tbody>
            `;

            items.forEach(item => {
                html += `
                    <tr>
                        <td>${escapeHtml(item.accountNumber)}</td>
                        <td>${escapeHtml(item.accountName)}</td>
                        <td class="amount">${formatCurrency(item.amount)}</td>
                    </tr>
                `;
            });

            html += `
                        </tbody>
                        <tfoot>
                            <tr>
                                <td colspan="2"><strong>Total ${formatCategoryName(categoryName)}</strong></td>
                                <td class="amount"><strong>${formatCurrency(categoryTotal)}</strong></td>
                            </tr>
                        </tfoot>
                    </table>
                </div>
            `;
        });

        container.innerHTML = html;

        //Update total expenses display
        const totalElement = document.getElementById('pl-total-expenses');
        if (totalElement) {
            totalElement.textContent = formatCurrency(totalExpenses);
        }
    }

    //Render P&L metrics
    function renderPLMetrics(metrics) {
        const container = document.getElementById('pl-metrics');
        if (!container) return;

        container.innerHTML = `
            <div class="metrics-grid">
                <div class="metric-card">
                    <h6>Gross Profit Margin</h6>
                    <span class="metric-value ${metrics.grossProfitMargin >= 0 ? 'positive' : 'negative'}">
                        ${formatPercentage(metrics.grossProfitMargin)}
                    </span>
                </div>
                <div class="metric-card">
                    <h6>Operating Margin</h6>
                    <span class="metric-value ${metrics.operatingMargin >= 0 ? 'positive' : 'negative'}">
                        ${formatPercentage(metrics.operatingMargin)}
                    </span>
                </div>
                <div class="metric-card">
                    <h6>Net Profit Margin</h6>
                    <span class="metric-value ${metrics.netProfitMargin >= 0 ? 'positive' : 'negative'}">
                        ${formatPercentage(metrics.netProfitMargin)}
                    </span>
                </div>
                <div class="metric-card">
                    <h6>Expense Ratio</h6>
                    <span class="metric-value">${formatPercentage(metrics.expenseRatio)}</span>
                </div>
                ${metrics.revenueGrowth !== null ? `
                    <div class="metric-card">
                        <h6>Revenue Growth</h6>
                        <span class="metric-value ${metrics.revenueGrowth >= 0 ? 'positive' : 'negative'}">
                            ${formatPercentage(metrics.revenueGrowth)}
                        </span>
                    </div>
                ` : ''}
                ${metrics.profitGrowth !== null ? `
                    <div class="metric-card">
                        <h6>Profit Growth</h6>
                        <span class="metric-value ${metrics.profitGrowth >= 0 ? 'positive' : 'negative'}">
                            ${formatPercentage(metrics.profitGrowth)}
                        </span>
                    </div>
                ` : ''}
            </div>
        `;
    }

    //Update P&L summary section
    function updatePLSummary(data, metrics) {
        //Update summary revenue and expenses
        const summaryRevenue = document.getElementById('pl-summary-revenue');
        const summaryExpenses = document.getElementById('pl-summary-expenses');
        const netIncome = document.getElementById('pl-net-income');

        if (summaryRevenue) summaryRevenue.textContent = formatCurrency(data.totalRevenue);
        if (summaryExpenses) summaryExpenses.textContent = formatCurrency(data.totalExpenses);
        if (netIncome) {
            netIncome.textContent = formatCurrency(data.netIncome);
            netIncome.className = `summary-amount ${data.netIncome >= 0 ? 'positive' : 'negative'}`;
        }
    }

    //Render comparison analysis
    function renderComparisonAnalysis(currentData, comparisonData) {
        const comparisonSection = document.getElementById('pl-comparison-section');
        const comparisonTable = document.getElementById('pl-comparison-data');

        if (!comparisonSection || !comparisonTable) return;

        const items = [
            {
                label: 'Total Revenue',
                current: currentData.totalRevenue,
                comparison: comparisonData.totalRevenue
            },
            {
                label: 'Total Expenses',
                current: currentData.totalExpenses,
                comparison: comparisonData.totalExpenses
            },
            {
                label: 'Net Income',
                current: currentData.netIncome,
                comparison: comparisonData.netIncome
            }
        ];

        let html = '';
        items.forEach(item => {
            const variance = item.current - item.comparison;
            const percentChange = item.comparison !== 0 ? ((variance / Math.abs(item.comparison)) * 100) : 0;
            const varianceClass = variance >= 0 ? 'variance-positive' : 'variance-negative';

            html += `
                <tr>
                    <td>${item.label}</td>
                    <td class="amount">${formatCurrency(item.current)}</td>
                    <td class="amount">${formatCurrency(item.comparison)}</td>
                    <td class="amount ${varianceClass}">${formatCurrency(variance)}</td>
                    <td class="amount ${varianceClass}">${formatPercentage(percentChange)}</td>
                </tr>
            `;
        });

        comparisonTable.innerHTML = html;
        comparisonSection.style.display = 'block';
    }

    function renderBalanceSheet(data) {
        //Render assets
        const assetsTbody = document.querySelector('#bs-assets-table tbody');
        assetsTbody.innerHTML = '';
        data.assets.forEach(asset => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${asset.accountNumber}</td>
                <td>${asset.accountName}</td>
                <td class="amount">${formatCurrency(asset.amount)}</td>
            `;
            assetsTbody.appendChild(row);
        });

        //Render liabilities
        const liabilitiesTbody = document.querySelector('#bs-liabilities-table tbody');
        liabilitiesTbody.innerHTML = '';
        data.liabilities.forEach(liability => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${liability.accountNumber}</td>
                <td>${liability.accountName}</td>
                <td class="amount">${formatCurrency(liability.amount)}</td>
            `;
            liabilitiesTbody.appendChild(row);
        });

        //Render equity
        const equityTbody = document.querySelector('#bs-equity-table tbody');
        equityTbody.innerHTML = '';
        data.equity.forEach(equity => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${equity.accountNumber}</td>
                <td>${equity.accountName}</td>
                <td class="amount">${formatCurrency(equity.amount)}</td>
            `;
            equityTbody.appendChild(row);
        });

        //Update totals
        document.getElementById('bs-total-assets').textContent = formatCurrency(data.totalAssets);
        document.getElementById('bs-total-liabilities').textContent = formatCurrency(data.totalLiabilities);
        document.getElementById('bs-total-equity').textContent = formatCurrency(data.totalEquity);
    }

    function renderAccountLedger(data) {
        //Update account info
        const accountInfo = document.getElementById('account-info');
        accountInfo.innerHTML = `
            <h4>${data.accountNumber} - ${data.accountName}</h4>
            <p><strong>Account Type:</strong> ${data.accountType}</p>
            <p><strong>Period:</strong> ${formatDate(data.fromDate)} to ${formatDate(data.toDate)}</p>
            <p><strong>Opening Balance:</strong> ${formatCurrency(data.openingBalance)}</p>
            <p><strong>Closing Balance:</strong> ${formatCurrency(data.closingBalance)}</p>
        `;

        //Render transactions
        const tbody = document.querySelector('#al-table tbody');
        tbody.innerHTML = '';

        //Add opening balance row if there are transactions
        if (data.entries.length > 0) {
            const openingRow = document.createElement('tr');
            openingRow.style.backgroundColor = '#f8f9fa';
            openingRow.innerHTML = `
                <td colspan="3"><strong>Opening Balance</strong></td>
                <td class="amount"></td>
                <td class="amount"></td>
                <td class="amount"><strong>${formatCurrency(data.openingBalance)}</strong></td>
            `;
            tbody.appendChild(openingRow);
        }

        data.entries.forEach(entry => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${formatDate(entry.date)}</td>
                <td>${entry.voucherNumber}</td>
                <td>${entry.description}</td>
                <td class="amount">${formatCurrency(entry.debitAmount)}</td>
                <td class="amount">${formatCurrency(entry.creditAmount)}</td>
                <td class="amount">${formatCurrency(entry.balance)}</td>
            `;
            tbody.appendChild(row);
        });
    }

    //Export Functions
    function exportToCSV(reportType) {
        try {
            let csvContent = '';
            let filename = '';

            switch (reportType) {
                case 'general-ledger':
                    csvContent = generateGeneralLedgerCSV();
                    filename = 'general_ledger.csv';
                    break;
                case 'trial-balance':
                    csvContent = generateTrialBalanceCSV();
                    filename = 'trial_balance.csv';
                    break;
                case 'income-statement':
                    csvContent = generateIncomeStatementCSV();
                    filename = 'income_statement.csv';
                    break;
                case 'profit-loss':
                    csvContent = generateProfitLossCSV();
                    filename = 'profit_loss_statement.csv';
                    break;
                case 'balance-sheet':
                    csvContent = generateBalanceSheetCSV();
                    filename = 'balance_sheet.csv';
                    break;
                case 'account-ledger':
                    csvContent = generateAccountLedgerCSV();
                    filename = 'account_ledger.csv';
                    break;
            }

            downloadCSV(csvContent, filename);
        } catch (error) {
            console.error('Error exporting to CSV:', error);
            showError('Failed to export report');
        }
    }

    async function exportToPDF(reportType) {
        try {
            showLoading();
            hideError();

            let url = `/api/Reports/ExportPDF/${reportType}?`;
            const params = new URLSearchParams();

            switch (reportType) {
                case 'general-ledger':
                    const glFromDate = document.getElementById('gl-from-date').value;
                    const glToDate = document.getElementById('gl-to-date').value;
                    const glAccountId = document.getElementById('gl-account').value;

                    if (!glFromDate || !glToDate) {
                        throw new Error('Please select both from and to dates');
                    }

                    params.append('fromDate', glFromDate);
                    params.append('toDate', glToDate);
                    if (glAccountId) params.append('accountId', glAccountId);
                    break;

                case 'trial-balance':
                    const tbAsOfDate = document.getElementById('tb-as-of-date').value;
                    if (!tbAsOfDate) {
                        throw new Error('Please select an as-of date');
                    }
                    params.append('asOfDate', tbAsOfDate);
                    break;

                case 'income-statement':
                    const isFromDate = document.getElementById('is-from-date').value;
                    const isToDate = document.getElementById('is-to-date').value;

                    if (!isFromDate || !isToDate) {
                        throw new Error('Please select both from and to dates');
                    }

                    params.append('fromDate', isFromDate);
                    params.append('toDate', isToDate);
                    break;

                case 'profit-loss':
                    const plFromDate = document.getElementById('pl-from-date').value;
                    const plToDate = document.getElementById('pl-to-date').value;
                    const plComparison = document.getElementById('pl-comparison').value;

                    if (!plFromDate || !plToDate) {
                        throw new Error('Please select both from and to dates');
                    }

                    params.append('fromDate', plFromDate);
                    params.append('toDate', plToDate);
                    if (plComparison && plComparison !== 'none') {
                        params.append('comparison', plComparison);
                    }
                    break;

                case 'balance-sheet':
                    const bsAsOfDate = document.getElementById('bs-as-of-date').value;
                    if (!bsAsOfDate) {
                        throw new Error('Please select an as-of date');
                    }
                    params.append('asOfDate', bsAsOfDate);
                    break;

                case 'account-ledger':
                    const alAccountId = document.getElementById('al-account').value;
                    const alFromDate = document.getElementById('al-from-date').value;
                    const alToDate = document.getElementById('al-to-date').value;

                    if (!alAccountId) {
                        throw new Error('Please select an account');
                    }
                    if (!alFromDate || !alToDate) {
                        throw new Error('Please select both from and to dates');
                    }

                    params.append('accountId', alAccountId);
                    params.append('fromDate', alFromDate);
                    params.append('toDate', alToDate);
                    break;
            }

            url += params.toString();

            const response = await AuthManager.makeAuthenticatedRequest(url);
            if (!response || !response.ok) {
                throw new Error('Failed to generate PDF report');
            }

            //Get the PDF as a blob
            const blob = await response.blob();

            //Create download link
            const downloadUrl = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = downloadUrl;

            //Get filename from response headers or generate one
            const contentDisposition = response.headers.get('content-disposition');
            let filename = `${reportType.replace('-', '_')}_${new Date().toISOString().split('T')[0]}.pdf`;

            if (contentDisposition) {
                const filenameMatch = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
                if (filenameMatch && filenameMatch[1]) {
                    filename = filenameMatch[1].replace(/['"]/g, '');
                }
            }

            link.setAttribute('download', filename);
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);

            //Clean up
            window.URL.revokeObjectURL(downloadUrl);

        } catch (error) {
            console.error('Error exporting to PDF:', error);
            showError(error.message);
        } finally {
            hideLoading();
        }
    }

    function generateGeneralLedgerCSV() {
        const table = document.getElementById('gl-table');
        return tableToCSV(table);
    }

    function generateTrialBalanceCSV() {
        const table = document.getElementById('tb-table');
        return tableToCSV(table);
    }

    function generateIncomeStatementCSV() {
        let csv = 'Income Statement\n\n';
        csv += 'REVENUES\n';
        csv += 'Account Number,Account Name,Amount\n';

        const revenueTable = document.getElementById('is-revenue-table');
        const revenueRows = revenueTable.querySelectorAll('tbody tr');
        revenueRows.forEach(row => {
            const cells = row.querySelectorAll('td');
            csv += `${cells[0].textContent},${cells[1].textContent},${cells[2].textContent}\n`;
        });

        csv += '\nEXPENSES\n';
        csv += 'Account Number,Account Name,Amount\n';

        const expenseTable = document.getElementById('is-expense-table');
        const expenseRows = expenseTable.querySelectorAll('tbody tr');
        expenseRows.forEach(row => {
            const cells = row.querySelectorAll('td');
            csv += `${cells[0].textContent},${cells[1].textContent},${cells[2].textContent}\n`;
        });

        csv += '\nSUMMARY\n';
        csv += `Total Revenue,${document.getElementById('is-total-revenue').textContent}\n`;
        csv += `Total Expenses,${document.getElementById('is-total-expenses').textContent}\n`;
        csv += `Net Income,${document.getElementById('is-net-income').textContent}\n`;

        return csv;
    }

    //Export P&L to CSV with formatting
    function generateProfitLossCSV() {
        try {
            let csv = 'Profit & Loss Statement\n';
            csv += `Generated on: ${new Date().toLocaleDateString()}\n\n`;

            //Add date range
            const fromDate = document.getElementById('pl-from-date').value;
            const toDate = document.getElementById('pl-to-date').value;
            csv += `Period: ${fromDate} to ${toDate}\n\n`;

            //Revenue section
            csv += 'REVENUES\n';
            csv += 'Category,Account Number,Account Name,Amount\n';

            const revenueCategories = document.querySelectorAll('#pl-revenue-categories .pl-category');
            revenueCategories.forEach(category => {
                const categoryName = category.querySelector('h5').textContent;
                const rows = category.querySelectorAll('tbody tr');

                rows.forEach(row => {
                    const cells = row.querySelectorAll('td');
                    if (cells.length === 3) {
                        csv += `"${categoryName}","${cells[0].textContent}","${cells[1].textContent}","${cells[2].textContent}"\n`;
                    }
                });
            });

            csv += '\nEXPENSES\n';
            csv += 'Category,Account Number,Account Name,Amount\n';

            const expenseCategories = document.querySelectorAll('#pl-expense-categories .pl-category');
            expenseCategories.forEach(category => {
                const categoryName = category.querySelector('h5').textContent;
                const rows = category.querySelectorAll('tbody tr');

                rows.forEach(row => {
                    const cells = row.querySelectorAll('td');
                    if (cells.length === 3) {
                        csv += `"${categoryName}","${cells[0].textContent}","${cells[1].textContent}","${cells[2].textContent}"\n`;
                    }
                });
            });

            //Summary
            csv += '\nSUMMARY\n';
            const totalRevenue = document.getElementById('pl-total-revenue')?.textContent || '0';
            const totalExpenses = document.getElementById('pl-total-expenses')?.textContent || '0';
            const netIncome = document.getElementById('pl-net-income')?.textContent || '0';

            csv += `Total Revenue,${totalRevenue}\n`;
            csv += `Total Expenses,${totalExpenses}\n`;
            csv += `Net Income,${netIncome}\n`;

            return csv;
        } catch (error) {
            console.error('Error generating P&L CSV:', error);
            throw new Error('Failed to generate CSV export');
        }
    }

    function generateBalanceSheetCSV() {
        let csv = 'Balance Sheet\n\n';
        csv += 'ASSETS\n';
        csv += 'Account Number,Account Name,Amount\n';

        const assetsTable = document.getElementById('bs-assets-table');
        const assetRows = assetsTable.querySelectorAll('tbody tr');
        assetRows.forEach(row => {
            const cells = row.querySelectorAll('td');
            csv += `${cells[0].textContent},${cells[1].textContent},${cells[2].textContent}\n`;
        });

        csv += '\nLIABILITIES\n';
        csv += 'Account Number,Account Name,Amount\n';

        const liabilitiesTable = document.getElementById('bs-liabilities-table');
        const liabilityRows = liabilitiesTable.querySelectorAll('tbody tr');
        liabilityRows.forEach(row => {
            const cells = row.querySelectorAll('td');
            csv += `${cells[0].textContent},${cells[1].textContent},${cells[2].textContent}\n`;
        });

        csv += '\nEQUITY\n';
        csv += 'Account Number,Account Name,Amount\n';

        const equityTable = document.getElementById('bs-equity-table');
        const equityRows = equityTable.querySelectorAll('tbody tr');
        equityRows.forEach(row => {
            const cells = row.querySelectorAll('td');
            csv += `${cells[0].textContent},${cells[1].textContent},${cells[2].textContent}\n`;
        });

        csv += '\nTOTALS\n';
        csv += `Total Assets,${document.getElementById('bs-total-assets').textContent}\n`;
        csv += `Total Liabilities,${document.getElementById('bs-total-liabilities').textContent}\n`;
        csv += `Total Equity,${document.getElementById('bs-total-equity').textContent}\n`;

        return csv;
    }

    function generateAccountLedgerCSV() {
        const table = document.getElementById('al-table');
        return tableToCSV(table);
    }

    function tableToCSV(table) {
        let csv = '';

        //Headers
        const headers = table.querySelectorAll('thead th');
        const headerRow = Array.from(headers).map(h => h.textContent.trim()).join(',');
        csv += headerRow + '\n';

        //Body rows
        const rows = table.querySelectorAll('tbody tr');
        rows.forEach(row => {
            const cells = row.querySelectorAll('td');
            const rowData = Array.from(cells).map(cell => {
                let text = cell.textContent.trim();
                //Escape commas and quotes
                if (text.includes(',') || text.includes('"')) {
                    text = '"' + text.replace(/"/g, '""') + '"';
                }
                return text;
            }).join(',');
            csv += rowData + '\n';
        });

        return csv;
    }

    function downloadCSV(csvContent, filename) {
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');

        if (link.download !== undefined) {
            const url = URL.createObjectURL(blob);
            link.setAttribute('href', url);
            link.setAttribute('download', filename);
            link.style.visibility = 'hidden';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        }
    }

    //Print Functions
    function printReport(reportType) {
        //Hide non-printable elements
        document.querySelectorAll('.tab-btn, .generate-btn, .export-btn, .print-btn').forEach(el => {
            el.style.display = 'none';
        });

        //Show only the current report
        reportSections.forEach(section => {
            if (!section.classList.contains('active')) {
                section.style.display = 'none';
            }
        });

        //Print
        window.print();

        //Restore elements after printing
        setTimeout(() => {
            document.querySelectorAll('.tab-btn, .generate-btn, .export-btn, .print-btn').forEach(el => {
                el.style.display = '';
            });

            reportSections.forEach(section => {
                section.style.display = '';
            });
        }, 1000);
    }

    //Utility Functions
    function formatDate(dateString) {
        const date = new Date(dateString);
        return date.toLocaleDateString();
    }

    function formatCurrency(amount) {
        if (amount === 0) return '';
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(Math.abs(amount));
    }

    function formatPercentage(value) {
        if (value === null || value === undefined || isNaN(value)) return 'N/A';
        return `${value.toFixed(2)}%`;
    }

    function formatCategoryName(categoryName) {
        return categoryName
            .replace(/([A-Z])/g, ' $1')
            .replace(/^./, str => str.toUpperCase())
            .trim();
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function showLoading() {
        loadingIndicator.style.display = 'flex';
    }

    function hideLoading() {
        loadingIndicator.style.display = 'none';
    }

    function showError(message) {
        errorContainer.textContent = message;
        errorContainer.style.display = 'block';

        //Auto-hide after 5 seconds
        setTimeout(() => {
            hideError();
        }, 5000);
    }

    function hideError() {
        errorContainer.style.display = 'none';
    }
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

        console.log('User authenticated, initializing Reports...');
        initializeReports();
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