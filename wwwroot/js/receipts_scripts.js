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
function initializeReceipts() {
    console.log('Initializing Receipts module...');

    const apiUrl = '/api/Receipts';
    const tableBody = document.querySelector("#receipts-table tbody");
    const receiptModal = document.getElementById("add-receipt-modal");
    const addReceiptBtn = document.getElementById("add-receipt-btn");
    const closeModal = document.getElementById("close-modal");
    const receiptForm = document.getElementById("add-receipt-form");
    const modalTitle = document.querySelector(".modal-content h2");
    const submitButton = document.querySelector("#add-receipt-form button[type='submit']");

    //Form elements
    const receiptNumber = document.getElementById("receipt-number");
    const payerName = document.getElementById("payer-name");
    const amount = document.getElementById("amount");
    const currency = document.getElementById("currency");
    const date = document.getElementById("date");
    const paymentMethod = document.getElementById("payment-method");
    const description = document.getElementById("description");

    let isEditMode = false;
    let editingReceiptId = null;

    //Validation functions
    function validateReceiptNumber(value) {
        return value.trim() !== '';
    }

    function validatePayerName(value) {
        return value.trim() !== '';
    }

    function validateAmount(value) {
        return !isNaN(value) && parseFloat(value) > 0;
    }

    function validateForm() {
        let isValid = true;

        //Clear previous errors
        clearErrors();

        //Validate Receipt Number
        if (!validateReceiptNumber(receiptNumber.value)) {
            document.getElementById("receipt-number-error").textContent = "Please enter a valid receipt number";
            receiptNumber.classList.add('invalid');
            isValid = false;
        }

        //Validate Payer Name
        if (!validatePayerName(payerName.value)) {
            document.getElementById("payer-name-error").textContent = "Please enter a payer name";
            payerName.classList.add('invalid');
            isValid = false;
        }

        //Validate Amount
        if (!validateAmount(amount.value)) {
            document.getElementById("amount-error").textContent = "Please enter a valid amount greater than 0";
            amount.classList.add('invalid');
            isValid = false;
        }

        //Validate Currency
        if (!currency.value) {
            document.getElementById("currency-error").textContent = "Please select a currency";
            currency.classList.add('invalid');
            isValid = false;
        }

        //Validate Date
        if (!date.value) {
            document.getElementById("date-error").textContent = "Please select a date";
            date.classList.add('invalid');
            isValid = false;
        }

        //Validate Payment Method
        if (!paymentMethod.value) {
            document.getElementById("payment-method-error").textContent = "Please select a payment method";
            paymentMethod.classList.add('invalid');
            isValid = false;
        }

        //Validate Description
        if (!description.value.trim()) {
            document.getElementById("description-error").textContent = "Please enter a description";
            description.classList.add('invalid');
            isValid = false;
        }

        return isValid;
    }

    //Generate receipt number function
    async function generateReceiptNumber() {
        try {
            const response = await AuthManager.makeAuthenticatedRequest(`${apiUrl}/GetNewReceiptNumber`);
            if (response && response.ok) {
                const data = await response.json();
                return data.receiptNumber;
            }
        } catch (error) {
            console.error('Error generating receipt number:', error);
        }

        // Fallback generation
        const today = new Date();
        const year = today.getFullYear();
        const month = String(today.getMonth() + 1).padStart(2, '0');
        const day = String(today.getDate()).padStart(2, '0');
        const random = Math.floor(Math.random() * 1000).toString().padStart(3, '0');
        return `RCP-${year}${month}${day}-${random}`;
    }

    //Show/hide modal functions
    async function showModal(isEdit = false) {
        isEditMode = isEdit;
        modalTitle.textContent = isEdit ? "Edit Receipt" : "Add New Receipt";
        submitButton.textContent = isEdit ? "Update Receipt" : "Add Receipt";
        receiptModal.style.display = "flex";

        //Set today's date as default for new receipts
        if (!isEdit) {
            const today = new Date().toISOString().split('T')[0];
            date.value = today;

            //Automatically generate receipt number for new receipts
            const newReceiptNumber = await generateReceiptNumber();
            receiptNumber.value = newReceiptNumber;
        }
    }

    function hideModal() {
        receiptModal.style.display = "none";
        clearErrors();
        receiptForm.reset();
        isEditMode = false;
        editingReceiptId = null;
    }

    //Clear error messages
    function clearErrors() {
        document.querySelectorAll('.error-message').forEach(error => error.textContent = '');
        document.querySelectorAll('.invalid').forEach(field => field.classList.remove('invalid'));
    }

    //Format date for display
    function formatDate(dateString) {
        const date = new Date(dateString);
        return date.toLocaleDateString();
    }

    //Format currency for display
    function formatCurrency(amount, currencyCode) {
        return `${amount} ${currencyCode}`;
    }

    //Add row to table function
    function addRow(receipt) {
        const row = document.createElement("tr");
        row.setAttribute('data-receipt-id', receipt.receiptId);
        row.innerHTML = `
            <td>${receipt.receiptId}</td>
            <td>${receipt.receiptNumber}</td>
            <td>${receipt.payerName}</td>
            <td>${receipt.amount.toFixed(2)}</td>
            <td>${receipt.currency}</td>
            <td>${formatDate(receipt.date)}</td>
            <td>${receipt.paymentMethod}</td>
            <td>${receipt.description}</td>
            <td>${formatDate(receipt.createdAt)}</td>
            <td>${formatDate(receipt.updatedAt)}</td>
            <td>
                <button class="edit-btn" data-receipt-id="${receipt.receiptId}">Edit</button>
                <button class="delete-btn" data-receipt-id="${receipt.receiptId}">Delete</button>
                <button class="pdf-btn" data-receipt-id="${receipt.receiptId}">PDF</button>
            </td>
        `;

        //Add edit, delete, and PDF button event listeners
        const editBtn = row.querySelector('.edit-btn');
        const deleteBtn = row.querySelector('.delete-btn');
        const pdfBtn = row.querySelector('.pdf-btn');

        editBtn.addEventListener('click', () => handleEdit(receipt.receiptId));
        deleteBtn.addEventListener('click', () => handleDelete(receipt.receiptId));
        pdfBtn.addEventListener('click', () => generateReceiptPdf(receipt.receiptId));

        if (isEditMode && receipt.receiptId === editingReceiptId) {
            const existingRow = document.querySelector(`tr[data-receipt-id="${receipt.receiptId}"]`);
            if (existingRow) {
                tableBody.replaceChild(row, existingRow);
            } else {
                tableBody.appendChild(row);
            }
        } else {
            tableBody.appendChild(row);
        }
    }

    //PDF Generation Function
    async function generateReceiptPdf(receiptId) {
        try {
            console.log('Generating PDF for receipt:', receiptId);

            //Show loading state
            const pdfButton = document.querySelector(`button[data-receipt-id="${receiptId}"].pdf-btn`);
            const originalText = pdfButton.textContent;
            pdfButton.textContent = 'Generating...';
            pdfButton.disabled = true;

            const response = await AuthManager.makeAuthenticatedRequest(`${apiUrl}/${receiptId}/pdf`);

            if (!response || !response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.message || 'Failed to generate PDF');
            }

            //Get the PDF blob
            const blob = await response.blob();

            //Create download link
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `Receipt-${receiptId}.pdf`;

            //Trigger download
            document.body.appendChild(link);
            link.click();

            //Cleanup
            document.body.removeChild(link);
            window.URL.revokeObjectURL(url);

            showSuccess('PDF generated successfully');

        } catch (error) {
            console.error('Error generating PDF:', error);
            showError('Failed to generate PDF: ' + error.message);
        } finally {
            //Restore button state
            const pdfButton = document.querySelector(`button[data-receipt-id="${receiptId}"].pdf-btn`);
            if (pdfButton) {
                pdfButton.textContent = 'PDF';
                pdfButton.disabled = false;
            }
        }
    }

    //Edit receipt function
    async function handleEdit(receiptId) {
        try {
            const response = await AuthManager.makeAuthenticatedRequest(`${apiUrl}/${receiptId}`);
            if (!response || !response.ok) {
                throw new Error('Failed to fetch receipt details');
            }

            const receipt = await response.json();

            //Populate form with receipt details
            receiptNumber.value = receipt.receiptNumber;
            payerName.value = receipt.payerName;
            amount.value = receipt.amount;
            currency.value = receipt.currency;
            date.value = new Date(receipt.date).toISOString().split('T')[0];
            paymentMethod.value = receipt.paymentMethod;
            description.value = receipt.description;

            editingReceiptId = receiptId;
            await showModal(true);
        } catch (error) {
            console.error('Error fetching receipt details:', error);
            alert('Failed to fetch receipt details. Please try again.');
        }
    }

    //Delete receipt function
    async function handleDelete(receiptId) {
        if (!confirm(`Are you sure you want to delete receipt ${receiptId}?`)) {
            return;
        }

        try {
            const response = await AuthManager.makeAuthenticatedRequest(`${apiUrl}/${receiptId}`, {
                method: 'DELETE'
            });

            if (!response || !response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.message || 'Failed to delete receipt');
            }

            //Remove the row from the table
            const row = document.querySelector(`tr[data-receipt-id="${receiptId}"]`);
            if (row) {
                row.remove();
            }

            alert('Receipt deleted successfully');
        } catch (error) {
            console.error('Error deleting receipt:', error);
            alert(error.message || 'Failed to delete receipt. Please try again.');
        }
    }

    //Form submission handler
    receiptForm.onsubmit = async function (e) {
        e.preventDefault();

        if (!validateForm()) return;

        const receiptData = {
            receiptNumber: receiptNumber.value,
            payerName: payerName.value,
            amount: parseFloat(amount.value),
            currency: currency.value,
            date: date.value,
            paymentMethod: paymentMethod.value,
            description: description.value
        };

        try {
            const url = isEditMode ? `${apiUrl}/${editingReceiptId}` : apiUrl;
            const method = isEditMode ? "PUT" : "POST";

            if (isEditMode) {
                receiptData.receiptId = editingReceiptId;
            }

            const response = await AuthManager.makeAuthenticatedRequest(url, {
                method: method,
                body: JSON.stringify(receiptData)
            });

            if (!response || !response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.message || `Failed to ${isEditMode ? 'update' : 'add'} receipt`);
            }

            const savedReceipt = await response.json();
            addRow(savedReceipt);
            hideModal();

            alert(`Receipt ${isEditMode ? 'updated' : 'added'} successfully`);

        } catch (error) {
            console.error(`Error ${isEditMode ? 'updating' : 'adding'} receipt:`, error);
            alert(error.message || `Failed to ${isEditMode ? 'update' : 'add'} receipt. Please try again.`);
        }
    };

    //Event listeners
    addReceiptBtn.onclick = () => showModal(false);
    closeModal.onclick = hideModal;

    //Clear form field errors on input
    receiptNumber.addEventListener('input', () => {
        document.getElementById("receipt-number-error").textContent = "";
        receiptNumber.classList.remove('invalid');
    });

    payerName.addEventListener('input', () => {
        document.getElementById("payer-name-error").textContent = "";
        payerName.classList.remove('invalid');
    });

    amount.addEventListener('input', () => {
        document.getElementById("amount-error").textContent = "";
        amount.classList.remove('invalid');
    });

    currency.addEventListener('change', () => {
        document.getElementById("currency-error").textContent = "";
        currency.classList.remove('invalid');
    });

    date.addEventListener('change', () => {
        document.getElementById("date-error").textContent = "";
        date.classList.remove('invalid');
    });

    paymentMethod.addEventListener('change', () => {
        document.getElementById("payment-method-error").textContent = "";
        paymentMethod.classList.remove('invalid');
    });

    description.addEventListener('input', () => {
        document.getElementById("description-error").textContent = "";
        description.classList.remove('invalid');
    });

    //Close modal when clicking outside
    window.onclick = function (event) {
        if (event.target === receiptModal) {
            hideModal();
        }
    };

    //Utility Functions
    function showError(message) {
        console.error('Error:', message);
        alert(message);
    }

    function showSuccess(message) {
        console.log('Success:', message);
     
        alert(message);
    }

    //Initial data fetch
    async function loadInitialData() {
        try {
            console.log('Loading initial receipts data...');
            const response = await AuthManager.makeAuthenticatedRequest(apiUrl);
            if (!response || !response.ok) {
                throw new Error("Failed to fetch data");
            }

            const data = await response.json();
            tableBody.innerHTML = "";
            data.forEach(receipt => addRow(receipt));
            console.log('Receipts data loaded successfully');
        } catch (error) {
            console.error('Error fetching receipts:', error);
            alert('Failed to load receipts. Please refresh the page.');
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

        console.log('User authenticated, initializing Receipts...');
        initializeReceipts();
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