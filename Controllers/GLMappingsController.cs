using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClearBooksFYP.Models;

namespace ClearBooksFYP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class GLMappingsController : BaseController
    {
        private readonly ClearBooksDbContext _context;

        public GLMappingsController(ClearBooksDbContext context)
        {
            _context = context;
        }

        // returns a list of gl mapping objects belonging to the current user
        // GET: api/GLMappings
        [HttpGet]
        public async Task<ActionResult<IEnumerable<GLMapping>>> GetGLMappings()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var mappings = await _context.GLMappings
                    .Where(m => m.UserId == userId)
                    .ToListAsync();
                return Ok(mappings);
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

        //gets a single mapping id belonging to the user
        // GET: api/GLMappings/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<GLMapping>> GetGLMapping(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var mapping = await _context.GLMappings
                    .FirstOrDefaultAsync(m => m.MappingId == id && m.UserId == userId);

                if (mapping == null)
                {
                    return NotFound(new { message = "Mapping not found" });
                }

                return mapping;
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

        // creating a new mapping
        // POST: api/GLMappings
        [HttpPost]
        public async Task<ActionResult<GLMapping>> AddGLMapping(GLMapping newMapping)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);

                // Validate that the accounts belong to the current user
                var debitAccountExists = await _context.ChartOfAccounts
                    .AnyAsync(c => c.AccountId == newMapping.DebitAccount && c.UserId == userId);

                var creditAccountExists = await _context.ChartOfAccounts
                    .AnyAsync(c => c.AccountId == newMapping.CreditAccount && c.UserId == userId);

                if (!debitAccountExists)
                {
                    return BadRequest(new { message = "Debit account not found or not accessible" });
                }

                if (!creditAccountExists)
                {
                    return BadRequest(new { message = "Credit account not found or not accessible" });
                }

                // Check if a mapping with the same transaction type already exists for this user
                var existingMapping = await _context.GLMappings
                    .FirstOrDefaultAsync(m => m.TransactionType == newMapping.TransactionType && m.UserId == userId);

                if (existingMapping != null)
                {
                    return BadRequest(new { message = "A mapping for this transaction type already exists" });
                }

                // Set user ID and timestamps
                newMapping.UserId = userId;
                newMapping.CreatedAt = DateTime.Now;
                newMapping.UpdatedAt = DateTime.Now;

                _context.GLMappings.Add(newMapping);
                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(GetGLMapping),
                    new { id = newMapping.MappingId },
                    newMapping
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

        // updating existing gl mapping
        // PUT: api/GLMappings/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGLMapping(int id, GLMapping updatedMapping)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var existingMapping = await _context.GLMappings
                    .FirstOrDefaultAsync(m => m.MappingId == id && m.UserId == userId);

                if (existingMapping == null)
                {
                    return NotFound(new { message = "Mapping not found" });
                }

                // Validate that the accounts belong to the current user
                var debitAccountExists = await _context.ChartOfAccounts
                    .AnyAsync(c => c.AccountId == updatedMapping.DebitAccount && c.UserId == userId);

                var creditAccountExists = await _context.ChartOfAccounts
                    .AnyAsync(c => c.AccountId == updatedMapping.CreditAccount && c.UserId == userId);

                if (!debitAccountExists)
                {
                    return BadRequest(new { message = "Debit account not found or not accessible" });
                }

                if (!creditAccountExists)
                {
                    return BadRequest(new { message = "Credit account not found or not accessible" });
                }

                // Check if a mapping with the same transaction type already exists for this user (excluding current mapping)
                var duplicateMapping = await _context.GLMappings
                    .FirstOrDefaultAsync(m => m.TransactionType == updatedMapping.TransactionType &&
                                            m.UserId == userId &&
                                            m.MappingId != id);

                if (duplicateMapping != null)
                {
                    return BadRequest(new { message = "A mapping for this transaction type already exists" });
                }

                // Update properties
                existingMapping.TransactionType = updatedMapping.TransactionType;
                existingMapping.DebitAccount = updatedMapping.DebitAccount;
                existingMapping.CreditAccount = updatedMapping.CreditAccount;
                existingMapping.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Ok(existingMapping);
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

        //deleting a gl mapping
        // DELETE: api/GLMappings/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGLMapping(int id)
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);
                var mapping = await _context.GLMappings
                    .FirstOrDefaultAsync(m => m.MappingId == id && m.UserId == userId);

                if (mapping == null)
                {
                    return NotFound(new { message = "Mapping not found" });
                }

                // Check if mapping is used in any vouchers
                var isUsedInVouchers = await _context.VoucherHeaders
                    .AnyAsync(v => v.TransactionType == mapping.TransactionType && v.UserId == userId);

                if (isUsedInVouchers)
                {
                    return BadRequest(new { message = "Cannot delete mapping that is used in vouchers" });
                }

                _context.GLMappings.Remove(mapping);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Mapping deleted successfully" });
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