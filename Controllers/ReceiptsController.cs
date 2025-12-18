using ClearBooksFYP.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SelectPdf;

namespace ClearBooksFYP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReceiptsController : BaseController
    {
        private readonly ClearBooksDbContext _context;
        private readonly ILogger<ReceiptsController> _logger;

        public ReceiptsController(ClearBooksDbContext context, ILogger<ReceiptsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        //get all the receipts

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Receipt>>> GetReceipts()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var receipts = await _context.Receipts
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.Date)
                    .ToListAsync();
                return Ok(receipts);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        //get single recepts
        [HttpGet("{id}")]
        public async Task<ActionResult<Receipt>> GetReceipt(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var receipt = await _context.Receipts
                    .FirstOrDefaultAsync(r => r.ReceiptId == id && r.UserId == userId);

                if (receipt == null)
                {
                    return NotFound(new { message = "Receipt not found" });
                }

                return receipt;
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        //creating new receipts
        [HttpPost]
        public async Task<ActionResult<Receipt>> CreateReceipt(Receipt receipt)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);

                // Validate essential fields
                if (string.IsNullOrEmpty(receipt.PayerName) ||
                    receipt.Amount <= 0 ||
                    string.IsNullOrEmpty(receipt.Currency) ||
                    string.IsNullOrEmpty(receipt.PaymentMethod) ||
                    string.IsNullOrEmpty(receipt.Description))
                {
                    return BadRequest(new { message = "All required fields must be provided and amount must be greater than 0." });
                }

                //check if receipt number already exists for thsi user
                if (!string.IsNullOrEmpty(receipt.ReceiptNumber))
                {
                    var existingReceipt = await _context.Receipts
                        .FirstOrDefaultAsync(r => r.ReceiptNumber == receipt.ReceiptNumber && r.UserId == userId);

                    if (existingReceipt != null)
                    {
                        return BadRequest(new { message = "Receipt number already exists" });
                    }
                }
                else
                {
                    //autogenerate receipt number if not provided
                    receipt.ReceiptNumber = await GenerateReceiptNumberAsync(userId);
                }

                //set the user id and timestamps
                receipt.UserId = userId;
                receipt.CreatedAt = DateTime.Now;
                receipt.UpdatedAt = DateTime.Now;

                _context.Receipts.Add(receipt);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetReceipt), new { id = receipt.ReceiptId }, receipt);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        //method to generate uniqur4 receipt number
        private async Task<string> GenerateReceiptNumberAsync(int userId)
        {
            //getting current yeatr and month
            string prefix = $"REC-{DateTime.Now:yyyy-MM}-";

            try
            {
                //find highest existing previous user number
                var latestReceipt = await _context.Receipts
                    .Where(r => r.UserId == userId && r.ReceiptNumber.StartsWith(prefix))
                    .OrderByDescending(r => r.ReceiptNumber)
                    .FirstOrDefaultAsync();

                int nextNumber = 1;

                if (latestReceipt != null)
                {
                    //extract the numebr part
                    string existingNumberStr = latestReceipt.ReceiptNumber.Substring(prefix.Length);
                    if (int.TryParse(existingNumberStr, out int existingNumber))
                    {
                        nextNumber = existingNumber + 1;
                    }
                }

                //need to be 4 digitts so zeroes at start
                return $"{prefix}{nextNumber:D4}";
            }
            catch
            {
                //error handle
                return $"{prefix}{Guid.NewGuid().ToString().Substring(0, 8)}";
            }
        }

        // upadting a receipt
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReceipt(int id, Receipt receipt)
        {
            try
            {
                if (id != receipt.ReceiptId)
                {
                    return BadRequest(new { message = "Receipt ID mismatch" });
                }

                var userId = await GetCurrentUserIdAsync(_context);

                //validating essential fields
                if (string.IsNullOrEmpty(receipt.PayerName) ||
                    receipt.Amount <= 0 ||
                    string.IsNullOrEmpty(receipt.Currency) ||
                    string.IsNullOrEmpty(receipt.PaymentMethod) ||
                    string.IsNullOrEmpty(receipt.Description))
                {
                    return BadRequest(new { message = "All required fields must be provided and amount must be greater than 0." });
                }

                var existingReceipt = await _context.Receipts
                    .FirstOrDefaultAsync(r => r.ReceiptId == id && r.UserId == userId);

                if (existingReceipt == null)
                {
                    return NotFound(new { message = "Receipt not found" });
                }

                //check if receipt number already exists for this user (excluding current receipt)
                if (!string.IsNullOrEmpty(receipt.ReceiptNumber))
                {
                    var duplicateReceipt = await _context.Receipts
                        .FirstOrDefaultAsync(r => r.ReceiptNumber == receipt.ReceiptNumber &&
                                                r.UserId == userId &&
                                                r.ReceiptId != id);

                    if (duplicateReceipt != null)
                    {
                        return BadRequest(new { message = "Receipt number already exists" });
                    }
                }

                //update receipt properties
                existingReceipt.ReceiptNumber = receipt.ReceiptNumber ?? existingReceipt.ReceiptNumber;
                existingReceipt.PayerName = receipt.PayerName;
                existingReceipt.Amount = receipt.Amount;
                existingReceipt.Currency = receipt.Currency;
                existingReceipt.Date = receipt.Date;
                existingReceipt.PaymentMethod = receipt.PaymentMethod;
                existingReceipt.Description = receipt.Description;
                existingReceipt.UpdatedAt = DateTime.Now;

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReceiptExists(id, userId))
                    {
                        return NotFound(new { message = "Receipt not found" });
                    }
                    else
                    {
                        throw;
                    }
                }

                return Ok(existingReceipt);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // deleting a receipt
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReceipt(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var receipt = await _context.Receipts
                    .FirstOrDefaultAsync(r => r.ReceiptId == id && r.UserId == userId);

                if (receipt == null)
                {
                    return NotFound(new { message = "Receipt not found" });
                }

                _context.Receipts.Remove(receipt);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Receipt deleted successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        //getting a new receipt number
        [HttpGet("GetNewReceiptNumber")]
        public async Task<ActionResult<object>> GetNewReceiptNumber()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var receiptNumber = await GenerateReceiptNumberAsync(userId);
                return Ok(new { receiptNumber });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        //pdf generation
        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> GenerateReceiptPdf(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var receipt = await _context.Receipts
                    .FirstOrDefaultAsync(r => r.ReceiptId == id && r.UserId == userId);

                if (receipt == null)
                {
                    return NotFound(new { message = "Receipt not found" });
                }

                //get user information
                var user = await GetCurrentUserAsync(_context);

                var pdfBytes = GenerateReceiptPdfBytes(receipt, user);

                return File(pdfBytes, "application/pdf", $"Receipt-{receipt.ReceiptNumber}.pdf");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access attempt");
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating PDF for receipt ID {id}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private byte[] GenerateReceiptPdfBytes(Receipt receipt, User user)
        {
            try
            {
                //convert amount to words
                var amountInWords = ConvertAmountToWords(receipt.Amount, receipt.Currency);

                //create htmk template
                var htmlContent = GenerateReceiptHtml(receipt, user, amountInWords);

                // Configure PDF converter
                var converter = new HtmlToPdf();

                //set page options
                converter.Options.PdfPageSize = PdfPageSize.A4;
                converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;
                converter.Options.MarginTop = 20;
                converter.Options.MarginBottom = 20;
                converter.Options.MarginLeft = 20;
                converter.Options.MarginRight = 20;

                //convert Hhtml to PDF
                var pdfDocument = converter.ConvertHtmlString(htmlContent);
                var pdfBytes = pdfDocument.Save();

                pdfDocument.Close();

                return pdfBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating receipt PDF");
                throw;
            }
        }

        private string GenerateReceiptHtml(Receipt receipt, User user, string amountInWords)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Receipt - {receipt.ReceiptNumber}</title>
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
            max-width: 700px;
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
        .receipt-meta {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 15px;
            font-size: 14px;
        }}
        .receipt-number {{
            font-size: 18px;
            font-weight: bold;
            color: #1a237e;
        }}
        .amount-section {{
            background: linear-gradient(135deg, #f0f4ff, #e3f2fd);
            border: 3px solid #1a237e;
            border-radius: 15px;
            padding: 25px;
            text-align: center;
            margin: 30px 0;
            box-shadow: 0 4px 12px rgba(26, 35, 126, 0.1);
        }}
        .amount-label {{
            font-size: 16px;
            font-weight: bold;
            color: #1a237e;
            margin-bottom: 10px;
            text-transform: uppercase;
            letter-spacing: 1px;
        }}
        .amount-value {{
            font-size: 36px;
            font-weight: bold;
            color: #0d47a1;
            margin: 15px 0;
            text-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .amount-words {{
            font-size: 14px;
            color: #555;
            font-style: italic;
            margin-top: 10px;
            padding: 10px;
            background: rgba(255,255,255,0.7);
            border-radius: 8px;
        }}
        .details-section {{
            margin: 30px 0;
        }}
        .details-table {{
            width: 100%;
            border-collapse: collapse;
            margin: 20px 0;
        }}
        .details-table td {{
            padding: 15px 20px;
            border-bottom: 2px solid #f0f0f0;
            font-size: 16px;
        }}
        .details-table td:first-child {{
            font-weight: bold;
            color: #1a237e;
            width: 30%;
            background: #f8f9fa;
        }}
        .description-section {{
            background: #f8f9fa;
            border: 2px solid #e9ecef;
            border-radius: 10px;
            padding: 20px;
            margin: 25px 0;
        }}
        .description-title {{
            font-weight: bold;
            color: #1a237e;
            margin-bottom: 10px;
            font-size: 16px;
        }}
        .description-text {{
            font-size: 15px;
            line-height: 1.8;
            color: #333;
        }}
        .signature-section {{
            display: flex;
            justify-content: space-between;
            margin-top: 50px;
            padding-top: 30px;
            border-top: 2px solid #e9ecef;
        }}
        .signature-box {{
            text-align: center;
            flex: 1;
            margin: 0 15px;
        }}
        .signature-title {{
            font-weight: bold;
            color: #1a237e;
            margin-bottom: 15px;
            font-size: 14px;
        }}
        .signature-name {{
            font-size: 16px;
            margin: 20px 0;
            min-height: 25px;
        }}
        .signature-line {{
            border-top: 2px solid #333;
            margin: 25px 0 10px 0;
        }}
        .signature-label {{
            font-size: 12px;
            color: #666;
        }}
        .footer {{
            margin-top: 40px;
            text-align: center;
            font-size: 12px;
            color: #666;
            border-top: 1px solid #e0e0e0;
            padding-top: 20px;
        }}
        .official-mark {{
            background: linear-gradient(90deg, #1a237e, #0d47a1);
            color: white;
            padding: 8px 20px;
            border-radius: 25px;
            font-weight: bold;
            display: inline-block;
            margin-top: 15px;
            font-size: 12px;
            text-transform: uppercase;
            letter-spacing: 1px;
        }}
        .watermark {{
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%) rotate(-45deg);
            font-size: 60px;
            color: rgba(26, 35, 126, 0.05);
            font-weight: bold;
            z-index: -1;
            pointer-events: none;
        }}
    </style>
</head>
<body>
    <div class=""watermark"">OFFICIAL RECEIPT</div>
    <div class=""container"">
        <div class=""header"">
            <div class=""company-name"">ClearBooks Financial Management</div>
            <div class=""document-title"">Official Receipt</div>
            <div class=""receipt-meta"">
                <div class=""receipt-number"">Receipt #: {receipt.ReceiptNumber}</div>
                <div>Date: {receipt.Date:dd MMMM yyyy}</div>
            </div>
        </div>

        <div class=""amount-section"">
            <div class=""amount-label"">Amount Received</div>
            <div class=""amount-value"">{receipt.Currency} {receipt.Amount:N2}</div>
            <div class=""amount-words"">{amountInWords}</div>
        </div>

        <div class=""details-section"">
            <table class=""details-table"">
                <tr>
                    <td>Received From:</td>
                    <td>{receipt.PayerName}</td>
                </tr>
                <tr>
                    <td>Payment Method:</td>
                    <td>{receipt.PaymentMethod}</td>
                </tr>
                <tr>
                    <td>Date Received:</td>
                    <td>{receipt.Date:dddd, dd MMMM yyyy}</td>
                </tr>
                <tr>
                    <td>Currency:</td>
                    <td>{receipt.Currency}</td>
                </tr>
            </table>
        </div>

        <div class=""description-section"">
            <div class=""description-title"">Description / Purpose:</div>
            <div class=""description-text"">{receipt.Description}</div>
        </div>

        <div class=""signature-section"">
            <div class=""signature-box"">
                <div class=""signature-title"">Received by:</div>
                <div class=""signature-name"">{user?.Name ?? "Authorized Personnel"}</div>
                <div class=""signature-line""></div>
                <div class=""signature-label"">Signature & Date</div>
            </div>
            <div class=""signature-box"">
                <div class=""signature-title"">Acknowledged by:</div>
                <div class=""signature-name"">{receipt.PayerName}</div>
                <div class=""signature-line""></div>
                <div class=""signature-label"">Signature & Date</div>
            </div>
        </div>

        <div class=""footer"">
            <p>This receipt was generated by ClearBooks Financial Management System</p>
            <p>Generated on {DateTime.Now:dd MMMM yyyy} at {DateTime.Now:HH:mm}</p>
            <div class=""official-mark"">*** Official Receipt ***</div>
        </div>
    </div>
</body>
</html>";
        }

        private string ConvertAmountToWords(decimal amount, string currency)
        {
            //basic amount to words conversion
            if (amount == 0)
                return $"Zero {currency} Only";

            var wholePart = (long)amount;
            var decimalPart = (int)Math.Round((amount - wholePart) * 100);

            var wholeWords = ConvertNumberToWords(wholePart);
            var result = $"{wholeWords} {currency}";

            if (decimalPart > 0)
            {
                var decimalWords = ConvertNumberToWords(decimalPart);
                result += $" and {decimalWords} Cents";
            }

            return result + " Only";
        }

        private string ConvertNumberToWords(long number)
        {
            if (number == 0) return "Zero";

            var ones = new[] { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
            var teens = new[] { "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            var tens = new[] { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
            var thousands = new[] { "", "Thousand", "Million", "Billion" };

            string ConvertHundreds(long num)
            {
                var result = "";

                if (num >= 100)
                {
                    result += ones[num / 100] + " Hundred ";
                    num %= 100;
                }

                if (num >= 20)
                {
                    result += tens[num / 10] + " ";
                    num %= 10;
                }
                else if (num >= 10)
                {
                    result += teens[num - 10] + " ";
                    num = 0;
                }

                if (num > 0)
                {
                    result += ones[num] + " ";
                }

                return result;
            }

            var result = "";
            var groupIndex = 0;

            while (number > 0)
            {
                var group = number % 1000;
                if (group != 0)
                {
                    var groupWords = ConvertHundreds(group);
                    if (groupIndex > 0)
                    {
                        groupWords += thousands[groupIndex] + " ";
                    }
                    result = groupWords + result;
                }
                number /= 1000;
                groupIndex++;
            }

            return result.Trim();
        }

        private bool ReceiptExists(int id, int userId)
        {
            return _context.Receipts.Any(e => e.ReceiptId == id && e.UserId == userId);
        }
    }
}