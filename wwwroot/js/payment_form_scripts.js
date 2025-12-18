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
function initializePayments() {
    console.log('Initializing Payments module...');

    //Get all required DOM elements
    const paymentsTable = document.getElementById('payments-table');
    const paymentModal = document.getElementById('payment-modal');
    const paymentForm = document.getElementById('payment-form');
    const addPaymentBtn = document.getElementById('add-payment-btn');
    const closeModalBtn = document.getElementById('close-modal');
    const paymentNumberInput = document.getElementById('payment-number');
    const paymentDateInput = document.getElementById('payment-date');
    const paymentMethodSelect = document.getElementById('payment-method');
    const payeeNameInput = document.getElementById('payee-name');
    const accountSelect = document.getElementById('account-id');
    const referenceNumberInput = document.getElementById('reference-number');
    const voucherDropdown = document.getElementById('voucher-dropdown');
    const paymentDetailsTable = document.getElementById('payment-details-table').querySelector('tbody');
    const totalPaymentAmount = document.getElementById('total-payment-amount');
    const saveDraftBtn = document.getElementById('save-draft-btn');
    const submitPaymentBtn = document.getElementById('submit-payment-btn');
    const printPaymentBtn = document.getElementById('print-payment-btn');
    const attachmentInput = document.getElementById('attachment-input');
    const attachmentList = document.getElementById('attachment-list');
    const paymentError = document.getElementById('payment-error');
    const descriptionInput = document.getElementById('description');

    //State Management
    let accounts = [];
    let pendingVouchers = [];
    let selectedVouchers = [];
    let editingPaymentId = null;
    let attachments = [];
    let isEditMode = false;

    //Initialize the page
    initialize();

    //Initialize function - starting point
    async function initialize() {
        try {
            console.log('Starting payment form initialization...');

            //Load accounts first as they're essential
            await fetchAccounts();

            //Load payments table
            await loadPayments();

            //Setup event listeners
            setupEventListeners();

            console.log('Payment form initialization completed successfully');
        } catch (error) {
            console.error('Initialization error:', error);
            showError('Failed to initialize the payment form: ' + error.message);
        }
    }

    //Event Listeners Setup
    function setupEventListeners() {
        //Button event listeners
        if (addPaymentBtn) {
            addPaymentBtn.addEventListener('click', initNewPayment);
        }

        if (closeModalBtn) {
            closeModalBtn.addEventListener('click', closeModal);
        }

        if (paymentForm) {
            paymentForm.addEventListener('submit', handlePaymentSubmit);
        }

        if (saveDraftBtn) {
            saveDraftBtn.addEventListener('click', () => savePayment('Draft'));
        }

        if (submitPaymentBtn) {
            submitPaymentBtn.addEventListener('click', () => savePayment('Paid'));
        }

        if (printPaymentBtn) {
            printPaymentBtn.addEventListener('click', () => {
                if (editingPaymentId) {
                    printPayment(editingPaymentId);
                }
            });
        }

        //File input event listener
        if (attachmentInput) {
            attachmentInput.addEventListener('change', handleAttachments);
        }

        //Voucher dropdown change event
        if (voucherDropdown) {
            voucherDropdown.addEventListener('change', handleVoucherSelection);
        }

        //Close modal when clicking outside
        window.addEventListener('click', (e) => {
            if (e.target === paymentModal) {
                closeModal();
            }
        });
    }

    //Fetch Functions
    async function fetchAccounts() {
        try {
            console.log('Fetching accounts...');
            const response = await AuthManager.makeAuthenticatedRequest('/api/ChartOfAccounts');

            if (!response || !response.ok) {
                throw new Error(`Failed to fetch accounts: ${response?.status} ${response?.statusText}`);
            }

            accounts = await response.json();
            console.log(`Loaded ${accounts.length} accounts`);
            populatePayFromAccounts();
            return accounts;
        } catch (error) {
            console.error('Error fetching accounts:', error);
            showError('Failed to load accounts. Please refresh the page.');
            //Set empty accounts to prevent crashes
            accounts = [];
            populatePayFromAccounts();
            throw error;
        }
    }

    async function fetchPendingVouchers(search = '') {
        try {
            console.log('Fetching pending vouchers...');

            const response = await AuthManager.makeAuthenticatedRequest(`/api/Vouchers/Pending?search=${encodeURIComponent(search)}`);

            if (!response || !response.ok) {
                console.warn('Failed to fetch pending vouchers, creating fallback empty data');
                pendingVouchers = [];
                populateVoucherDropdown();
                return pendingVouchers;
            }

            const data = await response.json();
            console.log('Raw voucher data:', data);

            //Ensure each voucher has the required properties
            pendingVouchers = data.map(voucher => ({
                voucherId: voucher.voucherId,
                voucherNumber: voucher.voucherNumber || 'N/A',
                voucherDate: voucher.voucherDate,
                totalAmount: voucher.totalAmount || 0,
                remainingAmount: voucher.remainingAmount || voucher.totalAmount || 0,
                status: voucher.status || 'Pending',
                description: voucher.description || ''
            }));

            console.log(`Loaded ${pendingVouchers.length} pending vouchers`);
            console.log('Processed vouchers:', pendingVouchers);

            populateVoucherDropdown();
            return pendingVouchers;
        } catch (error) {
            console.error('Error fetching pending vouchers:', error);
            showError('Failed to load pending vouchers. Some features may be limited.');
            //Don't throw error, just set empty array
            pendingVouchers = [];
            populateVoucherDropdown();
        }
    }

    async function getNewPaymentNumber() {
        try {
            console.log('Getting new payment number...');
            const response = await AuthManager.makeAuthenticatedRequest('/api/Payments/GetNewPaymentNumber');

            if (!response || !response.ok) {
                throw new Error(`Failed to get payment number: ${response?.status} ${response?.statusText}`);
            }

            const data = await response.json();
            console.log('Generated payment number:', data.paymentNumber);
            return data.paymentNumber;
        } catch (error) {
            console.error('Error getting new payment number:', error);
            //Generate fallback payment number
            const today = new Date();
            const datePart = today.toISOString().slice(0, 10).replace(/-/g, '');
            const fallbackNumber = `PMT-${datePart}-001`;
            console.log('Using fallback payment number:', fallbackNumber);
            return fallbackNumber;
        }
    }

    async function loadPayments() {
        try {
            console.log('Loading payments...');
            const response = await AuthManager.makeAuthenticatedRequest('/api/Payments');

            if (!response || !response.ok) {
                throw new Error(`Failed to load payments: ${response?.status} ${response?.statusText}`);
            }

            const payments = await response.json();
            console.log(`Loaded ${payments.length} payments`);
            renderPaymentsTable(payments);
        } catch (error) {
            console.error('Error loading payments:', error);
            showError('Failed to load payments. Please refresh the page.');
            //Render empty table to prevent crashes
            renderPaymentsTable([]);
        }
    }

    async function getPaymentById(id) {
        try {
            console.log('Fetching payment by ID:', id);
            const response = await AuthManager.makeAuthenticatedRequest(`/api/Payments/${id}`);

            if (!response || !response.ok) {
                throw new Error(`Failed to fetch payment: ${response?.status} ${response?.statusText}`);
            }

            return await response.json();
        } catch (error) {
            console.error(`Error fetching payment ID ${id}:`, error);
            showError(`Failed to load payment details for ID ${id}`);
            throw error;
        }
    }

    //UI Rendering Functions
    function populatePayFromAccounts() {
        if (!accountSelect) return;

        accountSelect.innerHTML = '<option value="">Select Account</option>';

        accounts.forEach(account => {
            const option = document.createElement('option');
            option.value = account.accountId;
            option.textContent = `${account.accountNumber || ''} - ${account.accountName || ''}`;
            accountSelect.appendChild(option);
        });

        console.log(`Populated ${accounts.length} accounts in dropdown`);
    }

    function populateVoucherDropdown() {
        if (!voucherDropdown) {
            console.error('Voucher dropdown element not found');
            return;
        }

        console.log('Populating voucher dropdown with:', pendingVouchers);

        voucherDropdown.innerHTML = '<option value="">Select Voucher</option>';

        if (!pendingVouchers || pendingVouchers.length === 0) {
            console.log('No pending vouchers to display');
            const option = document.createElement('option');
            option.value = '';
            option.textContent = 'No pending vouchers available';
            option.disabled = true;
            voucherDropdown.appendChild(option);
            return;
        }

        pendingVouchers.forEach(voucher => {
            //Skip vouchers that are already selected
            if (selectedVouchers.find(sv => sv.voucherId === voucher.voucherId)) {
                return;
            }

            const option = document.createElement('option');
            option.value = voucher.voucherId;
            const remainingAmount = voucher.remainingAmount || voucher.totalAmount || 0;
            option.textContent = `${voucher.voucherNumber} - ${formatCurrency(remainingAmount)} - ${voucher.status}`;
            voucherDropdown.appendChild(option);
        });

        console.log(`Populated ${voucherDropdown.options.length - 1} vouchers in dropdown`);
    }

    function renderPaymentsTable(payments) {
        if (!paymentsTable) return;

        const tbody = paymentsTable.querySelector('tbody');
        if (!tbody) return;

        tbody.innerHTML = '';

        if (!payments || payments.length === 0) {
            const row = document.createElement('tr');
            row.innerHTML = '<td colspan="6" style="text-align: center; padding: 20px; color: #666;">No payments found</td>';
            tbody.appendChild(row);
            return;
        }

        payments.forEach(payment => {
            const row = document.createElement('tr');

            //Format date for display
            const paymentDate = new Date(payment.paymentDate);
            const formattedDate = paymentDate.toLocaleDateString();

            row.innerHTML = `
                <td>${payment.paymentNumber || ''}</td>
                <td>${formattedDate}</td>
                <td>${payment.payeeName || ''}</td>
                <td>${payment.paymentMethod || ''}</td>
                <td class="text-right">${formatCurrency(payment.totalAmount)}</td>
                <td><span class="status-badge ${(payment.status || 'draft').toLowerCase()}">${payment.status || 'Draft'}</span></td>
                <td>
                    <button class="edit-btn action-btn" data-id="${payment.paymentId}" ${payment.status === 'Paid' ? 'disabled' : ''}>Edit</button>
                    <button class="print-btn action-btn" data-id="${payment.paymentId}">Print</button>
                </td>
            `;

            tbody.appendChild(row);
        });

        //Add event listeners to action buttons
        const editButtons = tbody.querySelectorAll('.edit-btn');
        const printButtons = tbody.querySelectorAll('.print-btn');

        editButtons.forEach(button => {
            button.addEventListener('click', () => {
                const paymentId = parseInt(button.getAttribute('data-id'));
                editPayment(paymentId);
            });
        });

        printButtons.forEach(button => {
            button.addEventListener('click', () => {
                const paymentId = parseInt(button.getAttribute('data-id'));
                printPayment(paymentId);
            });
        });

        console.log(`Rendered ${payments.length} payments in table`);
    }

    function renderPaymentDetails() {
        if (!paymentDetailsTable) return;

        paymentDetailsTable.innerHTML = '';
        let totalAmount = 0;

        if (selectedVouchers.length === 0) {
            const row = document.createElement('tr');
            row.innerHTML = '<td colspan="6" style="text-align: center; padding: 20px; color: #666;">No vouchers selected for payment</td>';
            paymentDetailsTable.appendChild(row);
            updateTotalAmount();
            return;
        }

        selectedVouchers.forEach((voucher, index) => {
            const row = document.createElement('tr');

            const voucherDate = new Date(voucher.voucherDate);
            const formattedDate = voucherDate.toLocaleDateString();
            const remainingAmount = voucher.remainingAmount || voucher.totalAmount;
            const amountToPay = voucher.amountToPay || 0;

            row.innerHTML = `
                <td>${voucher.voucherNumber}</td>
                <td>${formattedDate}</td>
                <td class="text-right">${formatCurrency(voucher.totalAmount)}</td>
                <td class="text-right">${formatCurrency(voucher.totalAmount - remainingAmount)}</td>
                <td class="text-right">${formatCurrency(remainingAmount)}</td>
                <td>
                    <input type="number" 
                           value="${amountToPay}" 
                           max="${remainingAmount}" 
                           min="0" 
                           step="0.01" 
                           class="amount-input" 
                           data-index="${index}"
                           style="width: 100px; text-align: right; margin-right: 10px;">
                    <button type="button" class="remove-voucher-btn" data-index="${index}">Remove</button>
                </td>
            `;

            paymentDetailsTable.appendChild(row);
            totalAmount += amountToPay;
        });

        //Add event listeners to amount inputs and remove buttons
        const amountInputs = paymentDetailsTable.querySelectorAll('.amount-input');
        const removeButtons = paymentDetailsTable.querySelectorAll('.remove-voucher-btn');

        amountInputs.forEach(input => {
            input.addEventListener('change', handleAmountChange);
            input.addEventListener('input', handleAmountChange);
        });

        removeButtons.forEach(button => {
            button.addEventListener('click', handleRemoveVoucher);
        });

        updateTotalAmount();
    }

    //Event Handlers
    async function initNewPayment() {
        try {
            console.log('Initializing new payment...');
            hideError();

            //\Reset all form fields and state
            if (paymentForm) {
                paymentForm.reset();
            }
            selectedVouchers = [];
            attachments = [];
            editingPaymentId = null;
            isEditMode = false;

            //Get and set payment number
            const paymentNumber = await getNewPaymentNumber();
            if (paymentNumberInput) {
                paymentNumberInput.value = paymentNumber;
            }

            //Set current date
            const today = new Date();
            if (paymentDateInput) {
                paymentDateInput.value = today.toISOString().split('T')[0];
            }

            //Load pending vouchers (with improved error handling)
            console.log('Loading pending vouchers for new payment...');
            await fetchPendingVouchers();

            //Clear payment details table
            renderPaymentDetails();

            //Clear attachments
            if (attachmentList) {
                attachmentList.innerHTML = '';
            }

            //Update modal title and buttons
            const modalTitle = paymentModal?.querySelector('h2');
            if (modalTitle) {
                modalTitle.textContent = 'New Payment Entry';
            }

            //Show/hide buttons appropriately
            if (saveDraftBtn) saveDraftBtn.style.display = 'inline-block';
            if (submitPaymentBtn) submitPaymentBtn.style.display = 'inline-block';
            if (printPaymentBtn) printPaymentBtn.style.display = 'none';

            //Show the modal
            if (paymentModal) {
                paymentModal.style.display = 'flex';
                console.log('Payment modal opened successfully');
            }

        } catch (error) {
            console.error('Error initializing new payment:', error);
            showError('Failed to initialize new payment: ' + error.message);
        }
    }

    function closeModal() {
        if (paymentModal) {
            paymentModal.style.display = 'none';
            hideError();
            console.log('Payment modal closed');
        }
    }

    function handlePaymentSubmit(e) {
        e.preventDefault();
        console.log('Payment form submission prevented - use save buttons instead');
    }

    async function savePayment(status) {
        try {
            console.log('Saving payment with status:', status);
            hideError();

            //Validate form
            if (!validatePaymentForm()) {
                return;
            }

            //Show loading state
            const saveButton = status === 'Draft' ? saveDraftBtn : submitPaymentBtn;
            const originalText = saveButton?.textContent;
            if (saveButton) {
                saveButton.disabled = true;
                saveButton.textContent = 'Saving...';
            }

            try {
                //Prepare payment data
                const paymentData = {
                    paymentId: editingPaymentId || 0,
                    paymentNumber: paymentNumberInput?.value || '',
                    paymentDate: paymentDateInput?.value || new Date().toISOString().split('T')[0],
                    payeeName: payeeNameInput?.value || '',
                    paymentMethod: paymentMethodSelect?.value || '',
                    accountId: parseInt(accountSelect?.value || '0'),
                    totalAmount: calculateTotalAmount(),
                    referenceNumber: referenceNumberInput?.value || '',
                    description: descriptionInput?.value || '',
                    status: status,
                    paymentDetails: selectedVouchers.map(voucher => ({
                        voucherId: voucher.voucherId,
                        amount: voucher.amountToPay || 0
                    }))
                };

                console.log('Payment data to save:', paymentData);

                //Create FormData for file upload
                const formData = new FormData();
                formData.append('payment', JSON.stringify(paymentData));

                //Append attachments
                attachments.forEach((file, index) => {
                    formData.append(`attachment_${index}`, file);
                });

                //Determine API endpoint and method
                const url = isEditMode ? `/api/Payments/${editingPaymentId}` : '/api/Payments';
                const method = isEditMode ? 'PUT' : 'POST';

                const response = await AuthManager.makeAuthenticatedRequest(url, {
                    method: method,
                    body: formData
                });

                if (!response || !response.ok) {
                    const errorText = await response.text();
                    throw new Error(errorText || 'Failed to save payment');
                }

                //Success
                console.log('Payment saved successfully');
                closeModal();
                await loadPayments(); //Reload the payments table

                const action = isEditMode ? 'updated' : 'created';
                showSuccess(`Payment ${action} successfully`);

                //If payment was submitted as "Paid", refresh pending vouchers for future use
                if (status === 'Paid') {
                    console.log('Payment submitted as Paid - vouchers should now be marked as paid');
                }

            } finally {
                //Restore button state
                if (saveButton) {
                    saveButton.disabled = false;
                    saveButton.textContent = originalText;
                }
            }

        } catch (error) {
            console.error('Error saving payment:', error);
            showError('Failed to save payment: ' + error.message);
        }
    }

    async function editPayment(paymentId) {
        try {
            console.log('Editing payment:', paymentId);
            hideError();

            const payment = await getPaymentById(paymentId);

            editingPaymentId = paymentId;
            isEditMode = true;

            //Populate form fields
            if (paymentNumberInput) paymentNumberInput.value = payment.paymentNumber || '';
            if (paymentDateInput) paymentDateInput.value = payment.paymentDate?.split('T')[0] || '';
            if (payeeNameInput) payeeNameInput.value = payment.payeeName || '';
            if (paymentMethodSelect) paymentMethodSelect.value = payment.paymentMethod || '';
            if (accountSelect) accountSelect.value = payment.accountId || '';
            if (referenceNumberInput) referenceNumberInput.value = payment.referenceNumber || '';
            if (descriptionInput) descriptionInput.value = payment.description || '';

            //Load payment details (vouchers)
            selectedVouchers = (payment.paymentDetails || []).map(detail => ({
                voucherId: detail.voucherId,
                voucherNumber: detail.voucherHeader?.voucherNumber || 'N/A',
                voucherDate: detail.voucherHeader?.voucherDate || new Date().toISOString(),
                totalAmount: detail.voucherHeader?.totalAmount || 0,
                remainingAmount: detail.voucherHeader?.remainingAmount || detail.voucherHeader?.totalAmount || 0,
                amountToPay: detail.amountPaid || 0
            }));

            //Load pending vouchers and render details
            try {
                await fetchPendingVouchers();
            } catch (error) {
                console.warn('Could not refresh pending vouchers for edit');
            }
            renderPaymentDetails();

            //Update modal title and buttons
            const modalTitle = paymentModal?.querySelector('h2');
            if (modalTitle) {
                modalTitle.textContent = 'Edit Payment Entry';
            }

            //Show/hide buttons appropriately
            if (payment.status === 'Paid') {
                if (saveDraftBtn) saveDraftBtn.style.display = 'none';
                if (submitPaymentBtn) submitPaymentBtn.style.display = 'none';
            } else {
                if (saveDraftBtn) saveDraftBtn.style.display = 'inline-block';
                if (submitPaymentBtn) submitPaymentBtn.style.display = 'inline-block';
            }
            if (printPaymentBtn) printPaymentBtn.style.display = 'inline-block';

            //Show the modal
            if (paymentModal) {
                paymentModal.style.display = 'flex';
                console.log('Edit payment modal opened');
            }

        } catch (error) {
            console.error('Error editing payment:', error);
            showError('Failed to load payment for editing: ' + error.message);
        }
    }

    function printPayment(paymentId) {
        try {
            console.log('Printing payment:', paymentId);
            const printUrl = `/api/Payments/${paymentId}/print`;
            window.open(printUrl, '_blank');
        } catch (error) {
            console.error('Error printing payment:', error);
            showError('Failed to print payment');
        }
    }

    function handleAttachments(e) {
        const files = Array.from(e.target.files);
        console.log('Handling attachments:', files.length, 'files');

        attachments = attachments.concat(files);
        renderAttachmentList();
    }

    function renderAttachmentList() {
        if (!attachmentList) return;

        attachmentList.innerHTML = '';

        attachments.forEach((file, index) => {
            const fileItem = document.createElement('div');
            fileItem.className = 'attachment-item';
            fileItem.innerHTML = `
                <span>${file.name} (${formatFileSize(file.size)})</span>
                <button type="button" onclick="removeAttachment(${index})">Remove</button>
            `;
            attachmentList.appendChild(fileItem);
        });
    }

    window.removeAttachment = function (index) {
        attachments.splice(index, 1);
        renderAttachmentList();
        console.log('Removed attachment at index:', index);
    };

    async function handleVoucherSelection() {
        const selectedVoucherId = parseInt(voucherDropdown?.value || '0');

        if (!selectedVoucherId) {
            return;
        }

        console.log('Voucher selected:', selectedVoucherId);

        //Find the selected voucher
        const voucher = pendingVouchers.find(v => v.voucherId === selectedVoucherId);
        if (!voucher) {
            showError('Selected voucher not found');
            if (voucherDropdown) voucherDropdown.value = '';
            return;
        }

        //Check if already selected
        if (selectedVouchers.find(sv => sv.voucherId === selectedVoucherId)) {
            showError('Voucher already selected');
            if (voucherDropdown) voucherDropdown.value = '';
            return;
        }

        //Add to selected vouchers
        selectedVouchers.push({
            voucherId: voucher.voucherId,
            voucherNumber: voucher.voucherNumber,
            voucherDate: voucher.voucherDate,
            totalAmount: voucher.totalAmount,
            remainingAmount: voucher.remainingAmount || voucher.totalAmount,
            amountToPay: voucher.remainingAmount || voucher.totalAmount
        });

        //Reset dropdown and re-populate
        if (voucherDropdown) voucherDropdown.value = '';
        populateVoucherDropdown();

        //Re-render payment details
        renderPaymentDetails();

        console.log('Added voucher to payment:', voucher.voucherNumber);
    }

    function handleAmountChange(e) {
        const index = parseInt(e.target.getAttribute('data-index'));
        const amount = parseFloat(e.target.value) || 0;

        if (!selectedVouchers[index]) return;

        const maxAmount = selectedVouchers[index].remainingAmount;

        if (amount > maxAmount) {
            e.target.value = maxAmount;
            selectedVouchers[index].amountToPay = maxAmount;
            showError(`Amount cannot exceed remaining amount of ${formatCurrency(maxAmount)}`);
        } else {
            selectedVouchers[index].amountToPay = amount;
        }

        updateTotalAmount();
    }

    function handleRemoveVoucher(e) {
        const index = parseInt(e.target.getAttribute('data-index'));
        const removedVoucher = selectedVouchers[index];

        selectedVouchers.splice(index, 1);

        console.log('Removed voucher from payment:', removedVoucher?.voucherNumber);

        //Re-populate voucher dropdown and re-render details
        populateVoucherDropdown();
        renderPaymentDetails();
    }

    function updateTotalAmount() {
        const total = calculateTotalAmount();
        if (totalPaymentAmount) {
            totalPaymentAmount.textContent = formatCurrency(total);
        }
    }

    function calculateTotalAmount() {
        return selectedVouchers.reduce((total, voucher) => {
            return total + (voucher.amountToPay || 0);
        }, 0);
    }

    function validatePaymentForm() {
        let isValid = true;
        const requiredFields = [
            { element: paymentDateInput, name: 'Payment date' },
            { element: payeeNameInput, name: 'Payee name' },
            { element: paymentMethodSelect, name: 'Payment method' },
            { element: accountSelect, name: 'Account selection' }
        ];

        //Clear previous error styles
        requiredFields.forEach(field => {
            if (field.element) {
                field.element.classList.remove('invalid');
            }
        });

        //Validate required fields
        requiredFields.forEach(field => {
            if (!field.element || !field.element.value || !field.element.value.trim()) {
                if (field.element) {
                    field.element.classList.add('invalid');
                }
                showError(`${field.name} is required`);
                isValid = false;
            }
        });

        if (selectedVouchers.length === 0) {
            showError('At least one voucher must be selected');
            isValid = false;
        }

        const totalAmount = calculateTotalAmount();
        if (totalAmount <= 0) {
            showError('Total payment amount must be greater than zero');
            isValid = false;
        }

        return isValid;
    }

    //Utility Functions
    function formatCurrency(amount) {
        return new Intl.NumberFormat('en-US', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        }).format(amount || 0);
    }

    function formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }

    function showError(message) {
        if (paymentError) {
            paymentError.textContent = message;
            paymentError.style.display = 'block';

            //Auto-hide after 5 seconds
            setTimeout(() => {
                hideError();
            }, 5000);
        }
        console.error('Payment Error:', message);
    }

    function hideError() {
        if (paymentError) {
            paymentError.textContent = '';
            paymentError.style.display = 'none';
        }
    }

    function showSuccess(message) {
        console.log('Success:', message);
     
        alert(message);
    }

    console.log('Payments module initialized successfully');
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

        console.log('User authenticated, initializing Payments...');
        initializePayments();
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