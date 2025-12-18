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
function initializeCOA() {
    console.log('Initializing COA module...');

    const apiUrl = '/api/ChartOfAccounts';
    const tableBody = document.querySelector("#accounts-table tbody");
    const accountModal = document.getElementById("add-account-modal");
    const addAccountBtn = document.getElementById("add-account-btn");
    const closeModal = document.getElementById("close-modal");
    const accountForm = document.getElementById("add-account-form");
    const modalTitle = document.querySelector(".modal-content h2");
    const submitButton = document.querySelector("#add-account-form button[type='submit']");

    //Form elements
    const accountNumber = document.getElementById("account-number");
    const accountName = document.getElementById("account-name");
    const accountType = document.getElementById("account-type");
    const subaccount = document.getElementById("subaccount");
    const parentAccount = document.getElementById("parent-account");
    const description = document.getElementById("description");

    let isEditMode = false;
    let editingAccountId = null;

    //Subaccount options mapping
    const subaccountOptions = {
        'Asset': ['Current Asset', 'Fixed Asset', 'Intangible Asset', 'Other Asset'],
        'Liability': ['Current Liability', 'Long-Term Liability'],
        'Equity': ['Retained Earnings', 'Capital Stock', 'Owner\'s Equity'],
        'Revenue': ['Sales Revenue', 'Service Revenue', 'Interest Income'],
        'Expense': ['Operating Expense', 'Administrative Expense', 'COGS']
    };

    //Update subaccount dropdown based on account type selection
    accountType.addEventListener('change', updateSubaccountOptions);

    function updateSubaccountOptions() {
        const selectedType = accountType.value;
        subaccount.innerHTML = '<option value="">Select Subaccount</option>';

        if (selectedType && subaccountOptions[selectedType]) {
            subaccountOptions[selectedType].forEach(option => {
                const optionElement = document.createElement('option');
                optionElement.value = option;
                optionElement.textContent = option;
                subaccount.appendChild(optionElement);
            });
        }
    }

    //Validation functions
    function validateAccountNumber(value) {
        return /^\d+$/.test(value);
    }

    function validateAccountName(value) {
        return /^[A-Za-z\s]+$/.test(value);
    }

    function validateParentAccount(value) {
        return value === '' || /^\d+$/.test(value);
    }

    function validateForm() {
        let isValid = true;

        //Clear previous errors
        clearErrors();

        //Validate Account Number
        if (!validateAccountNumber(accountNumber.value)) {
            document.getElementById("account-number-error").textContent = "Please enter a valid number";
            accountNumber.classList.add('invalid');
            isValid = false;
        }

        //Validate Account Name
        if (!validateAccountName(accountName.value)) {
            document.getElementById("account-name-error").textContent = "Please enter only letters and spaces";
            accountName.classList.add('invalid');
            isValid = false;
        }

        //Validate Account Type
        if (!accountType.value) {
            document.getElementById("account-type-error").textContent = "Please select an account type";
            accountType.classList.add('invalid');
            isValid = false;
        }

        //Validate Subaccount (if account type is selected)
        if (accountType.value && !subaccount.value) {
            document.getElementById("subaccount-error").textContent = "Please select a subaccount";
            subaccount.classList.add('invalid');
            isValid = false;
        }

        //Validate Parent Account if provided
        if (parentAccount.value && !validateParentAccount(parentAccount.value)) {
            document.getElementById("parent-account-error").textContent = "Please enter a valid number";
            parentAccount.classList.add('invalid');
            isValid = false;
        }

        return isValid;
    }

    //Show/hide modal functions
    function showModal(isEdit = false) {
        isEditMode = isEdit;
        modalTitle.textContent = isEdit ? "Edit Account" : "Add New Account";
        submitButton.textContent = isEdit ? "Update Account" : "Add Account";
        accountModal.style.display = "flex";
    }

    function hideModal() {
        accountModal.style.display = "none";
        clearErrors();
        accountForm.reset();
        subaccount.innerHTML = '<option value="">Select Subaccount</option>';
        isEditMode = false;
        editingAccountId = null;
    }

    //Clear error messages
    function clearErrors() {
        document.querySelectorAll('.error-message').forEach(error => error.textContent = '');
        document.querySelectorAll('.invalid').forEach(field => field.classList.remove('invalid'));
    }

    //Add row to table function
    function addRow(account) {
        const row = document.createElement("tr");
        row.setAttribute('data-account-id', account.accountId);
        row.innerHTML = `
            <td>${account.accountId}</td>
            <td>${account.accountNumber}</td>
            <td>${account.accountName}</td>
            <td>${account.accountType}</td>
            <td>${account.subaccount || ''}</td>
            <td>${account.parentAccount || ''}</td>
            <td>${account.description || ''}</td>
            <td>${new Date(account.createdAt).toLocaleDateString()}</td>
            <td>${new Date(account.updatedAt).toLocaleDateString()}</td>
            <td>
                <button class="edit-btn" data-account-id="${account.accountId}">Edit</button>
                <button class="delete-btn" data-account-id="${account.accountId}">Delete</button>
            </td>
        `;

        //Add edit and delete button event listeners
        const editBtn = row.querySelector('.edit-btn');
        const deleteBtn = row.querySelector('.delete-btn');

        editBtn.addEventListener('click', () => handleEdit(account.accountId));
        deleteBtn.addEventListener('click', () => handleDelete(account.accountId));

        if (isEditMode && account.accountId === editingAccountId) {
            const existingRow = document.querySelector(`tr[data-account-id="${account.accountId}"]`);
            if (existingRow) {
                existingRow.replaceWith(row);
            }
        } else {
            tableBody.appendChild(row);
        }
    }

    //Edit account function
    async function handleEdit(accountId) {
        try {
            const response = await AuthManager.makeAuthenticatedRequest(`${apiUrl}/${accountId}`);
            if (!response || !response.ok) {
                throw new Error('Failed to fetch account details');
            }

            const account = await response.json();

            //Populate form with account details
            accountNumber.value = account.accountNumber;
            accountName.value = account.accountName;
            accountType.value = account.accountType;

            //Update subaccount options based on account type
            updateSubaccountOptions();

            //Set the subaccount value after populating options
            if (account.subaccount) {
                setTimeout(() => {
                    subaccount.value = account.subaccount;
                }, 50);
            }

            parentAccount.value = account.parentAccount || '';
            description.value = account.description || '';

            editingAccountId = accountId;
            showModal(true);
        } catch (error) {
            console.error('Error fetching account details:', error);
            alert('Failed to fetch account details. Please try again.');
        }
    }

    //Delete account function
    async function handleDelete(accountId) {
        if (!confirm(`Are you sure you want to delete account ${accountId}?`)) {
            return;
        }

        try {
            const response = await AuthManager.makeAuthenticatedRequest(`${apiUrl}/${accountId}`, {
                method: 'DELETE'
            });

            if (!response || !response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.message || 'Failed to delete account');
            }

            //Remove the row from the table
            const row = document.querySelector(`tr[data-account-id="${accountId}"]`);
            if (row) {
                row.remove();
            }
        } catch (error) {
            console.error('Error deleting account:', error);
            alert(error.message || 'Failed to delete account. Please try again.');
        }
    }

    //Form submission handler
    accountForm.onsubmit = async function (e) {
        e.preventDefault();
        clearErrors();

        if (!validateForm()) return;

        const accountData = {
            accountNumber: accountNumber.value,
            accountName: accountName.value,
            accountType: accountType.value,
            subaccount: subaccount.value,
            parentAccount: parentAccount.value ? parseInt(parentAccount.value) : null,
            description: description.value || null
        };

        try {
            const url = isEditMode ? `${apiUrl}/${editingAccountId}` : apiUrl;
            const method = isEditMode ? "PUT" : "POST";

            const response = await AuthManager.makeAuthenticatedRequest(url, {
                method: method,
                body: JSON.stringify(accountData)
            });

            if (!response || !response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.message || `Failed to ${isEditMode ? 'update' : 'add'} account`);
            }

            const savedAccount = await response.json();
            addRow(savedAccount);
            hideModal();

        } catch (error) {
            console.error(`Error ${isEditMode ? 'updating' : 'adding'} account:`, error);
            alert(error.message || `Failed to ${isEditMode ? 'update' : 'add'} account. Please try again.`);
        }
    };

    //Event listeners
    addAccountBtn.onclick = () => showModal(false);
    closeModal.onclick = hideModal;

    //Clear form field errors on input
    accountNumber.addEventListener('input', () => {
        document.getElementById("account-number-error").textContent = "";
        accountNumber.classList.remove('invalid');
    });

    accountName.addEventListener('input', () => {
        document.getElementById("account-name-error").textContent = "";
        accountName.classList.remove('invalid');
    });

    accountType.addEventListener('change', () => {
        document.getElementById("account-type-error").textContent = "";
        accountType.classList.remove('invalid');
    });

    subaccount.addEventListener('change', () => {
        document.getElementById("subaccount-error").textContent = "";
        subaccount.classList.remove('invalid');
    });

    parentAccount.addEventListener('input', () => {
        document.getElementById("parent-account-error").textContent = "";
        parentAccount.classList.remove('invalid');
    });

    //Close modal when clicking outside
    window.onclick = function (event) {
        if (event.target === accountModal) {
            hideModal();
        }
    };

    //Initial data fetch
    async function loadInitialData() {
        try {
            console.log('Loading initial COA data...');
            const response = await AuthManager.makeAuthenticatedRequest(apiUrl);
            if (!response || !response.ok) {
                throw new Error("Failed to fetch data");
            }

            const data = await response.json();
            tableBody.innerHTML = "";
            data.forEach(account => addRow(account));
            console.log('COA data loaded successfully');
        } catch (error) {
            console.error('Error fetching chart of accounts:', error);
            alert('Failed to load chart of accounts. Please refresh the page.');
        }
    }

    //Load initial data
    loadInitialData();
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

        console.log('User authenticated, initializing COA...');
        initializeCOA();
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