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
function initializeSearchView() {
    console.log('Initializing Search View module...');

    // \Global variables
    let currentPage = 1;
    let totalPages = 1;
    let pageSize = 10;
    let currentEntityType = '';
    let currentFilterType = '';
    let currentFilterValue = '';
    let currentData = [];

    //DOM Elements
    const entityTypeSelect = document.getElementById('entity-type');
    const filterTypeContainer = document.getElementById('filter-type-container');
    const filterTypeSelect = document.getElementById('filter-type');
    const filterValueContainer = document.getElementById('filter-value-container');
    const filterValueSelect = document.getElementById('filter-value');
    const searchForm = document.getElementById('search-form');
    const resetBtn = document.getElementById('reset-btn');
    const loadingIndicator = document.getElementById('loading-indicator');
    const errorContainer = document.getElementById('error-container');
    const prevPageBtn = document.getElementById('prev-page');
    const nextPageBtn = document.getElementById('next-page');
    const pageInfo = document.getElementById('page-info');
    const pageSizeSelect = document.getElementById('page-size');

    //Tables
    const tables = {
        chartofaccount: document.getElementById('coa-table'),
        glmapping: document.getElementById('glmapping-table'),
        voucher: document.getElementById('voucher-table'),
        payment: document.getElementById('payment-table'),
        receipt: document.getElementById('receipt-table')
    };

    //Filter options mapping
    const filterOptions = {
        chartofaccount: [
            { value: 'accountNumber', label: 'Account Number' },
            { value: 'accountType', label: 'Account Type' }
        ],
        glmapping: [
            { value: 'mappingId', label: 'Mapping ID' },
            { value: 'transactionType', label: 'Transaction Type' }
        ],
        voucher: [
            { value: 'voucherNumber', label: 'Voucher Number' },
            { value: 'transactionType', label: 'Transaction Type' },
            { value: 'status', label: 'Status' }
        ],
        payment: [
            { value: 'payeeName', label: 'Payee Name' },
            { value: 'paymentMethod', label: 'Payment Method' },
            { value: 'status', label: 'Status' }
        ],
        receipt: [
            { value: 'payerName', label: 'Payer Name' },
            { value: 'receiptNumber', label: 'Receipt Number' },
            { value: 'paymentMethod', label: 'Payment Method' }
        ]
    };

    //Initialize UI components
    pageSizeSelect.value = pageSize;

    //Event listeners
    entityTypeSelect.addEventListener('change', handleEntityTypeChange);
    filterTypeSelect.addEventListener('change', handleFilterTypeChange);
    filterValueSelect.addEventListener('change', handleFilterValueChange);
    searchForm.addEventListener('submit', handleSearch);
    resetBtn.addEventListener('click', resetFilters);
    prevPageBtn.addEventListener('click', () => navigatePage(-1));
    nextPageBtn.addEventListener('click', () => navigatePage(1));
    pageSizeSelect.addEventListener('change', handlePageSizeChange);

    //Handler functions
    function handleEntityTypeChange() {
        currentEntityType = entityTypeSelect.value;

        //Reset other filters and pagination
        resetFilters(false);

        if (!currentEntityType) {
            filterTypeContainer.style.display = 'none';
            filterValueContainer.style.display = 'none';
            hideAllTables();
            return;
        }

        //Populate filter type options based on entity
        populateFilterTypes(currentEntityType);
        filterTypeContainer.style.display = 'block';

        //Hide tables
        hideAllTables();
    }

    function populateFilterTypes(entityType) {
        //Clear existing options
        filterTypeSelect.innerHTML = '<option value="">Select Filter</option>';

        //Add new options
        if (filterOptions[entityType]) {
            filterOptions[entityType].forEach(option => {
                const optElement = document.createElement('option');
                optElement.value = option.value;
                optElement.textContent = option.label;
                filterTypeSelect.appendChild(optElement);
            });
        }
    }

    function handleFilterTypeChange() {
        currentFilterType = filterTypeSelect.value;

        if (!currentFilterType) {
            filterValueContainer.style.display = 'none';
            return;
        }

        //Fetch filter values based on entity type and filter type
        fetchFilterValues(currentEntityType, currentFilterType);
    }

    async function fetchFilterValues(entityType, filterType) {
        showLoading();

        try {
            //Special cases for static options
            if (entityType === 'voucher' && filterType === 'status') {
                populateStaticFilterValues(['Pending', 'Paid', 'Void', 'Partially Paid']);
                hideLoading();
                return;
            } else if ((entityType === 'payment' && filterType === 'status')) {
                populateStaticFilterValues(['Draft', 'Paid', 'Void']);
                hideLoading();
                return;
            } else if (filterType === 'paymentMethod') {
                populateStaticFilterValues(['Cash', 'Bank Transfer', 'Check', 'Credit Card', 'PayPal', 'Other']);
                hideLoading();
                return;
            }

            //Dynamic options from API
            const response = await AuthManager.makeAuthenticatedRequest(`/api/Search/GetFilterValues?entityType=${entityType}&filterType=${filterType}`);

            if (!response || !response.ok) {
                throw new Error(`Error fetching filter values: ${response.statusText}`);
            }

            const values = await response.json();
            populateFilterValueOptions(values);
        } catch (error) {
            showError(`Failed to load filter values: ${error.message}`);
        } finally {
            hideLoading();
        }
    }

    function populateStaticFilterValues(values) {
        filterValueSelect.innerHTML = '<option value="">Select Value</option>';

        values.forEach(value => {
            const optElement = document.createElement('option');
            optElement.value = value;
            optElement.textContent = value;
            filterValueSelect.appendChild(optElement);
        });

        filterValueContainer.style.display = 'block';
    }

    function populateFilterValueOptions(values) {
        filterValueSelect.innerHTML = '<option value="">Select Value</option>';

        if (values && values.length > 0) {
            values.forEach(value => {
                const optElement = document.createElement('option');
                optElement.value = value;
                optElement.textContent = value;
                filterValueSelect.appendChild(optElement);
            });
        }

        filterValueContainer.style.display = 'block';
    }

    function handleFilterValueChange() {
        currentFilterValue = filterValueSelect.value;
    }

    async function handleSearch(event) {
        event.preventDefault();

        if (!currentEntityType || !currentFilterType || !currentFilterValue) {
            showError('Please select all filter options');
            return;
        }

        currentPage = 1;
        await fetchSearchResults();
    }

    async function fetchSearchResults() {
        showLoading();
        hideError();

        try {
            const url = `/api/Search/SearchEntities?entityType=${currentEntityType}&filterType=${currentFilterType}&filterValue=${encodeURIComponent(currentFilterValue)}&page=${currentPage}&pageSize=${pageSize}`;

            const response = await AuthManager.makeAuthenticatedRequest(url);

            if (!response || !response.ok) {
                throw new Error(`Error fetching results: ${response.statusText}`);
            }

            const data = await response.json();

            //Update pagination info
            currentData = data.items || [];
            totalPages = data.totalPages || 1;

            //Update UI
            updatePagination();
            renderResults(currentData);
        } catch (error) {
            showError(`Failed to load search results: ${error.message}`);
            hideAllTables();
        } finally {
            hideLoading();
        }
    }

    function renderResults(data) {
        hideAllTables();

        if (!data || data.length === 0) {
            showError('No results found for the selected criteria');
            return;
        }

        //Get the appropriate table
        const table = tables[currentEntityType];
        if (!table) return;

        //Clear existing rows
        const tbody = table.querySelector('tbody');
        tbody.innerHTML = '';

        //Create rows based on entity type
        switch (currentEntityType) {
            case 'chartofaccount':
                renderChartOfAccounts(tbody, data);
                break;
            case 'glmapping':
                renderGLMappings(tbody, data);
                break;
            case 'voucher':
                renderVouchers(tbody, data);
                break;
            case 'payment':
                renderPayments(tbody, data);
                break;
            case 'receipt':
                renderReceipts(tbody, data);
                break;
        }

        //Show the table
        table.style.display = 'table';
    }

    function renderChartOfAccounts(tbody, accounts) {
        accounts.forEach(account => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${account.accountId}</td>
                <td>${account.accountNumber}</td>
                <td>${account.accountName}</td>
                <td>${account.accountType}</td>
                <td>${account.subaccount || ''}</td>
                <td>${account.description || ''}</td>
                <td>
                    <button class="view-btn" data-id="${account.accountId}">View</button>
                    <button class="edit-btn" data-id="${account.accountId}">Edit</button>
                </td>
            `;
            tbody.appendChild(row);
        });

        //Add event listeners for action buttons
        addActionButtonListeners(tbody, 'chartofaccount');
    }

    function renderGLMappings(tbody, mappings) {
        mappings.forEach(mapping => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${mapping.mappingId}</td>
                <td>${mapping.transactionType}</td>
                <td>${mapping.debitAccount}</td>
                <td>${mapping.creditAccount}</td>
                <td>${formatDate(mapping.createdAt)}</td>
                <td>
                    <button class="view-btn" data-id="${mapping.mappingId}">View</button>
                    <button class="edit-btn" data-id="${mapping.mappingId}">Edit</button>
                </td>
            `;
            tbody.appendChild(row);
        });

        //Add event listeners for action buttons
        addActionButtonListeners(tbody, 'glmapping');
    }

    function renderVouchers(tbody, vouchers) {
        vouchers.forEach(voucher => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${voucher.voucherId}</td>
                <td>${voucher.voucherNumber}</td>
                <td>${formatDate(voucher.voucherDate)}</td>
                <td>${voucher.transactionType}</td>
                <td>${formatCurrency(voucher.totalAmount)}</td>
                <td>${voucher.status}</td>
                <td>${voucher.description || ''}</td>
                <td>
                    <button class="view-btn" data-id="${voucher.voucherId}">View</button>
                    <button class="edit-btn" data-id="${voucher.voucherId}">Edit</button>
                </td>
            `;
            tbody.appendChild(row);
        });

        //Add event listeners for action buttons
        addActionButtonListeners(tbody, 'voucher');
    }

    function renderPayments(tbody, payments) {
        payments.forEach(payment => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${payment.paymentId}</td>
                <td>${payment.paymentNumber}</td>
                <td>${formatDate(payment.paymentDate)}</td>
                <td>${payment.payeeName}</td>
                <td>${payment.paymentMethod}</td>
                <td>${formatCurrency(payment.totalAmount)}</td>
                <td>${payment.status}</td>
                <td>
                    <button class="view-btn" data-id="${payment.paymentId}">View</button>
                    <button class="edit-btn" data-id="${payment.paymentId}">Edit</button>
                </td>
            `;
            tbody.appendChild(row);
        });

        //Add event listeners for action buttons
        addActionButtonListeners(tbody, 'payment');
    }

    function renderReceipts(tbody, receipts) {
        receipts.forEach(receipt => {
            const row = document.createElement('tr');
            row.innerHTML = `
                <td>${receipt.receiptId}</td>
                <td>${receipt.receiptNumber}</td>
                <td>${formatDate(receipt.date)}</td>
                <td>${receipt.payerName}</td>
                <td>${formatCurrency(receipt.amount)}</td>
                <td>${receipt.paymentMethod}</td>
                <td>${receipt.description || ''}</td>
                <td>
                    <button class="view-btn" data-id="${receipt.receiptId}">View</button>
                    <button class="edit-btn" data-id="${receipt.receiptId}">Edit</button>
                </td>
            `;
            tbody.appendChild(row);
        });

        //Add event listeners for action buttons
        addActionButtonListeners(tbody, 'receipt');
    }

    function addActionButtonListeners(tbody, entityType) {
        //View buttons
        const viewButtons = tbody.querySelectorAll('.view-btn');
        viewButtons.forEach(btn => {
            btn.addEventListener('click', () => {
                const id = btn.getAttribute('data-id');
                handleViewEntity(entityType, id);
            });
        });

        //Edit buttons
        const editButtons = tbody.querySelectorAll('.edit-btn');
        editButtons.forEach(btn => {
            btn.addEventListener('click', () => {
                const id = btn.getAttribute('data-id');
                handleEditEntity(entityType, id);
            });
        });
    }

    function handleViewEntity(entityType, id) {
        //Redirect to entity detail page
        let page;
        let paramName;

        switch (entityType) {
            case 'chartofaccount':
                page = 'coa.html';
                paramName = 'accountId';
                break;
            case 'glmapping':
                page = 'gl_mapping.html';
                paramName = 'mappingId';
                break;
            case 'voucher':
                page = 'voucher_entry.html';
                paramName = 'voucherId';
                break;
            case 'payment':
                page = 'payment_form.html';
                paramName = 'paymentId';
                break;
            case 'receipt':
                page = 'receipts.html';
                paramName = 'receiptId';
                break;
        }

        if (page) {
            window.location.href = `${page}?view=true&${paramName}=${id}`;
        }
    }

    function handleEditEntity(entityType, id) {
        //Redirect to entity edit page
        let page;
        let paramName;

        switch (entityType) {
            case 'chartofaccount':
                page = 'coa.html';
                paramName = 'accountId';
                break;
            case 'glmapping':
                page = 'gl_mapping.html';
                paramName = 'mappingId';
                break;
            case 'voucher':
                page = 'voucher_entry.html';
                paramName = 'voucherId';
                break;
            case 'payment':
                page = 'payment_form.html';
                paramName = 'paymentId';
                break;
            case 'receipt':
                page = 'receipts.html';
                paramName = 'receiptId';
                break;
        }

        if (page) {
            window.location.href = `${page}?edit=true&${paramName}=${id}`;
        }
    }

    function updatePagination() {
        pageInfo.textContent = `Page ${currentPage} of ${totalPages}`;
        prevPageBtn.disabled = currentPage <= 1;
        nextPageBtn.disabled = currentPage >= totalPages;
    }

    function navigatePage(direction) {
        const newPage = currentPage + direction;

        if (newPage >= 1 && newPage <= totalPages) {
            currentPage = newPage;
            fetchSearchResults();
        }
    }

    function handlePageSizeChange() {
        pageSize = parseInt(pageSizeSelect.value);
        currentPage = 1;
        if (currentEntityType && currentFilterType && currentFilterValue) {
            fetchSearchResults();
        }
    }

    function resetFilters(resetAll = true) {
        if (resetAll) {
            entityTypeSelect.value = '';
            currentEntityType = '';
            filterTypeContainer.style.display = 'none';
        }

        filterTypeSelect.innerHTML = '<option value="">Select Filter</option>';
        filterValueSelect.innerHTML = '<option value="">Select Value</option>';
        currentFilterType = '';
        currentFilterValue = '';
        filterValueContainer.style.display = 'none';

        hideAllTables();
        hideError();

        //Reset pagination
        currentPage = 1;
        totalPages = 1;
        updatePagination();
    }

    //UI Helper Functions
    function hideAllTables() {
        Object.values(tables).forEach(table => {
            if (table) {
                table.style.display = 'none';
            }
        });
    }

    function showLoading() {
        if (loadingIndicator) {
            loadingIndicator.style.display = 'flex';
        }
    }

    function hideLoading() {
        if (loadingIndicator) {
            loadingIndicator.style.display = 'none';
        }
    }

    function showError(message) {
        if (errorContainer) {
            errorContainer.textContent = message;
            errorContainer.style.display = 'block';
        }
    }

    function hideError() {
        if (errorContainer) {
            errorContainer.style.display = 'none';
        }
    }

    //Utility Functions
    function formatDate(dateStr) {
        if (!dateStr) return '';
        const date = new Date(dateStr);
        return date.toLocaleDateString();
    }

    function formatCurrency(amount) {
        if (amount === null || amount === undefined) return '';
        return new Intl.NumberFormat('en-US', {
            style: 'currency',
            currency: 'USD'
        }).format(amount);
    }

    console.log('Search View module initialized successfully');
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

        console.log('User authenticated, initializing Search View...');
        initializeSearchView();
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