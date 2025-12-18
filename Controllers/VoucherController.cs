using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClearBooksFYP.Models;
using SelectPdf;

namespace ClearBooksFYP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VouchersController : BaseController
    {
        private readonly ClearBooksDbContext _context;
        private readonly ILogger<VouchersController> _logger;

        public VouchersController(ClearBooksDbContext context, ILogger<VouchersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // loading all current vouchers 
        [HttpGet]
        public async Task<ActionResult<IEnumerable<VoucherHeader>>> GetVouchers()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var vouchers = await _context.VoucherHeaders
                    .Include(v => v.VoucherDetails)
                    .Where(v => v.UserId == userId)
                    .Select(v => new
                    {
                        v.VoucherId,
                        v.VoucherNumber,
                        v.VoucherDate,
                        v.TransactionType,
                        v.Description,
                        v.TotalAmount,
                        v.Status,
                        VoucherDetails = v.VoucherDetails.Select(vd => new
                        {
                            vd.DetailId,
                            vd.VoucherId,
                            vd.AccountId,
                            vd.IsDebit,
                            vd.Amount,
                            vd.Description,
                            AccountName = vd.ChartOfAccount.AccountName
                        })
                    })
                    .ToListAsync();

                return Ok(vouchers);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // getting a single voiceher
        [HttpGet("{id}")]
        public async Task<ActionResult<VoucherHeader>> GetVoucher(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var voucher = await _context.VoucherHeaders
                    .Include(v => v.VoucherDetails)
                    .ThenInclude(vd => vd.ChartOfAccount)
                    .Where(v => v.VoucherId == id && v.UserId == userId)
                    .Select(v => new
                    {
                        v.VoucherId,
                        v.VoucherNumber,
                        v.VoucherDate,
                        v.TransactionType,
                        v.Description,
                        v.TotalAmount,
                        v.Status,
                        VoucherDetails = v.VoucherDetails.Select(vd => new
                        {
                            vd.DetailId,
                            vd.VoucherId,
                            vd.AccountId,
                            vd.IsDebit,
                            vd.Amount,
                            vd.Description,
                            AccountName = vd.ChartOfAccount.AccountName
                        })
                    })
                    .FirstOrDefaultAsync();

                if (voucher == null)
                {
                    return NotFound(new { message = "Voucher not found" });
                }

                return Ok(voucher);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // creating a new voucher
        [HttpPost]
        public async Task<ActionResult<VoucherHeader>> CreateVoucher(VoucherHeader voucher)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { errors = ModelState });
                }

                var userId = await GetCurrentUserIdAsync(_context);

                //validate that all accounts belong to the current user
                var accountIds = voucher.VoucherDetails.Select(vd => vd.AccountId).Distinct().ToList();
                var userAccountIds = await _context.ChartOfAccounts
                    .Where(c => c.UserId == userId && accountIds.Contains(c.AccountId))
                    .Select(c => c.AccountId)
                    .ToListAsync();

                if (userAccountIds.Count != accountIds.Count)
                {
                    return BadRequest(new { message = "One or more accounts are not accessible to the current user" });
                }

                //check if voucher number already exists for this user
                var existingVoucher = await _context.VoucherHeaders
                    .FirstOrDefaultAsync(v => v.VoucherNumber == voucher.VoucherNumber && v.UserId == userId);

                if (existingVoucher != null)
                {
                    return BadRequest(new { message = "Voucher number already exists" });
                }

                //set timestamps and user ID
                var now = DateTime.Now;
                voucher.UserId = userId;
                voucher.CreatedAt = now;
                voucher.UpdatedAt = now;
                voucher.VoucherId = 0;

                foreach (var detail in voucher.VoucherDetails)
                {
                    detail.DetailId = 0;
                    detail.VoucherId = 0;
                    detail.CreatedAt = now;
                    detail.UpdatedAt = now;
                    detail.VoucherHeader = null;
                    detail.ChartOfAccount = null;
                }

                _context.VoucherHeaders.Add(voucher);
                await _context.SaveChangesAsync();

                //reload the voucher with all related data
                var savedVoucher = await _context.VoucherHeaders
                    .Include(v => v.VoucherDetails)
                    .ThenInclude(vd => vd.ChartOfAccount)
                    .FirstOrDefaultAsync(v => v.VoucherId == voucher.VoucherId);

                if (savedVoucher == null)
                {
                    return BadRequest(new { message = "Failed to save voucher" });
                }

                return Ok(new
                {
                    message = "Voucher saved successfully",
                    data = savedVoucher
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (DbUpdateException dbEx)
            {
                return BadRequest(new { message = "Database error: " + dbEx.InnerException?.Message ?? dbEx.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error: " + ex.Message });
            }
        }

        //upadting an existing voucher
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVoucher(int id, VoucherHeader updatedVoucher)
        {
            if (id != updatedVoucher.VoucherId)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var existingVoucher = await _context.VoucherHeaders
                    .Include(v => v.VoucherDetails)
                    .FirstOrDefaultAsync(v => v.VoucherId == id && v.UserId == userId);

                if (existingVoucher == null)
                {
                    return NotFound(new { message = "Voucher not found" });
                }

                //check if voucher is already paid - prevent modification of paid vouchers
                if (existingVoucher.Status == "Paid")
                {
                    return BadRequest(new { message = "Cannot modify a voucher that has been paid" });
                }

                //validate that all accounts belong to the current user
                var accountIds = updatedVoucher.VoucherDetails.Select(vd => vd.AccountId).Distinct().ToList();
                var userAccountIds = await _context.ChartOfAccounts
                    .Where(c => c.UserId == userId && accountIds.Contains(c.AccountId))
                    .Select(c => c.AccountId)
                    .ToListAsync();

                if (userAccountIds.Count != accountIds.Count)
                {
                    return BadRequest(new { message = "One or more accounts are not accessible to the current user" });
                }

                //check if voucher number already exists for this user (excluding current voucher)
                var duplicateVoucher = await _context.VoucherHeaders
                    .FirstOrDefaultAsync(v => v.VoucherNumber == updatedVoucher.VoucherNumber &&
                                            v.UserId == userId &&
                                            v.VoucherId != id);

                if (duplicateVoucher != null)
                {
                    return BadRequest(new { message = "Voucher number already exists" });
                }

                //update header fields
                existingVoucher.VoucherDate = updatedVoucher.VoucherDate;
                existingVoucher.TransactionType = updatedVoucher.TransactionType;
                existingVoucher.Description = updatedVoucher.Description;
                existingVoucher.TotalAmount = updatedVoucher.TotalAmount;
                existingVoucher.Status = updatedVoucher.Status;
                existingVoucher.UpdatedAt = DateTime.Now;

                //remove existing details
                _context.VoucherDetails.RemoveRange(existingVoucher.VoucherDetails);

                //add updated details
                foreach (var detail in updatedVoucher.VoucherDetails)
                {
                    detail.VoucherId = id;
                    detail.VoucherHeader = existingVoucher;
                    detail.CreatedAt = DateTime.Now;
                    detail.UpdatedAt = DateTime.Now;

                    if (detail.ChartOfAccount != null)
                    {
                        var accountId = detail.ChartOfAccount.AccountId;
                        detail.ChartOfAccount = null;
                        detail.AccountId = accountId;
                    }

                    _context.VoucherDetails.Add(detail);
                }

                await _context.SaveChangesAsync();

                //reload the voucher with related data
                var savedVoucher = await _context.VoucherHeaders
                    .Include(v => v.VoucherDetails)
                    .ThenInclude(vd => vd.ChartOfAccount)
                    .FirstOrDefaultAsync(v => v.VoucherId == id);

                return Ok(savedVoucher);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (DbUpdateException dbEx)
            {
                return BadRequest(new { message = "Error updating database. Please check your data and try again." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        //deleting voucher
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVoucher(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var voucher = await _context.VoucherHeaders
                    .Include(v => v.VoucherDetails)
                    .FirstOrDefaultAsync(v => v.VoucherId == id && v.UserId == userId);

                if (voucher == null)
                {
                    return NotFound(new { message = "Voucher not found" });
                }

                //check if voucher is used in any payments
                var isUsedInPayments = await _context.PaymentDetails
                    .Include(pd => pd.Payment)
                    .AnyAsync(pd => pd.VoucherId == id && pd.Payment.UserId == userId);

                if (isUsedInPayments)
                {
                    return BadRequest(new { message = "Cannot delete voucher that is used in payments" });
                }

                //remove all related details first
                _context.VoucherDetails.RemoveRange(voucher.VoucherDetails);

                //then remove the header
                _context.VoucherHeaders.Remove(voucher);

                await _context.SaveChangesAsync();

                return Ok(new { message = "Voucher deleted successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error deleting voucher: {ex.Message}" });
            }
        }

        //generate the next sequential voucher number 
        [HttpGet("GetNewVoucherNumber")]
        public async Task<ActionResult<string>> GetNewVoucherNumber()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var lastVoucher = await _context.VoucherHeaders
                    .Where(v => v.UserId == userId)
                    .OrderByDescending(v => v.VoucherNumber)
                    .FirstOrDefaultAsync();

                string newNumber = "V-00001";

                if (lastVoucher != null && lastVoucher.VoucherNumber.StartsWith("V-"))
                {
                    var lastNumberStr = lastVoucher.VoucherNumber.Substring(2);
                    if (int.TryParse(lastNumberStr, out int lastNumber))
                    {
                        newNumber = $"V-{(lastNumber + 1):D5}";
                    }
                }

                return Ok(new { voucherNumber = newNumber });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        //pending voucher status
        [HttpGet("Pending")]
        public async Task<ActionResult<IEnumerable<VoucherHeader>>> GetPendingVouchers([FromQuery] string search = "")
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var query = _context.VoucherHeaders
                    .Include(v => v.VoucherDetails)
                    .Where(v => v.UserId == userId && v.Status != "Paid");

                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(v =>
                        v.VoucherNumber.Contains(search) ||
                        v.Description.Contains(search));
                }

                var vouchers = await query
                    .OrderByDescending(v => v.VoucherDate)
                    .Take(50)
                    .ToListAsync();

                //for each voucher, calculate the remaining amount to be paid
                foreach (var voucher in vouchers)
                {
                    //calculate the total amount already paid
                    var paidAmount = await _context.PaymentDetails
                        .Include(pd => pd.Payment)
                        .Where(pd => pd.VoucherId == voucher.VoucherId &&
                               pd.Payment.UserId == userId &&
                               pd.Payment.Status == "Paid")
                        .SumAsync(pd => pd.AmountPaid);

                    //set the remaining amount as a property for the frontend
                    voucher.RemainingAmount = voucher.TotalAmount - paidAmount;
                }

                //filter out vouchers that are fully paid
                var availableVouchers = vouchers.Where(v => v.RemainingAmount > 0).ToList();

                return availableVouchers;
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error while retrieving pending vouchers");
            }
        }

        //pdf generation method
        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> GenerateVoucherPdf(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var voucher = await _context.VoucherHeaders
                    .Include(v => v.VoucherDetails)
                    .ThenInclude(vd => vd.ChartOfAccount)
                    .FirstOrDefaultAsync(v => v.VoucherId == id && v.UserId == userId);

                if (voucher == null)
                {
                    return NotFound(new { message = "Voucher not found" });
                }

                //gettting user information
                var user = await GetCurrentUserAsync(_context);

                var pdfBytes = GenerateVoucherPdfBytes(voucher, user);

                return File(pdfBytes, "application/pdf", $"Voucher-{voucher.VoucherNumber}.pdf");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating PDF for voucher ID {id}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private byte[] GenerateVoucherPdfBytes(VoucherHeader voucher, User user)
        {
            try
            {
                //calculate totals
                decimal totalDebit = voucher.VoucherDetails.Where(d => d.IsDebit).Sum(d => d.Amount);
                decimal totalCredit = voucher.VoucherDetails.Where(d => !d.IsDebit).Sum(d => d.Amount);
                bool isBalanced = Math.Abs(totalDebit - totalCredit) <= 0.01m;

                //create HTML template
                var htmlContent = GenerateVoucherHtml(voucher, user, totalDebit, totalCredit, isBalanced);

                //configure PDF converter
                var converter = new HtmlToPdf();

                //set page options
                converter.Options.PdfPageSize = PdfPageSize.A4;
                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
                converter.Options.MarginTop = 20;
                converter.Options.MarginBottom = 20;
                converter.Options.MarginLeft = 20;
                converter.Options.MarginRight = 20;

                //convert HTML to PDF
                var pdfDocument = converter.ConvertHtmlString(htmlContent);
                var pdfBytes = pdfDocument.Save();

                pdfDocument.Close();

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating voucher PDF");
                throw;
            }
        }

        private string GenerateVoucherHtml(VoucherHeader voucher, User user, decimal totalDebit, decimal totalCredit, bool isBalanced)
        {
            var detailsRows = string.Join("", voucher.VoucherDetails.OrderBy(d => d.DetailId).Select(detail =>
            {
                var accountName = detail.ChartOfAccount?.AccountName ?? "Unknown Account";
                var debitAmount = detail.IsDebit ? detail.Amount : 0;
                var creditAmount = !detail.IsDebit ? detail.Amount : 0;

                return $@"
                    <tr>
                        <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; font-size: 14px;"">{accountName}</td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; font-size: 14px;"">{detail.Description ?? ""}</td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; text-align: right; font-size: 14px;"">{(debitAmount > 0 ? $"${debitAmount:N2}" : "")}</td>
                        <td style=""padding: 12px; border-bottom: 1px solid #e0e0e0; text-align: right; font-size: 14px;"">{(creditAmount > 0 ? $"${creditAmount:N2}" : "")}</td>
                    </tr>";
            }));

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Journal Voucher - {voucher.VoucherNumber}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Arial, sans-serif;
            margin: 0;
            padding: 0;
            color: #2c3e50;
            line-height: 1.6;
        }}
        .container {{
            max-width: 800px;
            margin: 0 auto;
            padding: 20px;
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
            border-bottom: 3px solid #1a237e;
            padding-bottom: 20px;
        }}
        .company-name {{
            font-size: 28px;
            font-weight: bold;
            color: #1a237e;
            margin-bottom: 5px;
        }}
        .document-title {{
            font-size: 20px;
            color: #0d47a1;
            margin-bottom: 10px;
        }}
        .voucher-info {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 30px;
            background: #f8f9fa;
            padding: 15px;
            border-radius: 8px;
        }}
        .voucher-details {{
            flex: 1;
        }}
        .voucher-meta {{
            text-align: right;
            flex: 1;
        }}
        .info-row {{
            margin-bottom: 8px;
            font-size: 14px;
        }}
        .label {{
            font-weight: bold;
            color: #1a237e;
        }}
        .entries-table {{
            width: 100%;
            border-collapse: collapse;
            margin: 20px 0;
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
            border-radius: 8px;
            overflow: hidden;
        }}
        .entries-table th {{
            background: linear-gradient(90deg, #1a237e, #0d47a1);
            color: white;
            padding: 15px 12px;
            text-align: left;
            font-weight: bold;
            text-transform: uppercase;
            font-size: 12px;
            letter-spacing: 0.5px;
        }}
        .entries-table th:nth-child(3),
        .entries-table th:nth-child(4) {{
            text-align: right;
        }}
        .totals-row {{
            background: #f1f3f4;
            font-weight: bold;
        }}
        .totals-row td {{
            padding: 15px 12px !important;
            border-top: 2px solid #1a237e;
        }}
        .signature-section {{
            display: flex;
            justify-content: space-between;
            margin-top: 40px;
            padding-top: 20px;
        }}
        .signature-box {{
            text-align: center;
            flex: 1;
            margin: 0 20px;
        }}
        .signature-line {{
            border-top: 2px solid #333;
            margin: 30px 0 10px 0;
        }}
        .balance-warning {{
            background: #fff3cd;
            border: 2px solid #ffc107;
            color: #856404;
            padding: 15px;
            border-radius: 8px;
            margin: 20px 0;
            text-align: center;
            font-weight: bold;
        }}
        .description-box {{
            background: #f8f9fa;
            border: 1px solid #dee2e6;
            border-radius: 8px;
            padding: 15px;
            margin: 20px 0;
        }}
        .footer {{
            margin-top: 30px;
            text-align: center;
            font-size: 12px;
            color: #666;
            border-top: 1px solid #e0e0e0;
            padding-top: 15px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <div class=""company-name"">ClearBooks Financial Management</div>
            <div class=""document-title"">Journal Voucher</div>
        </div>

        <div class=""voucher-info"">
            <div class=""voucher-details"">
                <div class=""info-row"">
                    <span class=""label"">Voucher Number:</span> {voucher.VoucherNumber}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Transaction Type:</span> {voucher.TransactionType ?? "N/A"}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Status:</span> {voucher.Status}
                </div>
            </div>
            <div class=""voucher-meta"">
                <div class=""info-row"">
                    <span class=""label"">Date:</span> {voucher.VoucherDate:dd/MM/yyyy}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Total Amount:</span> ${voucher.TotalAmount:N2}
                </div>
                <div class=""info-row"">
                    <span class=""label"">Generated:</span> {DateTime.Now:dd/MM/yyyy HH:mm}
                </div>
            </div>
        </div>

        {(!string.IsNullOrEmpty(voucher.Description) ? $@"
        <div class=""description-box"">
            <div class=""info-row"">
                <span class=""label"">Description:</span>
            </div>
            <div style=""margin-top: 10px; font-size: 14px;"">{voucher.Description}</div>
        </div>" : "")}

        <table class=""entries-table"">
            <thead>
                <tr>
                    <th style=""width: 35%;"">Account</th>
                    <th style=""width: 30%;"">Description</th>
                    <th style=""width: 17.5%; text-align: right;"">Debit</th>
                    <th style=""width: 17.5%; text-align: right;"">Credit</th>
                </tr>
            </thead>
            <tbody>
                {detailsRows}
                <tr class=""totals-row"">
                    <td colspan=""2"" style=""text-align: right; font-weight: bold; color: #1a237e;"">TOTALS:</td>
                    <td style=""text-align: right; font-weight: bold; color: #1a237e;"">${totalDebit:N2}</td>
                    <td style=""text-align: right; font-weight: bold; color: #1a237e;"">${totalCredit:N2}</td>
                </tr>
            </tbody>
        </table>

        {(!isBalanced ? $@"
        <div class=""balance-warning"">
            ⚠️ WARNING: Debit and Credit totals do not balance! Difference: ${Math.Abs(totalDebit - totalCredit):N2}
        </div>" : "")}

        <div class=""signature-section"">
            <div class=""signature-box"">
                <div><strong>Prepared by:</strong></div>
                <div style=""margin: 10px 0; font-size: 14px;"">{user?.Name ?? "System User"}</div>
                <div class=""signature-line""></div>
                <div style=""font-size: 12px; color: #666;"">Signature & Date</div>
            </div>
            <div class=""signature-box"">
                <div><strong>Reviewed by:</strong></div>
                <div style=""margin: 10px 0; font-size: 14px;"">&nbsp;</div>
                <div class=""signature-line""></div>
                <div style=""font-size: 12px; color: #666;"">Signature & Date</div>
            </div>
            <div class=""signature-box"">
                <div><strong>Authorized by:</strong></div>
                <div style=""margin: 10px 0; font-size: 14px;"">&nbsp;</div>
                <div class=""signature-line""></div>
                <div style=""font-size: 12px; color: #666;"">Signature & Date</div>
            </div>
        </div>

        <div class=""footer"">
            <p>This document was generated by ClearBooks Financial Management System on {DateTime.Now:dd/MM/yyyy} at {DateTime.Now:HH:mm}</p>
            <p><strong>*** Official Journal Voucher ***</strong></p>
        </div>
    </div>
</body>
</html>";
        }
    }
}