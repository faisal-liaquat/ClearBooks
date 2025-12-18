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
function initializeSummaryReports() {
    console.log('Initializing Summary Reports module...');

    //State Management
    let charts = {};
    let dashboardData = {};

    //DOM Elements
    const loadingIndicator = document.getElementById('loading-indicator');
    const errorContainer = document.getElementById('error-container');
    const dashboardContent = document.getElementById('dashboard-content');
    const refreshAllBtn = document.getElementById('refresh-all-btn');
    const exportDashboardBtn = document.getElementById('export-dashboard-btn');
    const retryBtn = document.getElementById('retry-btn');

    //Period selectors
    const trendPeriodSelect = document.getElementById('trend-period');
    const cashflowPeriodSelect = document.getElementById('cashflow-period');
    const topAccountsTypeSelect = document.getElementById('top-accounts-type');
    const volumeMetricSelect = document.getElementById('volume-metric');

    //Chart.js default configuration
    Chart.defaults.font.family = "'Segoe UI', Arial, sans-serif";
    Chart.defaults.font.size = 12;
    Chart.defaults.color = '#2c3e50';

    //Initialize the module
    initialize();

    async function initialize() {
        try {
            setupEventListeners();
            await loadDashboardData();
            console.log('Summary Reports module initialized successfully');
        } catch (error) {
            console.error('Error initializing summary reports:', error);
            showError('Failed to initialize summary reports module');
        }
    }

    function setupEventListeners() {
        //Main action buttons
        refreshAllBtn.addEventListener('click', handleRefreshAll);
        exportDashboardBtn.addEventListener('click', handleExportDashboard);
        retryBtn.addEventListener('click', handleRetry);

        //Period change listeners
        trendPeriodSelect.addEventListener('change', () => updateMonthlyTrends());
        cashflowPeriodSelect.addEventListener('change', () => updateCashFlowAnalysis());
        topAccountsTypeSelect.addEventListener('change', () => updateTopAccounts());
        volumeMetricSelect.addEventListener('change', () => updateTransactionVolume());

        //Export chart buttons
        const exportChartBtns = document.querySelectorAll('.export-chart-btn');
        exportChartBtns.forEach(btn => {
            btn.addEventListener('click', (e) => {
                const chartType = e.target.getAttribute('data-chart');
                exportChart(chartType);
            });
        });
    }

    //Data Loading Functions
    async function loadDashboardData() {
        try {
            showLoading();
            hideError();

            //Load all required data in parallel
            const [
                balanceSheetData,
                incomeStatementData,
                trialBalanceData,
                accountsData,
                vouchersData
            ] = await Promise.all([
                fetchBalanceSheetData(),
                fetchIncomeStatementData(),
                fetchTrialBalanceData(),
                fetchAccountsData(),
                fetchVouchersData()
            ]);

            //Store data for chart generation
            dashboardData = {
                balanceSheet: balanceSheetData,
                incomeStatement: incomeStatementData,
                trialBalance: trialBalanceData,
                accounts: accountsData,
                vouchers: vouchersData
            };

            //Update overview cards
            updateOverviewCards();

            //Generate all charts
            await generateAllCharts();

            //Update summary table
            updateSummaryTable();

            showDashboard();

        } catch (error) {
            console.error('Error loading dashboard data:', error);
            showError('Failed to load dashboard data. Please try again.');
        }
    }

    async function fetchBalanceSheetData() {
        const today = new Date().toISOString().split('T')[0];
        const response = await AuthManager.makeAuthenticatedRequest(
            `/api/Reports/BalanceSheet?asOfDate=${today}`
        );
        if (!response || !response.ok) {
            throw new Error('Failed to fetch balance sheet data');
        }
        return await response.json();
    }

    async function fetchIncomeStatementData() {
        const today = new Date();
        const startOfYear = new Date(today.getFullYear(), 0, 1).toISOString().split('T')[0];
        const todayStr = today.toISOString().split('T')[0];

        const response = await AuthManager.makeAuthenticatedRequest(
            `/api/Reports/IncomeStatement?fromDate=${startOfYear}&toDate=${todayStr}`
        );
        if (!response || !response.ok) {
            throw new Error('Failed to fetch income statement data');
        }
        return await response.json();
    }

    async function fetchTrialBalanceData() {
        const today = new Date().toISOString().split('T')[0];
        const response = await AuthManager.makeAuthenticatedRequest(
            `/api/Reports/TrialBalance?asOfDate=${today}`
        );
        if (!response || !response.ok) {
            throw new Error('Failed to fetch trial balance data');
        }
        return await response.json();
    }

    async function fetchAccountsData() {
        const response = await AuthManager.makeAuthenticatedRequest('/api/ChartOfAccounts');
        if (!response || !response.ok) {
            throw new Error('Failed to fetch accounts data');
        }
        return await response.json();
    }

    async function fetchVouchersData() {
        const response = await AuthManager.makeAuthenticatedRequest('/api/Vouchers');
        if (!response || !response.ok) {
            throw new Error('Failed to fetch vouchers data');
        }
        return await response.json();
    }

    //Overview Cards Update
    function updateOverviewCards() {
        const { balanceSheet, incomeStatement } = dashboardData;

        document.getElementById('total-assets').textContent = formatCurrency(balanceSheet.totalAssets);
        document.getElementById('total-liabilities').textContent = formatCurrency(balanceSheet.totalLiabilities);
        document.getElementById('total-equity').textContent = formatCurrency(balanceSheet.totalEquity);
        document.getElementById('net-income').textContent = formatCurrency(incomeStatement.netIncome);

        //Add animation effect
        animateNumbers(['total-assets', 'total-liabilities', 'total-equity', 'net-income']);
    }

    function animateNumbers(elementIds) {
        elementIds.forEach(id => {
            const element = document.getElementById(id);
            element.style.transform = 'scale(1.1)';
            element.style.transition = 'transform 0.3s ease';
            setTimeout(() => {
                element.style.transform = 'scale(1)';
            }, 300);
        });
    }

    //Chart Generation Functions
    async function generateAllCharts() {
        await Promise.all([
            generateAccountDistributionChart(),
            generateMonthlyTrendsChart(),
            generateCashFlowChart(),
            generateTopAccountsChart(),
            generateTransactionVolumeChart(),
            generateBalanceSheetChart()
        ]);
    }

    function generateAccountDistributionChart() {
        const ctx = document.getElementById('account-distribution-chart').getContext('2d');

        //Destroy existing chart if it exists
        if (charts.accountDistribution) {
            charts.accountDistribution.destroy();
        }

        //Calculate account type totals from trial balance
        const accountTypeTotals = {};
        dashboardData.trialBalance.entries.forEach(entry => {
            const type = entry.accountType;
            const amount = entry.debitBalance + entry.creditBalance;
            accountTypeTotals[type] = (accountTypeTotals[type] || 0) + amount;
        });

        const labels = Object.keys(accountTypeTotals);
        const data = Object.values(accountTypeTotals);
        const colors = [
            '#1a237e', '#0d47a1', '#1565c0', '#1976d2', '#1e88e5'
        ];

        charts.accountDistribution = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: data,
                    backgroundColor: colors.slice(0, labels.length),
                    borderWidth: 2,
                    borderColor: '#ffffff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 20,
                            usePointStyle: true
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const label = context.label || '';
                                const value = formatCurrency(context.parsed);
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = ((context.parsed / total) * 100).toFixed(1);
                                return `${label}: ${value} (${percentage}%)`;
                            }
                        }
                    }
                }
            }
        });
    }

    async function generateMonthlyTrendsChart() {
        const ctx = document.getElementById('monthly-trends-chart').getContext('2d');

        if (charts.monthlyTrends) {
            charts.monthlyTrends.destroy();
        }

        const months = parseInt(trendPeriodSelect.value);
        const monthlyData = await generateMonthlyData(months);

        charts.monthlyTrends = new Chart(ctx, {
            type: 'line',
            data: {
                labels: monthlyData.labels,
                datasets: [
                    {
                        label: 'Revenue',
                        data: monthlyData.revenue,
                        borderColor: '#28a745',
                        backgroundColor: 'rgba(40, 167, 69, 0.1)',
                        borderWidth: 3,
                        fill: true,
                        tension: 0.4
                    },
                    {
                        label: 'Expenses',
                        data: monthlyData.expenses,
                        borderColor: '#dc3545',
                        backgroundColor: 'rgba(220, 53, 69, 0.1)',
                        borderWidth: 3,
                        fill: true,
                        tension: 0.4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    intersect: false,
                },
                plugins: {
                    legend: {
                        position: 'top',
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return `${context.dataset.label}: ${formatCurrency(context.parsed.y)}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Month'
                        }
                    },
                    y: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Amount'
                        },
                        ticks: {
                            callback: function (value) {
                                return formatCurrencyShort(value);
                            }
                        }
                    }
                }
            }
        });
    }

    async function generateCashFlowChart() {
        const ctx = document.getElementById('cash-flow-chart').getContext('2d');

        if (charts.cashFlow) {
            charts.cashFlow.destroy();
        }

        const months = parseInt(cashflowPeriodSelect.value);
        const cashFlowData = await generateCashFlowData(months);

        charts.cashFlow = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: cashFlowData.labels,
                datasets: [
                    {
                        label: 'Cash Inflow',
                        data: cashFlowData.inflow,
                        backgroundColor: 'rgba(40, 167, 69, 0.8)',
                        borderColor: '#28a745',
                        borderWidth: 1
                    },
                    {
                        label: 'Cash Outflow',
                        data: cashFlowData.outflow,
                        backgroundColor: 'rgba(220, 53, 69, 0.8)',
                        borderColor: '#dc3545',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top',
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return `${context.dataset.label}: ${formatCurrency(Math.abs(context.parsed.y))}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Month'
                        }
                    },
                    y: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Amount'
                        },
                        ticks: {
                            callback: function (value) {
                                return formatCurrencyShort(value);
                            }
                        }
                    }
                }
            }
        });
    }

    async function generateTopAccountsChart() {
        const ctx = document.getElementById('top-accounts-chart').getContext('2d');

        if (charts.topAccounts) {
            charts.topAccounts.destroy();
        }

        const accountType = topAccountsTypeSelect.value;
        const topAccountsData = generateTopAccountsData(accountType);

        charts.topAccounts = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: topAccountsData.labels,
                datasets: [{
                    label: `Top ${accountType} Accounts`,
                    data: topAccountsData.amounts,
                    backgroundColor: 'rgba(26, 35, 126, 0.8)',
                    borderColor: '#1a237e',
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                indexAxis: 'y',
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return `${context.label}: ${formatCurrency(context.parsed.x)}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Amount'
                        },
                        ticks: {
                            callback: function (value) {
                                return formatCurrencyShort(value);
                            }
                        }
                    },
                    y: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Account'
                        }
                    }
                }
            }
        });
    }

    async function generateTransactionVolumeChart() {
        const ctx = document.getElementById('transaction-volume-chart').getContext('2d');

        if (charts.transactionVolume) {
            charts.transactionVolume.destroy();
        }

        const metric = volumeMetricSelect.value;
        const volumeData = generateTransactionVolumeData(metric);

        charts.transactionVolume = new Chart(ctx, {
            type: 'line',
            data: {
                labels: volumeData.labels,
                datasets: [{
                    label: metric === 'count' ? 'Transaction Count' : 'Transaction Amount',
                    data: volumeData.values,
                    borderColor: '#17a2b8',
                    backgroundColor: 'rgba(23, 162, 184, 0.1)',
                    borderWidth: 3,
                    fill: true,
                    tension: 0.4
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'top',
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                if (metric === 'count') {
                                    return `${context.dataset.label}: ${context.parsed.y}`;
                                } else {
                                    return `${context.dataset.label}: ${formatCurrency(context.parsed.y)}`;
                                }
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Month'
                        }
                    },
                    y: {
                        display: true,
                        title: {
                            display: true,
                            text: metric === 'count' ? 'Count' : 'Amount'
                        },
                        ticks: {
                            callback: function (value) {
                                if (metric === 'count') {
                                    return value;
                                } else {
                                    return formatCurrencyShort(value);
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    function generateBalanceSheetChart() {
        const ctx = document.getElementById('balance-sheet-chart').getContext('2d');

        if (charts.balanceSheet) {
            charts.balanceSheet.destroy();
        }

        const { balanceSheet } = dashboardData;

        charts.balanceSheet = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: ['Assets', 'Liabilities', 'Equity'],
                datasets: [{
                    label: 'Balance Sheet Summary',
                    data: [
                        balanceSheet.totalAssets,
                        balanceSheet.totalLiabilities,
                        balanceSheet.totalEquity
                    ],
                    backgroundColor: [
                        'rgba(26, 35, 126, 0.8)',
                        'rgba(220, 53, 69, 0.8)',
                        'rgba(40, 167, 69, 0.8)'
                    ],
                    borderColor: [
                        '#1a237e',
                        '#dc3545',
                        '#28a745'
                    ],
                    borderWidth: 1
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                return `${context.label}: ${formatCurrency(context.parsed.y)}`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Category'
                        }
                    },
                    y: {
                        display: true,
                        title: {
                            display: true,
                            text: 'Amount'
                        },
                        ticks: {
                            callback: function (value) {
                                return formatCurrencyShort(value);
                            }
                        }
                    }
                }
            }
        });
    }

    //Data Generation Helper Functions
    async function generateMonthlyData(months) {
        const labels = [];
        const revenue = [];
        const expenses = [];

        const currentDate = new Date();

        for (let i = months - 1; i >= 0; i--) {
            const date = new Date(currentDate.getFullYear(), currentDate.getMonth() - i, 1);
            const monthStr = date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
            labels.push(monthStr);

            // monthly data based on existing income statement data
           
            const monthlyRevenue = (dashboardData.incomeStatement.totalRevenue / 12) * (0.8 + Math.random() * 0.4);
            const monthlyExpenses = (dashboardData.incomeStatement.totalExpenses / 12) * (0.8 + Math.random() * 0.4);

            revenue.push(monthlyRevenue);
            expenses.push(monthlyExpenses);
        }

        return { labels, revenue, expenses };
    }

    async function generateCashFlowData(months) {
        const labels = [];
        const inflow = [];
        const outflow = [];

        const currentDate = new Date();

        for (let i = months - 1; i >= 0; i--) {
            const date = new Date(currentDate.getFullYear(), currentDate.getMonth() - i, 1);
            const monthStr = date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
            labels.push(monthStr);

            //Simulate cash flow data
            const monthlyInflow = (dashboardData.incomeStatement.totalRevenue / 12) * (0.9 + Math.random() * 0.2);
            const monthlyOutflow = (dashboardData.incomeStatement.totalExpenses / 12) * (0.9 + Math.random() * 0.2);

            inflow.push(monthlyInflow);
            outflow.push(-monthlyOutflow); //Negative for outflow
        }

        return { labels, inflow, outflow };
    }

    function generateTopAccountsData(accountType) {
        const filteredEntries = dashboardData.trialBalance.entries
            .filter(entry => entry.accountType.toLowerCase() === accountType.toLowerCase())
            .map(entry => ({
                name: entry.accountName,
                amount: entry.debitBalance + entry.creditBalance
            }))
            .sort((a, b) => b.amount - a.amount)
            .slice(0, 10); //Top 10 accounts

        return {
            labels: filteredEntries.map(entry => entry.name),
            amounts: filteredEntries.map(entry => entry.amount)
        };
    }

    function generateTransactionVolumeData(metric) {
        const labels = [];
        const values = [];

        const currentDate = new Date();

        for (let i = 11; i >= 0; i--) {
            const date = new Date(currentDate.getFullYear(), currentDate.getMonth() - i, 1);
            const monthStr = date.toLocaleDateString('en-US', { month: 'short', year: 'numeric' });
            labels.push(monthStr);

            if (metric === 'count') {
                //Simulate transaction count
                const count = Math.floor(20 + Math.random() * 80);
                values.push(count);
            } else {
                //Simulate transaction amount
                const amount = (dashboardData.incomeStatement.totalRevenue / 12) * (0.8 + Math.random() * 0.4);
                values.push(amount);
            }
        }

        return { labels, values };
    }

    //Summary Table Update
    function updateSummaryTable() {
        const tbody = document.querySelector('#financial-summary-table tbody');
        tbody.innerHTML = '';

        const summaryData = [
            {
                metric: 'Total Revenue',
                current: dashboardData.incomeStatement.totalRevenue,
                previous: dashboardData.incomeStatement.totalRevenue * 0.9, //Simulated previous period
            },
            {
                metric: 'Total Expenses',
                current: dashboardData.incomeStatement.totalExpenses,
                previous: dashboardData.incomeStatement.totalExpenses * 1.1, //Simulated previous period
            },
            {
                metric: 'Net Income',
                current: dashboardData.incomeStatement.netIncome,
                previous: dashboardData.incomeStatement.netIncome * 0.8, //Simulated previous period
            },
            {
                metric: 'Total Assets',
                current: dashboardData.balanceSheet.totalAssets,
                previous: dashboardData.balanceSheet.totalAssets * 0.95, //Simulated previous period
            }
        ];

        summaryData.forEach(item => {
            const change = item.current - item.previous;
            const changePercent = item.previous !== 0 ? ((change / item.previous) * 100) : 0;
            const changeClass = change > 0 ? 'positive-change' : change < 0 ? 'negative-change' : 'neutral-change';
            const changeSymbol = change > 0 ? '+' : '';

            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${item.metric}</td>
                <td>${formatCurrency(item.current)}</td>
                <td>${formatCurrency(item.previous)}</td>
                <td class="${changeClass}">${changeSymbol}${formatCurrency(change)}</td>
                <td class="${changeClass}">${changeSymbol}${changePercent.toFixed(1)}%</td>
            `;
            tbody.appendChild(row);
        });
    }

    //Chart Update Functions
    function updateMonthlyTrends() {
        generateMonthlyTrendsChart();
    }

    function updateCashFlowAnalysis() {
        generateCashFlowChart();
    }

    function updateTopAccounts() {
        generateTopAccountsChart();
    }

    function updateTransactionVolume() {
        generateTransactionVolumeChart();
    }

    //Event Handlers
    async function handleRefreshAll() {
        try {
            refreshAllBtn.textContent = 'Refreshing...';
            refreshAllBtn.disabled = true;

            await loadDashboardData();

            //Show success feedback
            refreshAllBtn.textContent = 'Refreshed!';
            setTimeout(() => {
                refreshAllBtn.textContent = 'Refresh All';
                refreshAllBtn.disabled = false;
            }, 2000);
        } catch (error) {
            console.error('Error refreshing dashboard:', error);
            refreshAllBtn.textContent = 'Refresh All';
            refreshAllBtn.disabled = false;
            showError('Failed to refresh dashboard data');
        }
    }

    async function handleExportDashboard() {
        try {
            exportDashboardBtn.textContent = 'Exporting...';
            exportDashboardBtn.disabled = true;

            //Generate a comprehensive PDF report
            await exportDashboardToPDF();

            exportDashboardBtn.textContent = 'Exported!';
            setTimeout(() => {
                exportDashboardBtn.textContent = 'Export Dashboard';
                exportDashboardBtn.disabled = false;
            }, 2000);
        } catch (error) {
            console.error('Error exporting dashboard:', error);
            exportDashboardBtn.textContent = 'Export Dashboard';
            exportDashboardBtn.disabled = false;
            showError('Failed to export dashboard');
        }
    }

    function handleRetry() {
        loadDashboardData();
    }

    //Export Functions
    async function exportChart(chartType) {
        try {
            const chart = charts[chartType.replace('-', '').replace('_', '')];
            if (!chart) {
                showError('Chart not found');
                return;
            }

            //Convert chart to image
            const canvas = chart.canvas;
            const imageData = canvas.toDataURL('image/png');

            //Create download link
            const link = document.createElement('a');
            link.href = imageData;
            link.download = `${chartType}-chart.png`;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);

        } catch (error) {
            console.error('Error exporting chart:', error);
            showError('Failed to export chart');
        }
    }

    async function exportDashboardToPDF() {
        try {
            //Use the existing PDF export from reports API
            const today = new Date().toISOString().split('T')[0];
            const startOfYear = new Date(new Date().getFullYear(), 0, 1).toISOString().split('T')[0];

            //Export multiple reports as a comprehensive package
            const reportTypes = [
                { type: 'balance-sheet', params: `asOfDate=${today}` },
                { type: 'income-statement', params: `fromDate=${startOfYear}&toDate=${today}` },
                { type: 'trial-balance', params: `asOfDate=${today}` }
            ];

            for (const report of reportTypes) {
                const response = await AuthManager.makeAuthenticatedRequest(
                    `/api/Reports/ExportPDF/${report.type}?${report.params}`
                );

                if (response && response.ok) {
                    const blob = await response.blob();
                    const url = window.URL.createObjectURL(blob);
                    const link = document.createElement('a');
                    link.href = url;
                    link.download = `Summary-${report.type}-${today}.pdf`;
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    window.URL.revokeObjectURL(url);
                }
            }

        } catch (error) {
            console.error('Error exporting dashboard to PDF:', error);
            throw error;
        }
    }

    //UI State Management
    function showLoading() {
        loadingIndicator.style.display = 'flex';
        errorContainer.style.display = 'none';
        dashboardContent.style.display = 'none';
    }

    function showError(message) {
        document.getElementById('error-message').textContent = message;
        loadingIndicator.style.display = 'none';
        errorContainer.style.display = 'block';
        dashboardContent.style.display = 'none';
    }

    function hideError() {
        errorContainer.style.display = 'none';
    }

    function showDashboard() {
        loadingIndicator.style.display = 'none';
        errorContainer.style.display = 'none';
        dashboardContent.style.display = 'block';
    }

    //Utility Functions
    function formatCurrency(amount) {
        if (amount === null || amount === undefined) return '$0.00';
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: 'USD',
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(amount);
    }

    function formatCurrencyShort(amount) {
        if (amount === null || amount === undefined) return '$0';

        const absAmount = Math.abs(amount);
        if (absAmount >= 1000000) {
            return `${(amount / 1000000).toFixed(1)}M`;
        } else if (absAmount >= 1000) {
            return `${(amount / 1000).toFixed(1)}K`;
        } else {
            return `${amount.toFixed(0)}`;
        }
    }

    console.log('Summary Reports module initialized successfully');
}

//heck authentication and initialize
async function checkAuthAndInit() {
    try {
        console.log('Checking authentication...');
        const isAuthenticated = await AuthManager.isAuthenticated();
        if (!isAuthenticated) {
            console.log('User not authenticated, redirecting to login...');
            window.location.href = '/login.html';
            return;
        }

        console.log('User authenticated, initializing Summary Reports...');
        initializeSummaryReports();
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