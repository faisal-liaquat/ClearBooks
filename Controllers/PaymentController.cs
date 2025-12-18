using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClearBooksFYP.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SelectPdf;

namespace ClearBooksFYP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : BaseController
    {
        private readonly ClearBooksDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(
            ClearBooksDbContext context,
            IWebHostEnvironment environment,
            ILogger<PaymentsController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        //getting all payments
        // GET: api/Payments
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaymentHeader>>> GetPayments()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                return await _context.PaymentHeaders
                    .Include(p => p.PaymentDetails)
                    .Where(p => p.UserId == userId)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payments");
                return StatusCode(500, "Internal server error while retrieving payments");
            }
        }

        //getting single payments
        // GET: api/Payments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PaymentHeader>> GetPayment(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var payment = await _context.PaymentHeaders
                    .Include(p => p.PaymentDetails)
                        .ThenInclude(d => d.VoucherHeader)
                    .Include(p => p.PaymentAttachments)
                    .FirstOrDefaultAsync(p => p.PaymentId == id && p.UserId == userId);

                if (payment == null)
                {
                    return NotFound($"Payment with ID {id} not found");
                }

                return payment;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving payment with ID {id}");
                return StatusCode(500, $"Internal server error while retrieving payment with ID {id}");
            }
        }

        // generate a new payment number based on date
        // GET: api/Payments/GetNewPaymentNumber
        [HttpGet("GetNewPaymentNumber")]
        public async Task<ActionResult<object>> GetNewPaymentNumber()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var today = DateTime.Now.ToString("yyyyMMdd");
                var prefix = $"PMT-{today}-";

                var lastPayment = await _context.PaymentHeaders
                    .Where(p => p.UserId == userId && p.PaymentNumber.StartsWith(prefix))
                    .OrderByDescending(p => p.PaymentNumber)
                    .FirstOrDefaultAsync();

                int sequence = 1;
                if (lastPayment != null && int.TryParse(lastPayment.PaymentNumber.Substring(prefix.Length), out int lastSequence))
                {
                    sequence = lastSequence + 1;
                }

                return new { paymentNumber = $"{prefix}{sequence:D3}" };
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating new payment number");
                return StatusCode(500, "Internal server error while generating payment number");
            }
        }

        //create a new payment using payment form
        // POST: api/Payments
        [HttpPost]
        public async Task<ActionResult<PaymentHeader>> CreatePayment()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);

                // Parse the payment data from the form
                if (!Request.Form.TryGetValue("payment", out var paymentJson))
                {
                    return BadRequest("Payment data is required");
                }

                var paymentDto = JsonConvert.DeserializeObject<PaymentDto>(paymentJson);
                if (paymentDto == null)
                {
                    return BadRequest("Invalid payment data format");
                }

                // Begin transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Validate that the account belongs to the current user
                    var accountExists = await _context.ChartOfAccounts
                        .AnyAsync(c => c.AccountId == paymentDto.AccountId && c.UserId == userId);

                    if (!accountExists)
                    {
                        return BadRequest("Account not found or not accessible");
                    }

                    // Validate that all vouchers belong to the current user and are not already paid
                    if (paymentDto.PaymentDetails != null && paymentDto.PaymentDetails.Any())
                    {
                        var voucherIds = paymentDto.PaymentDetails.Select(pd => pd.VoucherId).ToList();
                        var vouchers = await _context.VoucherHeaders
                            .Where(v => v.UserId == userId && voucherIds.Contains(v.VoucherId))
                            .ToListAsync();

                        if (vouchers.Count != voucherIds.Count)
                        {
                            return BadRequest("One or more vouchers are not accessible to the current user");
                        }

                        // Check if any vouchers are already fully paid
                        foreach (var voucher in vouchers)
                        {
                            var totalPaid = await _context.PaymentDetails
                                .Include(pd => pd.Payment)
                                .Where(pd => pd.VoucherId == voucher.VoucherId &&
                                           pd.Payment.UserId == userId &&
                                           pd.Payment.Status == "Paid")
                                .SumAsync(pd => pd.AmountPaid);

                            if (totalPaid >= voucher.TotalAmount)
                            {
                                return BadRequest($"Voucher {voucher.VoucherNumber} is already fully paid");
                            }
                        }
                    }

                    // Create payment header
                    var payment = new PaymentHeader
                    {
                        UserId = userId,
                        PaymentNumber = paymentDto.PaymentNumber,
                        PaymentDate = DateTime.Parse(paymentDto.PaymentDate),
                        PayeeName = paymentDto.PayeeName,
                        PaymentMethod = paymentDto.PaymentMethod,
                        AccountId = paymentDto.AccountId,
                        TotalAmount = paymentDto.TotalAmount,
                        ReferenceNumber = paymentDto.ReferenceNumber,
                        Description = paymentDto.Description,
                        Status = paymentDto.Status,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.PaymentHeaders.Add(payment);
                    await _context.SaveChangesAsync();

                    // Process payment details (vouchers being paid)
                    if (paymentDto.PaymentDetails != null && paymentDto.PaymentDetails.Any())
                    {
                        foreach (var detail in paymentDto.PaymentDetails)
                        {
                            var voucherId = detail.VoucherId;
                            var voucher = await _context.VoucherHeaders
                                .FirstOrDefaultAsync(v => v.VoucherId == voucherId && v.UserId == userId);

                            if (voucher == null)
                            {
                                throw new Exception($"Voucher with ID {voucherId} not found");
                            }

                            // Add payment detail
                            var paymentDetail = new PaymentDetail
                            {
                                PaymentId = payment.PaymentId,
                                VoucherId = voucherId,
                                AmountPaid = detail.Amount,
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };

                            _context.PaymentDetails.Add(paymentDetail);
                        }

                        // Save payment details first
                        await _context.SaveChangesAsync();

                        // Update voucher statuses for all payment details
                        foreach (var detail in paymentDto.PaymentDetails)
                        {
                            await UpdateVoucherStatus(detail.VoucherId, userId);
                        }
                    }

                    // Process attachments
                    var attachments = Request.Form.Files;
                    if (attachments != null && attachments.Count > 0)
                    {
                        string uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads", "Payments", payment.PaymentId.ToString());
                        Directory.CreateDirectory(uploadsFolder);

                        foreach (var file in attachments)
                        {
                            if (file.Length > 0)
                            {
                                // Generate unique filename
                                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                                string filePath = Path.Combine(uploadsFolder, fileName);

                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                // Save attachment record
                                var attachment = new PaymentAttachment
                                {
                                    PaymentId = payment.PaymentId,
                                    FileName = file.FileName,
                                    FilePath = Path.Combine("Uploads", "Payments", payment.PaymentId.ToString(), fileName),
                                    FileType = file.ContentType,
                                    UploadDate = DateTime.Now
                                };

                                _context.PaymentAttachments.Add(attachment);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return CreatedAtAction(nameof(GetPayment), new { id = payment.PaymentId }, payment);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during payment creation");
                    throw;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating payment");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        //update payment 
        // PUT: api/Payments/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePayment(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);

                // Parse the payment data from the form
                if (!Request.Form.TryGetValue("payment", out var paymentJson))
                {
                    return BadRequest("Payment data is required");
                }

                var paymentDto = JsonConvert.DeserializeObject<PaymentDto>(paymentJson);
                if (paymentDto == null || paymentDto.PaymentId != id)
                {
                    return BadRequest("Invalid payment data or mismatched payment ID");
                }

                var existingPayment = await _context.PaymentHeaders
                    .Include(p => p.PaymentDetails)
                    .FirstOrDefaultAsync(p => p.PaymentId == id && p.UserId == userId);

                if (existingPayment == null)
                {
                    return NotFound($"Payment with ID {id} not found");
                }

                // Check if payment is already finalized (can't update paid payments)
                if (existingPayment.Status == "Paid")
                {
                    return BadRequest("Cannot modify a payment that has already been processed");
                }

                // Begin transaction
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Validate that the account belongs to the current user
                    var accountExists = await _context.ChartOfAccounts
                        .AnyAsync(c => c.AccountId == paymentDto.AccountId && c.UserId == userId);

                    if (!accountExists)
                    {
                        return BadRequest("Account not found or not accessible");
                    }

                    // Update payment header
                    existingPayment.PaymentDate = DateTime.Parse(paymentDto.PaymentDate);
                    existingPayment.PayeeName = paymentDto.PayeeName;
                    existingPayment.PaymentMethod = paymentDto.PaymentMethod;
                    existingPayment.AccountId = paymentDto.AccountId;
                    existingPayment.TotalAmount = paymentDto.TotalAmount;
                    existingPayment.ReferenceNumber = paymentDto.ReferenceNumber;
                    existingPayment.Description = paymentDto.Description;
                    existingPayment.Status = paymentDto.Status;
                    existingPayment.UpdatedAt = DateTime.Now;

                    // Remove existing payment details and reset voucher statuses
                    var oldVoucherIds = existingPayment.PaymentDetails.Select(pd => pd.VoucherId).Distinct().ToList();
                    _context.PaymentDetails.RemoveRange(existingPayment.PaymentDetails);
                    await _context.SaveChangesAsync();

                    // Reset old voucher statuses
                    foreach (var voucherId in oldVoucherIds)
                    {
                        await UpdateVoucherStatus(voucherId, userId);
                    }

                    // Add new payment details
                    if (paymentDto.PaymentDetails != null && paymentDto.PaymentDetails.Any())
                    {
                        foreach (var detail in paymentDto.PaymentDetails)
                        {
                            var voucherId = detail.VoucherId;
                            var voucher = await _context.VoucherHeaders
                                .FirstOrDefaultAsync(v => v.VoucherId == voucherId && v.UserId == userId);

                            if (voucher == null)
                            {
                                throw new Exception($"Voucher with ID {voucherId} not found");
                            }

                            // Add payment detail
                            var paymentDetail = new PaymentDetail
                            {
                                PaymentId = existingPayment.PaymentId,
                                VoucherId = voucherId,
                                AmountPaid = detail.Amount,
                                CreatedAt = DateTime.Now,
                                UpdatedAt = DateTime.Now
                            };

                            _context.PaymentDetails.Add(paymentDetail);
                        }

                        // Save payment details first
                        await _context.SaveChangesAsync();

                        // Update voucher statuses for new payment details
                        foreach (var detail in paymentDto.PaymentDetails)
                        {
                            await UpdateVoucherStatus(detail.VoucherId, userId);
                        }
                    }

                    // Process new attachments
                    var attachments = Request.Form.Files;
                    if (attachments != null && attachments.Count > 0)
                    {
                        string uploadsFolder = Path.Combine(_environment.ContentRootPath, "Uploads", "Payments", existingPayment.PaymentId.ToString());
                        Directory.CreateDirectory(uploadsFolder);

                        foreach (var file in attachments)
                        {
                            if (file.Length > 0)
                            {
                                // Generate unique filename
                                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                                string filePath = Path.Combine(uploadsFolder, fileName);

                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                // Save attachment record
                                var attachment = new PaymentAttachment
                                {
                                    PaymentId = existingPayment.PaymentId,
                                    FileName = file.FileName,
                                    FilePath = Path.Combine("Uploads", "Payments", existingPayment.PaymentId.ToString(), fileName),
                                    FileType = file.ContentType,
                                    UploadDate = DateTime.Now
                                };

                                _context.PaymentAttachments.Add(attachment);
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return NoContent();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during payment update");
                    throw;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating payment with ID {id}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // generates pdf receipt
        // GET: api/Payments/5/print
        [HttpGet("{id}/print")]
        public async Task<IActionResult> PrintPayment(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var payment = await _context.PaymentHeaders
                    .Include(p => p.PaymentDetails)
                        .ThenInclude(d => d.VoucherHeader)
                    .Include(p => p.ChartOfAccount)
                    .FirstOrDefaultAsync(p => p.PaymentId == id && p.UserId == userId);

                if (payment == null)
                {
                    return NotFound($"Payment with ID {id} not found");
                }

                // Get user information
                var user = await GetCurrentUserAsync(_context);

                // Generate PDF using SelectPdf library
                var pdfBytes = GeneratePaymentPdfBytes(payment, user);

                return File(pdfBytes, "application/pdf", $"Payment-{payment.PaymentNumber}.pdf");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating payment report for ID {id}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        // Generate PDF bytes using SelectPdf
        private byte[] GeneratePaymentPdfBytes(PaymentHeader payment, User user)
        {
            try
            {
                // Create HTML template
                var htmlContent = GeneratePaymentHtml(payment, user);

                // Configure PDF converter
                var converter = new HtmlToPdf();

                // Set page options
                converter.Options.PdfPageSize = PdfPageSize.A4;
                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
                converter.Options.MarginTop = 20;
                converter.Options.MarginBottom = 20;
                converter.Options.MarginLeft = 20;
                converter.Options.MarginRight = 20;

                // Convert HTML to PDF
                var pdfDocument = converter.ConvertHtmlString(htmlContent);
                var pdfBytes = pdfDocument.Save();

                pdfDocument.Close();

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating payment PDF");
                throw;
            }
        }

        // Generate HTML content for PDF
        private string GeneratePaymentHtml(PaymentHeader payment, User user)
        {
            // Generate voucher details rows
            var voucherDetailsRows = string.Join("", payment.PaymentDetails.Select(detail =>
            {
                var voucherNumber = detail.VoucherHeader?.VoucherNumber ?? "N/A";
                var voucherDate = detail.VoucherHeader?.VoucherDate.ToString("dd/MM/yyyy") ?? "N/A";
                var voucherTotal = detail.VoucherHeader?.TotalAmount ?? 0;
                var amountPaid = detail.AmountPaid;

                return $@"
                    <tr>
                        <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; font-size: 14px;"">{voucherNumber}</td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; text-align: center; font-size: 14px;"">{voucherDate}</td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; text-align: right; font-size: 14px;"">${voucherTotal:N2}</td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; text-align: right; font-size: 14px; font-weight: bold; color: #1a237e;"">${amountPaid:N2}</td>
                    </tr>";
            }));

            // Convert amount to words
            var amountInWords = ConvertAmountToWords(payment.TotalAmount);

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Payment Receipt - {payment.PaymentNumber}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Arial, sans-serif;
            margin: 0;
            padding: 0;
            color: #2c3e50;
            line-height: 1.6;
            background: #fff;
        }}
        .container {{
            max-width: 800px;
            margin: 0 auto;
            padding: 30px;
        }}
        .header {{
            text-align: center;
            margin-bottom: 40px;
            border-bottom: 4px solid #1a237e;
            padding-bottom: 25px;
        }}
        .company-name {{
            font-size: 32px;
            font-weight: bold;
            color: #1a237e;
            margin-bottom: 8px;
        }}
        .document-title {{
            font-size: 24px;
            color: #0d47a1;
            font-weight: bold;
            margin-bottom: 15px;
        }}
        .payment-info {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 30px;
            background: #f8f9fa;
            padding: 20px;
            border-radius: 8px;
        }}
        .payment-details {{
            flex: 1;
        }}
        .payment-meta {{
            text-align: right;
            flex: 1;
        }}
        .info-row {{
            margin-bottom: 10px;
            font-size: 15px;
        }}
        .label {{
            font-weight: bold;
            color: #1a237e;
            display: inline-block;
            width: 140px;
        }}
        .amount-section {{
            background: #e3f2fd;
            border: 2px solid #1a237e;
            border-radius: 10px;
            padding: 20px;
            margin: 25px 0;
            text-align: center;
        }}
        .amount-number {{
            font-size: 28px;
            font-weight: bold;
            color: #1a237e;
            margin-bottom: 10px;
        }}
        .amount-words {{
            font-size: 16px;
            color: #0d47a1;
            font-style: italic;
            text-transform: capitalize;
        }}
        .vouchers-table {{
            width: 100%;
            border-collapse: collapse;
            margin: 25px 0;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-radius: 8px;
            overflow: hidden;
        }}
        .vouchers-table th {{
            background: linear-gradient(90deg, #1a237e, #0d47a1);
            color: white;
            padding: 15px 12px;
            text-align: left;
            font-weight: bold;
            text-transform: uppercase;
            font-size: 12px;
            letter-spacing: 0.5px;
        }}
        .vouchers-table th:nth-child(2) {{
            text-align: center;
        }}
        .vouchers-table th:nth-child(3),
        .vouchers-table th:nth-child(4) {{
            text-align: right;
        }}
        .total-row {{
            background: #f1f3f4;
            font-weight: bold;
        }}
        .total-row td {{
            padding: 15px 12px !important;
            border-top: 2px solid #1a237e;
        }}
        .description-box {{
            background: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 8px;
            padding: 20px;
            margin: 25px 0;
        }}
        .signature-section {{
            display: flex;
            justify-content: space-between;
            margin-top: 50px;
            padding-top: 30px;
            border-top: 1px solid #e0e0e0;
        }}
        .signature-box {{
            text-align: center;
            flex: 1;
            margin: 0 20px;
        }}
        .signature-line {{
            border-top: 2px solid #333;
            margin: 40px 0 15px 0;
        }}
        .signature-label {{
            font-size: 14px;
            color: #666;
            font-weight: bold;
        }}
        .footer {{
            margin-top: 40px;
            text-align: center;
            font-size: 12px;
            color: #666;
            border-top: 1px solid #e0e0e0;
            padding-top: 20px;
        }}
        .status-badge {{
            display: inline-block;
            padding: 5px 15px;
            border-radius: 20px;
            font-size: 14px;
            font-weight: bold;
            text-transform: uppercase;
        }}
        .status-paid {{
            background: #d4edda;
            color: #155724;
            border: 1px solid #c3e6cb;
        }}
        .status-draft {{
            background: #fff3cd;
            color: #856404;
            border: 1px solid #ffeaa7;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""company-name"">ClearBooks Financial Management</div>
            <div class=""document-title"">Payment Receipt</div>
        </div>

        <div class=""payment-info"">
            <div class=""payment-details"">
                <div class=""info-row"">
                    <span class=""label"">Payment Number:</span> {payment.PaymentNumber}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Payee Name:</span> {payment.PayeeName}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Payment Method:</span> {payment.PaymentMethod}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Reference Number:</span> {payment.ReferenceNumber ?? "N/A"}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Pay From Account:</span> {payment.ChartOfAccount?.AccountName ?? "N/A"} ({payment.ChartOfAccount?.AccountNumber ?? "N/A"})
                </div>
            </div>
            <div class=""payment-meta"">
                <div class=""info-row"">
                    <span class=""label"">Date:</span> {payment.PaymentDate:dd/MM/yyyy}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Status:</span> <span class=""status-badge status-{payment.Status.ToLower()}"">{payment.Status}</span>
                </div>
                <div class=""info-row"">
                    <span class=""label"">Generated:</span> {DateTime.Now:dd/MM/yyyy HH:mm}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Generated By:</span> {user?.Name ?? "System User"}
                </div>
            </div>
        </div>

        <div class=""amount-section"">
            <div class=""amount-number"">${payment.TotalAmount:N2}</div>
            <div class=""amount-words"">{amountInWords}</div>
        </div>

        {(!string.IsNullOrEmpty(payment.Description) ? $@"
        <div class=""description-box"">
            <div class=""info-row"">
                <span class=""label"">Description:</span>
            </div>
            <div style=""margin-top: 15px; font-size: 15px; line-height: 1.6;"">{payment.Description}</div>
        </div>" : "")}

        {(payment.PaymentDetails.Any() ? $@"
        <div style=""margin: 30px 0;"">
            <h3 style=""color: #1a237e; margin-bottom: 15px; font-size: 18px;"">Vouchers Paid</h3>
            <table class=""vouchers-table"">
                <thead>
                    <tr>
                        <th style=""width: 25%;"">Voucher Number</th>
                        <th style=""width: 20%; text-align: center;"">Date</th>
                        <th style=""width: 25%; text-align: right;"">Voucher Total</th>
                        <th style=""width: 30%; text-align: right;"">Amount Paid</th>
                    </tr>
                </thead>
                <tbody>
                    {voucherDetailsRows}
                    <tr class=""total-row"">
                        <td colspan=""3"" style=""text-align: right; font-weight: bold; color: #1a237e;"">TOTAL PAYMENT:</td>
                        <td style=""text-align: right; font-weight: bold; color: #1a237e; font-size: 16px;"">${payment.TotalAmount:N2}</td>
                    </tr>
                </tbody>
            </table>
        </div>" : "")}

        <div class=""signature-section"">
            <div class=""signature-box"">
                <div style=""font-weight: bold; margin-bottom: 10px;"">Paid By</div>
                <div style=""margin: 15px 0; font-size: 14px;"">{user?.Name ?? "System User"}</div>
                <div class=""signature-line""></div>
                <div class=""signature-label"">Signature & Date</div>
            </div>
            <div class=""signature-box"">
                <div style=""font-weight: bold; margin-bottom: 10px;"">Received By</div>
                <div style=""margin: 15px 0; font-size: 14px;"">{payment.PayeeName}</div>
                <div class=""signature-line""></div>
                <div class=""signature-label"">Signature & Date</div>
            </div>
            <div class=""signature-box"">
                <div style=""font-weight: bold; margin-bottom: 10px;"">Authorized By</div>
                <div style=""margin: 15px 0; font-size: 14px;"">&nbsp;</div>
                <div class=""signature-line""></div>
                <div class=""signature-label"">Signature & Date</div>
            </div>
        </div>

        <div class=""footer"">
            <p><strong>*** Official Payment Receipt ***</strong></p>
            <p>This document was generated by ClearBooks Financial Management System on {DateTime.Now:dd/MM/yyyy} at {DateTime.Now:HH:mm}</p>
            <p>Payment ID: {payment.PaymentId} | System Reference: PMT-{payment.PaymentId:D8}</p>
        </div>
    </div>
</body>
</html>";
        }



        // Helper method to convert amount to words
        private string ConvertAmountToWords(decimal amount)
        {
            try
            {
                if (amount == 0)
                    return "Zero Dollars Only";

                var dollars = (int)Math.Floor(amount);
                var cents = (int)Math.Round((amount - dollars) * 100);

                var dollarsInWords = NumberToWords(dollars);
                var result = $"{dollarsInWords} Dollar{(dollars != 1 ? "s" : "")}";

                if (cents > 0)
                {
                    var centsInWords = NumberToWords(cents);
                    result += $" and {centsInWords} Cent{(cents != 1 ? "s" : "")}";
                }

                result += " Only";
                return result;
            }
            catch
            {
                return $"${amount:N2}";
            }
        }

        private string NumberToWords(int number)
        {
            if (number == 0)
                return "Zero";

            if (number < 0)
                return "Minus " + NumberToWords(Math.Abs(number));

            string words = "";

            if (number / 1000000 > 0)
            {
                words += NumberToWords(number / 1000000) + " Million ";
                number %= 1000000;
            }

            if (number / 1000 > 0)
            {
                words += NumberToWords(number / 1000) + " Thousand ";
                number %= 1000;
            }

            if (number / 100 > 0)
            {
                words += NumberToWords(number / 100) + " Hundred ";
                number %= 100;
            }

            if (number > 0)
            {
                if (words != "")
                    words += "and ";

                var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
                var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

                if (number < 20)
                    words += unitsMap[number];
                else
                {
                    words += tensMap[number / 10];
                    if ((number % 10) > 0)
                        words += "-" + unitsMap[number % 10];
                }
            }

            return words.Trim();
        }

        // update voucher status as paid/pending
        // Helper method to update voucher payment status
        private async Task UpdateVoucherStatus(int voucherId, int userId)
        {
            try
            {
                var voucher = await _context.VoucherHeaders
                    .FirstOrDefaultAsync(v => v.VoucherId == voucherId && v.UserId == userId);

                if (voucher != null)
                {
                    // Calculate total payments for this voucher (only count "Paid" payments)
                    var totalPaid = await _context.PaymentDetails
                        .Include(pd => pd.Payment)
                        .Where(pd => pd.VoucherId == voucherId &&
                               pd.Payment.UserId == userId &&
                               pd.Payment.Status == "Paid")
                        .SumAsync(pd => pd.AmountPaid);

                    // Update voucher status based on payment amount
                    if (totalPaid >= voucher.TotalAmount)
                    {
                        voucher.Status = "Paid";
                    }
                    else if (totalPaid > 0)
                    {
                        voucher.Status = "Partially Paid";
                    }
                    else
                    {
                        voucher.Status = "Pending";
                    }

                    voucher.UpdatedAt = DateTime.Now;
                    _context.VoucherHeaders.Update(voucher);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating voucher status for ID {voucherId}");
                throw;
            }
        }
    }

    // DTOs
    public class PaymentDto
    {
        public int PaymentId { get; set; }
        public string PaymentNumber { get; set; }
        public string PaymentDate { get; set; }
        public string PayeeName { get; set; }
        public string PaymentMethod { get; set; }
        public int AccountId { get; set; }
        public decimal TotalAmount { get; set; }
        public string ReferenceNumber { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public List<PaymentDetailDto> PaymentDetails { get; set; }
    }

    public class PaymentDetailDto
    {
        public int VoucherId { get; set; }
        public decimal Amount { get; set; }
    }
}