using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClearBooksFYP.Models;

namespace ClearBooksFYP.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : BaseController
    {
        private readonly ClearBooksDbContext _context;

        public SearchController(ClearBooksDbContext context)
        {
            _context = context;
        }

        //dropdown filter values
        [HttpGet("GetFilterValues")]
        public async Task<ActionResult<IEnumerable<string>>> GetFilterValues(string entityType, string filterType)
        {
            try
            {
                if (string.IsNullOrEmpty(entityType) || string.IsNullOrEmpty(filterType))
                {
                    return BadRequest("Entity type and filter type are required");
                }

                var userId = await GetCurrentUserIdAsync(_context);
                List<string> values = new List<string>();

                switch (entityType.ToLower())
                {
                    case "chartofaccount":
                        if (filterType == "accountNumber")
                        {
                            values = await _context.ChartOfAccounts
                                .Where(a => a.UserId == userId)
                                .Select(a => a.AccountNumber)
                                .Distinct()
                                .OrderBy(a => a)
                                .ToListAsync();
                        }
                        else if (filterType == "accountType")
                        {
                            values = await _context.ChartOfAccounts
                                .Where(a => a.UserId == userId)
                                .Select(a => a.AccountType)
                                .Distinct()
                                .OrderBy(a => a)
                                .ToListAsync();
                        }
                        break;

                    case "glmapping":
                        if (filterType == "mappingId")
                        {
                            values = await _context.GLMappings
                                .Where(m => m.UserId == userId)
                                .Select(m => m.MappingId.ToString())
                                .Distinct()
                                .OrderBy(m => m)
                                .ToListAsync();
                        }
                        else if (filterType == "transactionType")
                        {
                            values = await _context.GLMappings
                                .Where(m => m.UserId == userId)
                                .Select(m => m.TransactionType)
                                .Distinct()
                                .OrderBy(m => m)
                                .ToListAsync();
                        }
                        break;

                    case "voucher":
                        if (filterType == "voucherNumber")
                        {
                            values = await _context.VoucherHeaders
                                .Where(v => v.UserId == userId)
                                .Select(v => v.VoucherNumber)
                                .Distinct()
                                .OrderBy(v => v)
                                .ToListAsync();
                        }
                        else if (filterType == "transactionType")
                        {
                            values = await _context.VoucherHeaders
                                .Where(v => v.UserId == userId)
                                .Select(v => v.TransactionType)
                                .Distinct()
                                .OrderBy(v => v)
                                .ToListAsync();
                        }
                        else if (filterType == "status")
                        {
                            values = await _context.VoucherHeaders
                                .Where(v => v.UserId == userId)
                                .Select(v => v.Status)
                                .Distinct()
                                .OrderBy(v => v)
                                .ToListAsync();
                        }
                        break;

                    case "payment":
                        if (filterType == "payeeName")
                        {
                            values = await _context.PaymentHeaders
                                .Where(p => p.UserId == userId)
                                .Select(p => p.PayeeName)
                                .Distinct()
                                .OrderBy(p => p)
                                .ToListAsync();
                        }
                        else if (filterType == "paymentMethod")
                        {
                            values = await _context.PaymentHeaders
                                .Where(p => p.UserId == userId)
                                .Select(p => p.PaymentMethod)
                                .Distinct()
                                .OrderBy(p => p)
                                .ToListAsync();
                        }
                        else if (filterType == "status")
                        {
                            values = await _context.PaymentHeaders
                                .Where(p => p.UserId == userId)
                                .Select(p => p.Status)
                                .Distinct()
                                .OrderBy(p => p)
                                .ToListAsync();
                        }
                        break;

                    case "receipt":
                        if (filterType == "payerName")
                        {
                            values = await _context.Receipts
                                .Where(r => r.UserId == userId)
                                .Select(r => r.PayerName)
                                .Distinct()
                                .OrderBy(r => r)
                                .ToListAsync();
                        }
                        else if (filterType == "receiptNumber")
                        {
                            values = await _context.Receipts
                                .Where(r => r.UserId == userId)
                                .Select(r => r.ReceiptNumber)
                                .Distinct()
                                .OrderBy(r => r)
                                .ToListAsync();
                        }
                        else if (filterType == "paymentMethod")
                        {
                            values = await _context.Receipts
                                .Where(r => r.UserId == userId)
                                .Select(r => r.PaymentMethod)
                                .Distinct()
                                .OrderBy(r => r)
                                .ToListAsync();
                        }
                        break;

                    default:
                        return BadRequest($"Unknown entity type: {entityType}");
                }

                return Ok(values);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        //getting search entity types
        [HttpGet("SearchEntities")]
        public async Task<ActionResult<SearchResult>> SearchEntities(
            string entityType,
            string filterType,
            string filterValue,
            int page = 1,
            int pageSize = 10)
        {
            try
            {
                if (string.IsNullOrEmpty(entityType))
                {
                    return BadRequest("Entity type is required");
                }

                var userId = await GetCurrentUserIdAsync(_context);

                //pagination parameters
                if (page < 1)
                {
                    page = 1;
                }

                if (pageSize < 1 || pageSize > 100)
                {
                    pageSize = 10;
                }

                var searchResult = new SearchResult
                {
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalItems = 0,
                    TotalPages = 0,
                    Items = new List<object>()
                };

                switch (entityType.ToLower())
                {
                    case "chartofaccount":
                        var coaQuery = _context.ChartOfAccounts
                            .Where(a => a.UserId == userId)
                            .AsQueryable();

                        //account filters
                        if (!string.IsNullOrEmpty(filterType) && !string.IsNullOrEmpty(filterValue))
                        {
                            switch (filterType)
                            {
                                case "accountNumber":
                                    coaQuery = coaQuery.Where(a => a.AccountNumber == filterValue);
                                    break;
                                case "accountType":
                                    coaQuery = coaQuery.Where(a => a.AccountType == filterValue);
                                    break;
                            }
                        }

                        //get total count
                        searchResult.TotalItems = await coaQuery.CountAsync();
                        searchResult.TotalPages = (int)Math.Ceiling((double)searchResult.TotalItems / pageSize);

                        //apply pagination
                        var accounts = await coaQuery
                            .OrderBy(a => a.AccountNumber)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

                        searchResult.Items = accounts.Cast<object>().ToList();
                        break;

                    case "glmapping":
                        var glQuery = _context.GLMappings
                            .Where(m => m.UserId == userId)
                            .AsQueryable();

                        //gl mapping filters
                        if (!string.IsNullOrEmpty(filterType) && !string.IsNullOrEmpty(filterValue))
                        {
                            switch (filterType)
                            {
                                case "mappingId":
                                    if (int.TryParse(filterValue, out int mappingId))
                                    {
                                        glQuery = glQuery.Where(m => m.MappingId == mappingId);
                                    }
                                    break;
                                case "transactionType":
                                    glQuery = glQuery.Where(m => m.TransactionType == filterValue);
                                    break;
                            }
                        }

                        //get total count
                        searchResult.TotalItems = await glQuery.CountAsync();
                        searchResult.TotalPages = (int)Math.Ceiling((double)searchResult.TotalItems / pageSize);

                        //apply pagination
                        var mappings = await glQuery
                            .OrderBy(m => m.MappingId)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

                        searchResult.Items = mappings.Cast<object>().ToList();
                        break;

                    case "voucher":
                        var voucherQuery = _context.VoucherHeaders
                            .Where(v => v.UserId == userId)
                            .AsQueryable();

                        //voucher filters
                        if (!string.IsNullOrEmpty(filterType) && !string.IsNullOrEmpty(filterValue))
                        {
                            switch (filterType)
                            {
                                case "voucherNumber":
                                    voucherQuery = voucherQuery.Where(v => v.VoucherNumber == filterValue);
                                    break;
                                case "transactionType":
                                    voucherQuery = voucherQuery.Where(v => v.TransactionType == filterValue);
                                    break;
                                case "status":
                                    voucherQuery = voucherQuery.Where(v => v.Status == filterValue);
                                    break;
                            }
                        }

                        //get total count
                        searchResult.TotalItems = await voucherQuery.CountAsync();
                        searchResult.TotalPages = (int)Math.Ceiling((double)searchResult.TotalItems / pageSize);

                        //apply pagination
                        var vouchers = await voucherQuery
                            .OrderByDescending(v => v.VoucherDate)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

                        searchResult.Items = vouchers.Cast<object>().ToList();
                        break;

                    case "payment":
                        var paymentQuery = _context.PaymentHeaders
                            .Where(p => p.UserId == userId)
                            .AsQueryable();

                        //payment filters
                        if (!string.IsNullOrEmpty(filterType) && !string.IsNullOrEmpty(filterValue))
                        {
                            switch (filterType)
                            {
                                case "payeeName":
                                    paymentQuery = paymentQuery.Where(p => p.PayeeName == filterValue);
                                    break;
                                case "paymentMethod":
                                    paymentQuery = paymentQuery.Where(p => p.PaymentMethod == filterValue);
                                    break;
                                case "status":
                                    paymentQuery = paymentQuery.Where(p => p.Status == filterValue);
                                    break;
                            }
                        }

                        //get total count
                        searchResult.TotalItems = await paymentQuery.CountAsync();
                        searchResult.TotalPages = (int)Math.Ceiling((double)searchResult.TotalItems / pageSize);

                        //apply pagination
                        var payments = await paymentQuery
                            .OrderByDescending(p => p.PaymentDate)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

                        searchResult.Items = payments.Cast<object>().ToList();
                        break;

                    case "receipt":
                        var receiptQuery = _context.Receipts
                            .Where(r => r.UserId == userId)
                            .AsQueryable();

                        //receipt filters
                        if (!string.IsNullOrEmpty(filterType) && !string.IsNullOrEmpty(filterValue))
                        {
                            switch (filterType)
                            {
                                case "payerName":
                                    receiptQuery = receiptQuery.Where(r => r.PayerName == filterValue);
                                    break;
                                case "receiptNumber":
                                    receiptQuery = receiptQuery.Where(r => r.ReceiptNumber == filterValue);
                                    break;
                                case "paymentMethod":
                                    receiptQuery = receiptQuery.Where(r => r.PaymentMethod == filterValue);
                                    break;
                            }
                        }

                        //get total count
                        searchResult.TotalItems = await receiptQuery.CountAsync();
                        searchResult.TotalPages = (int)Math.Ceiling((double)searchResult.TotalItems / pageSize);

                        //apply pagination
                        var receipts = await receiptQuery
                            .OrderByDescending(r => r.Date)
                            .Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToListAsync();

                        searchResult.Items = receipts.Cast<object>().ToList();
                        break;

                    default:
                        return BadRequest($"Unknown entity type: {entityType}");
                }

                return Ok(searchResult);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        //global search across all entities
        [HttpGet("GlobalSearch")]
        public async Task<ActionResult<GlobalSearchResult>> GlobalSearch(string searchTerm, int page = 1, int pageSize = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    return BadRequest("Search term is required");
                }

                var userId = await GetCurrentUserIdAsync(_context);
                var result = new GlobalSearchResult
                {
                    SearchTerm = searchTerm,
                    CurrentPage = page,
                    PageSize = pageSize
                };

                //search across all entities
                var accountResults = await _context.ChartOfAccounts
                    .Where(a => a.UserId == userId &&
                               (a.AccountName.Contains(searchTerm) ||
                                a.AccountNumber.Contains(searchTerm) ||
                                a.Description.Contains(searchTerm)))
                    .Take(5)
                    .Select(a => new SearchResultItem
                    {
                        Id = a.AccountId,
                        Type = "Account",
                        Title = a.AccountName,
                        Subtitle = $"Account #{a.AccountNumber} - {a.AccountType}",
                        Description = a.Description
                    })
                    .ToListAsync();

                var voucherResults = await _context.VoucherHeaders
                    .Where(v => v.UserId == userId &&
                               (v.VoucherNumber.Contains(searchTerm) ||
                                v.Description.Contains(searchTerm) ||
                                v.TransactionType.Contains(searchTerm)))
                    .Take(5)
                    .Select(v => new SearchResultItem
                    {
                        Id = v.VoucherId,
                        Type = "Voucher",
                        Title = v.VoucherNumber,
                        Subtitle = $"{v.TransactionType} - {v.Status}",
                        Description = v.Description,
                        Amount = v.TotalAmount,
                        Date = v.VoucherDate
                    })
                    .ToListAsync();

                var paymentResults = await _context.PaymentHeaders
                    .Where(p => p.UserId == userId &&
                               (p.PaymentNumber.Contains(searchTerm) ||
                                p.PayeeName.Contains(searchTerm) ||
                                p.Description.Contains(searchTerm)))
                    .Take(5)
                    .Select(p => new SearchResultItem
                    {
                        Id = p.PaymentId,
                        Type = "Payment",
                        Title = p.PaymentNumber,
                        Subtitle = $"To: {p.PayeeName} - {p.Status}",
                        Description = p.Description,
                        Amount = p.TotalAmount,
                        Date = p.PaymentDate
                    })
                    .ToListAsync();

                var receiptResults = await _context.Receipts
                    .Where(r => r.UserId == userId &&
                               (r.ReceiptNumber.Contains(searchTerm) ||
                                r.PayerName.Contains(searchTerm) ||
                                r.Description.Contains(searchTerm)))
                    .Take(5)
                    .Select(r => new SearchResultItem
                    {
                        Id = r.ReceiptId,
                        Type = "Receipt",
                        Title = r.ReceiptNumber,
                        Subtitle = $"From: {r.PayerName}",
                        Description = r.Description,
                        Amount = r.Amount,
                        Date = r.Date
                    })
                    .ToListAsync();

                //combining all results
                result.Results = accountResults
                    .Concat(voucherResults)
                    .Concat(paymentResults)
                    .Concat(receiptResults)
                    .OrderByDescending(r => r.Date)
                    .ToList();

                result.TotalItems = result.Results.Count;
                result.HasMoreResults = result.TotalItems >= 20; //indicate if there might be more

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        //returns dashboard stats for the current user
        [HttpGet("QuickStats")]
        public async Task<ActionResult<QuickStatsResult>> GetQuickStats()
        {
            try
            {
                var userId = await GetCurrentUserIdAsync(_context);

                var stats = new QuickStatsResult
                {
                    TotalAccounts = await _context.ChartOfAccounts.CountAsync(a => a.UserId == userId),
                    TotalVouchers = await _context.VoucherHeaders.CountAsync(v => v.UserId == userId),
                    TotalPayments = await _context.PaymentHeaders.CountAsync(p => p.UserId == userId),
                    TotalReceipts = await _context.Receipts.CountAsync(r => r.UserId == userId),

                    PendingVouchers = await _context.VoucherHeaders
                        .CountAsync(v => v.UserId == userId && v.Status == "Pending"),

                    DraftPayments = await _context.PaymentHeaders
                        .CountAsync(p => p.UserId == userId && p.Status == "Draft"),

                    TotalPaymentsAmount = await _context.PaymentHeaders
                        .Where(p => p.UserId == userId && p.Status == "Paid")
                        .SumAsync(p => p.TotalAmount),

                    TotalReceiptsAmount = await _context.Receipts
                        .Where(r => r.UserId == userId)
                        .SumAsync(r => r.Amount)
                };

                return Ok(stats);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

    //dTOs for Search Results
    public class SearchResult
    {
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public List<object> Items { get; set; }
    }

    public class GlobalSearchResult
    {
        public string SearchTerm { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public bool HasMoreResults { get; set; }
        public List<SearchResultItem> Results { get; set; }
    }

    public class SearchResultItem
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Description { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? Date { get; set; }
    }

    public class QuickStatsResult
    {
        public int TotalAccounts { get; set; }
        public int TotalVouchers { get; set; }
        public int TotalPayments { get; set; }
        public int TotalReceipts { get; set; }
        public int PendingVouchers { get; set; }
        public int DraftPayments { get; set; }
        public decimal TotalPaymentsAmount { get; set; }
        public decimal TotalReceiptsAmount { get; set; }
    }
}