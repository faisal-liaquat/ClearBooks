using ClearBooksFYP.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClearBooksFYP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChartOfAccountsController : BaseController
    {
        private readonly ClearBooksDbContext _context;

        public ChartOfAccountsController(ClearBooksDbContext context)
        {
            _context = context;
        }

        //getting all accoiunts
        // GET: api/ChartOfAccounts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ChartOfAccount>>> GetChartOfAccounts()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var chartOfAccounts = await _context.ChartOfAccounts
                    .Where(c => c.UserId == userId)
                    .ToListAsync();
                return Ok(chartOfAccounts);
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


        //ensures a user can only access their own account
        // GET: api/ChartOfAccounts/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<ChartOfAccount>> GetChartOfAccount(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var account = await _context.ChartOfAccounts
                    .FirstOrDefaultAsync(c => c.AccountId == id && c.UserId == userId);

                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                return account;
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

        //creating new account
        // POST: api/ChartOfAccounts
        [HttpPost]
        public async Task<ActionResult<ChartOfAccount>> AddChartOfAccount(ChartOfAccount newAccount)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);

                // Check if account number already exists for this user
                var existingAccount = await _context.ChartOfAccounts
                    .FirstOrDefaultAsync(c => c.AccountNumber == newAccount.AccountNumber && c.UserId == userId);

                if (existingAccount != null)
                {
                    return BadRequest(new { message = "Account number already exists" });
                }

                // Set user ID and timestamps
                newAccount.UserId = userId;
                newAccount.CreatedAt = DateTime.Now;
                newAccount.UpdatedAt = DateTime.Now;

                _context.ChartOfAccounts.Add(newAccount);
                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(GetChartOfAccount),
                    new { id = newAccount.AccountId },
                    newAccount
                );
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // upadating an existing account
        // PUT: api/ChartOfAccounts/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateChartOfAccount(int id, ChartOfAccount updatedAccount)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var existingAccount = await _context.ChartOfAccounts
                    .FirstOrDefaultAsync(c => c.AccountId == id && c.UserId == userId);

                if (existingAccount == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                // Check if account number already exists for this user (excluding current account)
                var duplicateAccount = await _context.ChartOfAccounts
                    .FirstOrDefaultAsync(c => c.AccountNumber == updatedAccount.AccountNumber &&
                                            c.UserId == userId &&
                                            c.AccountId != id);

                if (duplicateAccount != null)
                {
                    return BadRequest(new { message = "Account number already exists" });
                }

                // Update properties
                existingAccount.AccountNumber = updatedAccount.AccountNumber;
                existingAccount.AccountName = updatedAccount.AccountName;
                existingAccount.AccountType = updatedAccount.AccountType;
                existingAccount.Subaccount = updatedAccount.Subaccount;
                existingAccount.ParentAccount = updatedAccount.ParentAccount;
                existingAccount.Description = updatedAccount.Description;
                existingAccount.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(existingAccount);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // deleting acount 
        // DELETE: api/ChartOfAccounts/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteChartOfAccount(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var account = await _context.ChartOfAccounts
                    .FirstOrDefaultAsync(c => c.AccountId == id && c.UserId == userId);

                if (account == null)
                {
                    return NotFound(new { message = "Account not found" });
                }

                // Check if this account is a parent account
                var hasChildren = await _context.ChartOfAccounts
                    .AnyAsync(a => a.ParentAccount == id && a.UserId == userId);

                if (hasChildren)
                {
                    return BadRequest(new { message = "Cannot delete account with child accounts" });
                }

                // Check if account is used in GL mappings
                var isUsedInMappings = await _context.GLMappings
                    .AnyAsync(m => (m.DebitAccount == id || m.CreditAccount == id) && m.UserId == userId);

                if (isUsedInMappings)
                {
                    return BadRequest(new { message = "Cannot delete account that is used in GL mappings" });
                }

                // Check if account is used in voucher details
                var isUsedInVouchers = await _context.VoucherDetails
                    .Include(vd => vd.VoucherHeader)
                    .AnyAsync(vd => vd.AccountId == id && vd.VoucherHeader.UserId == userId);

                if (isUsedInVouchers)
                {
                    return BadRequest(new { message = "Cannot delete account that is used in vouchers" });
                }

                // Check if account is used in payments
                var isUsedInPayments = await _context.PaymentHeaders
                    .AnyAsync(p => p.AccountId == id && p.UserId == userId);

                if (isUsedInPayments)
                {
                    return BadRequest(new { message = "Cannot delete account that is used in payments" });
                }

                _context.ChartOfAccounts.Remove(account);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Account deleted successfully" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
            }
        }
    }
}